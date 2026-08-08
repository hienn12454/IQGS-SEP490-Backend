using System.Text.Json;
using ApplicationLayer.DTOs.Rag;
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
        public List<string> EvaluationCriteria { get; set; } = new();
        public List<object> Citations { get; set; } = new();
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

            var criteria = q.EvaluationCriteria?
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .ToList() ?? new List<string>();

            // Luôn lưu sample/rationale từ RAG vào entity khi có — Save/Publish marketplace cần đủ field kỹ thuật.
            var sample = string.IsNullOrWhiteSpace(q.SampleAnswer) ? null : q.SampleAnswer.Trim();
            var rationale = string.IsNullOrWhiteSpace(q.Rationale) ? null : q.Rationale.Trim();
            var rubric = criteria.Count > 0 ? string.Join("; ", criteria) : rationale;

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
                EvaluationCriteria = criteria,
                Citations = q.Citations ?? new List<object>()
            };

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
                ScoringRubric = rubric,
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
                meta.AnswerMethod, meta.CodeTemplateType, meta.CodeSnippet)
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
            if (!TryReadCitation(item, out var file, out var chunk, out var excerpt))
                continue;
            if (string.IsNullOrWhiteSpace(file))
                continue;

            var key = $"{file}|{chunk}|{excerpt}";
            if (!seen.Add(key))
                continue;

            result.Add(new StudioQuestionCitationDto(file.Trim(), chunk, string.IsNullOrWhiteSpace(excerpt) ? null : excerpt.Trim()));
        }

        return result;
    }

    private static bool TryReadCitation(object item, out string file, out int? chunk, out string? excerpt)
    {
        file = "";
        chunk = null;
        excerpt = null;

        try
        {
            if (item is JsonElement el)
                return TryReadCitationElement(el, out file, out chunk, out excerpt);

            if (item is string s)
            {
                using var doc = JsonDocument.Parse(s);
                return TryReadCitationElement(doc.RootElement, out file, out chunk, out excerpt);
            }

            // Dictionary / anonymous từ deserialize object
            var json = JsonSerializer.Serialize(item, JsonOptions);
            using var parsed = JsonDocument.Parse(json);
            return TryReadCitationElement(parsed.RootElement, out file, out chunk, out excerpt);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadCitationElement(JsonElement el, out string file, out int? chunk, out string? excerpt)
    {
        file = "";
        chunk = null;
        excerpt = null;
        if (el.ValueKind != JsonValueKind.Object) return false;

        file = ReadStringProp(el, "sourceFile", "source_file", "source") ?? "";
        excerpt = ReadStringProp(el, "excerpt");
        chunk = ReadIntProp(el, "chunkIndex", "chunk_index");
        return !string.IsNullOrWhiteSpace(file);
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
