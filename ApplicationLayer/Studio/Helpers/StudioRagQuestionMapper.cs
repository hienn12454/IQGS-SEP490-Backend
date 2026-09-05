using System.Text.Json;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Studio.Contracts;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// SCRUM-367 / SCRUM-390: Map câu hỏi RAG → InterviewQuestion Studio.
/// TagsJson giữ metadata kỹ thuật (skill/focus/rationale/criteria/citations)
/// để Save/Publish sang question_sets đủ giống luồng V1; ListQuestions expose citations cho HR.
/// </summary>
public static class StudioRagQuestionMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Payload nhúng trong InterviewQuestion.TagsJson (snapshot cho Save).</summary>
    public sealed class QuestionMeta
    {
        public string? Skill { get; set; }
        public string? FocusArea { get; set; }
        public string? Rationale { get; set; }
        public string? CodeTemplateType { get; set; }
        public string? CodeSnippet { get; set; }
        /// <summary>SCRUM-396: gợi ý text hình ảnh cho HR.</summary>
        public string? ImageHint { get; set; }
        /// <summary>SCRUM-396: path Azure Blob ảnh HR đính kèm.</summary>
        public string? AttachedImageBlobPath { get; set; }
        /// <summary>SCRUM-400: Text | Code.</summary>
        public string? AnswerMethod { get; set; }
        /// <summary>SCRUM-421: RubricV1 JSON — nguồn sự thật rubric trong TagsJson.</summary>
        public string? RubricJson { get; set; }
        /// <summary>SCRUM-421: Provenance waterfall JD → Admin → LLM.</summary>
        public object? SourceProvenance { get; set; }
        /// <summary>SCRUM-421: Cảnh báo soft_llm — thiếu tài liệu Admin.</summary>
        public bool MissingAdminWarning { get; set; }
        /// <summary>Legacy / mirror criteria objects (string hoặc RubricCriterion).</summary>
        public List<object> EvaluationCriteria { get; set; } = new();
        public List<object> Citations { get; set; } = new();
    }

    /// <summary>SCRUM-418: Resolve rubric từ TagsJson (+ fallback ScoringRubric text).</summary>
    public static RubricNormalizer.RubricDocumentV1 ResolveRubricDocument(QuestionMeta meta, string? scoringRubricFallback = null)
    {
        if (!string.IsNullOrWhiteSpace(meta.RubricJson))
            return RubricNormalizer.NormalizeFromJson(meta.RubricJson);

        if (meta.EvaluationCriteria is { Count: > 0 })
            return RubricNormalizer.NormalizeFromObjects(meta.EvaluationCriteria);

        if (!string.IsNullOrWhiteSpace(scoringRubricFallback))
        {
            var lines = scoringRubricFallback
                .Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return RubricNormalizer.NormalizeFromLegacyStrings(lines);
        }

        return RubricNormalizer.NormalizeFromJson(null);
    }

    public static void ApplyRubricToMeta(QuestionMeta meta, RubricNormalizer.RubricDocumentV1 doc)
    {
        meta.RubricJson = RubricNormalizer.SerializeDocument(doc);
        meta.EvaluationCriteria = doc.Criteria
            .Select(c => (object)new Dictionary<string, object?>
            {
                ["id"] = c.Id,
                ["label"] = c.Label,
                ["weight"] = c.Weight,
                ["anchors"] = c.Anchors
            })
            .ToList();
    }

    public static List<InterviewQuestion> Map(
        IReadOnlyList<RagGeneratedQuestionDto> ragQuestions,
        Guid projectId,
        Guid planId,
        Guid runId,
        IReadOnlyList<PlanSection> sections,
        bool includeSampleAnswers,
        bool includeScoringRubric)
    {
        _ = includeSampleAnswers;
        _ = includeScoringRubric;
        var list = new List<InterviewQuestion>();
        var order = 1;
        foreach (var q in ragQuestions.OrderBy(x => x.Order ?? int.MaxValue))
        {
            if (string.IsNullOrWhiteSpace(q.Question)) continue;

            var type = StudioRagPlanMapper.MapQuestionType(q.QuestionType);
            var difficulty = StudioRagPlanMapper.MapDifficulty(q.Difficulty);
            var sectionId = ResolveSectionId(sections, type, q.FocusArea);

            // SCRUM-418: RAG criteria (string[] hoặc object[]) → RubricV1
            var rubricDoc = RubricNormalizer.NormalizeFromRagCriteria(
                q.EvaluationCriteria?.Cast<object>().ToList());

            // Luôn lưu sample/rationale từ RAG vào entity khi có — Save/Publish marketplace cần đủ field kỹ thuật.
            var sample = string.IsNullOrWhiteSpace(q.SampleAnswer) ? null : q.SampleAnswer.Trim();
            var rationale = string.IsNullOrWhiteSpace(q.Rationale) ? null : q.Rationale.Trim();
            var rubricDisplay = RubricNormalizer.ToDisplayText(rubricDoc);
            if (string.IsNullOrWhiteSpace(rubricDisplay) && !string.IsNullOrWhiteSpace(rationale))
                rubricDisplay = rationale;

            var meta = new QuestionMeta
            {
                Skill = string.IsNullOrWhiteSpace(q.Skill) ? null : q.Skill.Trim(),
                FocusArea = string.IsNullOrWhiteSpace(q.FocusArea) ? null : q.FocusArea.Trim(),
                Rationale = rationale,
                CodeTemplateType = string.IsNullOrWhiteSpace(q.CodeTemplateType) ? null : q.CodeTemplateType.Trim(),
                CodeSnippet = string.IsNullOrWhiteSpace(q.CodeSnippet) ? null : q.CodeSnippet.Trim(),
                ImageHint = string.IsNullOrWhiteSpace(q.ImageHint) ? null : q.ImageHint.Trim(),
                AnswerMethod = ApplicationLayer.Helpers.AnswerMethodNormalizer.Resolve(
                    q.AnswerMethod, q.CodeTemplateType, q.CodeSnippet),
                Citations = q.Citations ?? new List<object>(),
                SourceProvenance = q.SourceProvenance,
                MissingAdminWarning = q.MissingAdminWarning
            };
            ApplyRubricToMeta(meta, rubricDoc);

            list.Add(new InterviewQuestion
            {
                ProjectId = projectId,
                InterviewPlanId = planId,
                PlanSectionId = sectionId,
                GenerationRunId = runId,
                Content = q.Question.Trim(),
                Type = type,
                Difficulty = difficulty,
                OrderIndex = q.Order is > 0 ? q.Order.Value : order,
                EstimatedMinutes = 5,
                ExpectedAnswer = sample,
                ScoringRubric = string.IsNullOrWhiteSpace(rubricDisplay) ? null : rubricDisplay,
                GeneratedByModelName = "RAG",
                TagsJson = JsonSerializer.Serialize(meta, JsonOptions)
            });
            order++;
        }
        return list;
    }

    public static QuestionMeta ParseMeta(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
            return new QuestionMeta();

        try
        {
            // Legacy: TagsJson = ["skill"] array đơn
            var trimmed = tagsJson.TrimStart();
            if (trimmed.StartsWith('['))
            {
                var arr = JsonSerializer.Deserialize<List<string>>(tagsJson, JsonOptions);
                var skill = arr?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
                return new QuestionMeta { Skill = skill?.Trim() };
            }

            return JsonSerializer.Deserialize<QuestionMeta>(tagsJson, JsonOptions) ?? new QuestionMeta();
        }
        catch
        {
            return new QuestionMeta();
        }
    }

    public static string SerializeMeta(QuestionMeta meta)
        => JsonSerializer.Serialize(meta, JsonOptions);

    /// <summary>SCRUM-390: Map entity → DTO list/update/regen (kèm citations từ TagsJson).</summary>
    public static StudioQuestionDto MapToStudioQuestionDto(InterviewQuestion q, string? attachedImageUrl = null)
    {
        var meta = ParseMeta(q.TagsJson);
        var rubricDoc = ResolveRubricDocument(meta, q.ScoringRubric);
        return new StudioQuestionDto(
            q.Id,
            q.Content,
            q.Difficulty,
            q.Type,
            q.OrderIndex,
            q.ExpectedAnswer,
            q.ScoringRubric,
            ExtractCitations(meta.Citations),
            string.IsNullOrWhiteSpace(meta.CodeTemplateType) ? null : meta.CodeTemplateType.Trim(),
            string.IsNullOrWhiteSpace(meta.CodeSnippet) ? null : meta.CodeSnippet.Trim(),
            string.IsNullOrWhiteSpace(meta.ImageHint) ? null : meta.ImageHint.Trim(),
            string.IsNullOrWhiteSpace(attachedImageUrl) ? null : attachedImageUrl.Trim(),
            ApplicationLayer.Helpers.AnswerMethodNormalizer.Resolve(
                meta.AnswerMethod, meta.CodeTemplateType, meta.CodeSnippet),
            RubricNormalizer.SerializeDocument(rubricDoc),
            ParseSourceProvenance(meta.SourceProvenance),
            meta.MissingAdminWarning,
            string.IsNullOrWhiteSpace(meta.Rationale) ? null : meta.Rationale.Trim(),
            // SCRUM-436: skill/tech tag cho badge UI
            string.IsNullOrWhiteSpace(meta.Skill) ? null : meta.Skill.Trim(),
            string.IsNullOrWhiteSpace(meta.FocusArea) ? null : meta.FocusArea.Trim()
        );
    }

    /// <summary>SCRUM-390: Parse citations từ TagsJson / raw list (camelCase + snake_case).</summary>
    public static IReadOnlyList<StudioQuestionCitationDto> ExtractCitationsFromTagsJson(string? tagsJson)
        => ExtractCitations(ParseMeta(tagsJson).Citations);

    public static IReadOnlyList<StudioQuestionCitationDto> ExtractCitations(IEnumerable<object>? raw)
    {
        if (raw is null) return Array.Empty<StudioQuestionCitationDto>();

        var result = new List<StudioQuestionCitationDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in raw)
        {
            if (!TryReadCitation(item, out var file, out var chunk, out var excerpt, out var knowledgeBase,
                    out var origin, out var usedFor, out var reason))
                continue;
            if (string.IsNullOrWhiteSpace(file))
                continue;

            var key = $"{file}|{chunk}|{excerpt}|{knowledgeBase}";
            if (!seen.Add(key))
                continue;

            result.Add(new StudioQuestionCitationDto(
                file.Trim(),
                chunk,
                string.IsNullOrWhiteSpace(excerpt) ? null : excerpt.Trim(),
                NormalizeKnowledgeBase(knowledgeBase, file),
                InferCitationOrigin(origin, knowledgeBase, file),
                ParseUsedFor(usedFor),
                string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));
        }

        return result;
    }

    private static PlanProvenanceBlockDto? ParseSourceProvenance(object? raw)
    {
        if (raw is null) return null;
        try
        {
            if (raw is JsonElement el && el.ValueKind == JsonValueKind.Object)
                return StudioRagPlanMapper.ParseProvenanceBlock(el);

            var json = JsonSerializer.Serialize(raw, JsonOptions);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            return StudioRagPlanMapper.ParseProvenanceBlock(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    private static string? InferCitationOrigin(string? origin, string? knowledgeBase, string sourceFile)
    {
        if (!string.IsNullOrWhiteSpace(origin))
            return origin.Trim().ToUpperInvariant() switch
            {
                "HR" => "HR",
                "SYSTEM" => "SYSTEM",
                "LLM" => "LLM",
                _ => origin.Trim()
            };

        if (IsJobDescriptionSource(sourceFile)) return "HR";
        var kb = NormalizeKnowledgeBase(knowledgeBase, sourceFile);
        return kb switch
        {
            "system" => "SYSTEM",
            "hr" => "HR",
            _ => null
        };
    }

    private static IReadOnlyList<string>? ParseUsedFor(object? usedFor)
    {
        if (usedFor is null) return null;
        try
        {
            if (usedFor is JsonElement el && el.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var u in el.EnumerateArray())
                    if (u.ValueKind == JsonValueKind.String && u.GetString() is { } s)
                        list.Add(s);
                return list.Count > 0 ? list : null;
            }
            if (usedFor is IEnumerable<string> strs)
                return strs.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }
        catch
        {
            // ignore
        }
        return null;
    }

    private static bool TryReadCitation(object item, out string file, out int? chunk, out string? excerpt,
        out string? knowledgeBase, out string? origin, out object? usedFor, out string? reason)
    {
        file = "";
        chunk = null;
        excerpt = null;
        knowledgeBase = null;
        origin = null;
        usedFor = null;
        reason = null;

        try
        {
            if (item is JsonElement el)
                return TryReadCitationElement(el, out file, out chunk, out excerpt, out knowledgeBase,
                    out origin, out usedFor, out reason);

            if (item is string s)
            {
                using var doc = JsonDocument.Parse(s);
                return TryReadCitationElement(doc.RootElement, out file, out chunk, out excerpt, out knowledgeBase,
                    out origin, out usedFor, out reason);
            }

            var json = JsonSerializer.Serialize(item, JsonOptions);
            using var parsed = JsonDocument.Parse(json);
            return TryReadCitationElement(parsed.RootElement, out file, out chunk, out excerpt, out knowledgeBase,
                out origin, out usedFor, out reason);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadCitationElement(
        JsonElement el,
        out string file,
        out int? chunk,
        out string? excerpt,
        out string? knowledgeBase,
        out string? origin,
        out object? usedFor,
        out string? reason)
    {
        file = "";
        chunk = null;
        excerpt = null;
        knowledgeBase = null;
        origin = null;
        usedFor = null;
        reason = null;
        if (el.ValueKind != JsonValueKind.Object) return false;

        file = ReadStringProp(el, "sourceFile", "source_file", "source") ?? "";
        excerpt = ReadStringProp(el, "excerpt");
        chunk = ReadIntProp(el, "chunkIndex", "chunk_index");
        knowledgeBase = ReadStringProp(el, "knowledgeBase", "knowledge_base");
        origin = ReadStringProp(el, "origin");
        reason = ReadStringProp(el, "reason");
        if (el.TryGetProperty("usedFor", out var uf))
            usedFor = uf.Clone();
        else if (el.TryGetProperty("used_for", out var uf2))
            usedFor = uf2.Clone();
        return !string.IsNullOrWhiteSpace(file);
    }

    /// <summary>SCRUM-419: Chuẩn hóa knowledgeBase từ RAG (hr|system).</summary>
    private static string? NormalizeKnowledgeBase(string? knowledgeBase, string sourceFile)
    {
        if (IsJobDescriptionSource(sourceFile))
            return "hr";

        var kb = (knowledgeBase ?? "").Trim().ToLowerInvariant();
        return kb switch
        {
            "hr" => "hr",
            "system" => "system",
            _ => string.IsNullOrWhiteSpace(kb) ? null : kb
        };
    }

    private static bool IsJobDescriptionSource(string sourceFile)
    {
        var n = sourceFile.Trim().ToLowerInvariant();
        return n is "job-description" or "jd" or "job description" or "job_description";
    }

    private static string? ReadStringProp(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var p)) continue;
            if (p.ValueKind == JsonValueKind.String)
            {
                var v = p.GetString();
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
        }
        return null;
    }

    private static int? ReadIntProp(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var p)) continue;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n))
                return n;
            if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var parsed))
                return parsed;
        }
        return null;
    }

    private static Guid ResolveSectionId(IReadOnlyList<PlanSection> sections, QuestionType type, string? focusArea)
    {
        if (sections.Count == 0) return Guid.Empty;

        if (!string.IsNullOrWhiteSpace(focusArea))
        {
            var byFocus = sections.FirstOrDefault(s =>
                s.Name.Contains(focusArea, StringComparison.OrdinalIgnoreCase)
                || (focusArea?.Contains(s.Name, StringComparison.OrdinalIgnoreCase) ?? false));
            if (byFocus is not null) return byFocus.Id;
        }

        var typeName = type.ToString();
        var byType = sections.FirstOrDefault(s =>
            s.Name.Contains(typeName, StringComparison.OrdinalIgnoreCase)
            || typeName.Contains(s.Name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
        return byType?.Id ?? sections[0].Id;
    }
}
