using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Studio.Contracts;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>SCRUM-428: cắt plan 1 slot + apply kết quả RAG regen lên InterviewQuestion.</summary>
public static class StudioQuestionRegenHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public const int MaxInstructionLength = 1000;

    public static string? NormalizeInstruction(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return null;
        var t = instruction.Trim();
        if (t.Length > MaxInstructionLength)
            t = t[..MaxInstructionLength];
        // HrNote dùng ; — thay newline bằng space
        return string.Join(' ', t.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static PlanOutlineItemDto ResolveSlot(
        InterviewQuestion question,
        string? sourcePlanJson)
    {
        var outline = StudioRagPlanMapper.ExtractOutlineItems(sourcePlanJson);
        var byOrder = outline.FirstOrDefault(o => o.Order == question.OrderIndex);
        if (byOrder is not null)
            return byOrder;

        var meta = StudioRagQuestionMapper.ParseMeta(question.TagsJson);
        var type = question.Type.ToString().ToLowerInvariant() switch
        {
            "behavioral" => "behavioral",
            "systemdesign" => "system-design",
            "problemsolving" => "problem-solving",
            _ => "technical"
        };
        var skill = meta.Skill ?? "";
        var focus = meta.FocusArea ?? skill;
        var goal = meta.Rationale ?? (string.IsNullOrWhiteSpace(focus)
            ? $"Đánh giá năng lực {type}."
            : $"Đánh giá {focus} trong ngữ cảnh vị trí.");
        var answer = AnswerMethodNormalizer.Resolve(meta.AnswerMethod, meta.CodeTemplateType, meta.CodeSnippet);
        var citations = StudioRagQuestionMapper.ExtractCitations(meta.Citations);
        return new PlanOutlineItemDto(
            question.OrderIndex > 0 ? question.OrderIndex : 1,
            type,
            question.Difficulty.ToString().ToLowerInvariant(),
            skill,
            focus,
            goal,
            answer,
            citations.Count > 0 ? citations : null);
    }

    /// <summary>Mini approvedPlan: totalQuestions=1 + đúng 1 outline slot.</summary>
    public static object BuildSingleSlotApprovedPlan(string? sourcePlanJson, PlanOutlineItemDto slot)
    {
        JsonObject root;
        try
        {
            if (!string.IsNullOrWhiteSpace(sourcePlanJson))
            {
                var node = JsonNode.Parse(sourcePlanJson);
                root = node as JsonObject
                    ?? (node?["plan"] as JsonObject)
                    ?? new JsonObject();
                // Deep clone qua parse lại để không mutate SourcePlanJson gốc
                root = JsonNode.Parse(root.ToJsonString())!.AsObject();
            }
            else
            {
                root = new JsonObject();
            }
        }
        catch
        {
            root = new JsonObject();
        }

        if (string.IsNullOrWhiteSpace(root["roleTitle"]?.GetValue<string>())
            && string.IsNullOrWhiteSpace(root["role_title"]?.GetValue<string>()))
            root["roleTitle"] = "Interview";

        root["totalQuestions"] = 1;
        root["total_questions"] = 1;
        root["difficulty"] = slot.Difficulty;
        root["summary"] = root["summary"]?.GetValue<string>()
            ?? $"Regenerate single question order {slot.Order}.";

        // Mini plan chỉ 1 câu — order luôn 1 để RAG batch không lệch
        var slotForPlan = slot with { Order = 1 };
        var outlineItem = BuildOutlineJson(slotForPlan);
        var outlineArr = new JsonArray { outlineItem };
        root["recommendedQuestionOutline"] = outlineArr;
        root["recommended_question_outline"] = outlineArr.DeepClone();

        var dist = new JsonArray
        {
            new JsonObject
            {
                ["type"] = slot.Type,
                ["count"] = 1,
                ["reason"] = "studio-regen-single-slot"
            }
        };
        root["questionTypeDistribution"] = dist;
        root["question_type_distribution"] = dist.DeepClone();

        return JsonSerializer.Deserialize<object>(root.ToJsonString(JsonOptions), JsonOptions)
            ?? root;
    }

    public static string BuildRegenHrNote(
        Guid projectId,
        Guid planId,
        Guid questionId,
        string contentMode,
        IReadOnlyList<string> codeTemplates,
        string langInstruction,
        IReadOnlyList<string> focusNames,
        string? instruction,
        string? avoidQuestionsNote = null)
    {
        var templatesSegment = codeTemplates.Count > 0
            ? string.Join(",", codeTemplates)
            : "BUG_DETECTION,CODE_COMPLETION";
        var sb = new StringBuilder();
        sb.Append($"STUDIO_UI=1; STUDIO_REGEN=1; Studio project {projectId}; plan {planId}; question {questionId}; ");
        sb.Append($"CONTENT_MODE={contentMode}; CODE_TEMPLATES={templatesSegment}; {langInstruction}");
        var note = StudioRagPlanHrNoteBuilder.AppendQuestionFocusConstraint(sb.ToString(), focusNames);
        var hr = NormalizeInstruction(instruction);
        if (!string.IsNullOrWhiteSpace(hr))
            note = note.TrimEnd() + $"; HR_REGEN_NOTE={hr}";
        if (!string.IsNullOrWhiteSpace(avoidQuestionsNote))
            note = note.TrimEnd() + "; " + avoidQuestionsNote.Trim();
        return note;
    }

    /// <summary>SCRUM-429: tóm tắt câu khác để LLM tránh trùng ý.</summary>
    public static string? BuildAvoidQuestionsNote(IEnumerable<string> otherQuestionContents, int maxItems = 12, int maxLenEach = 120)
    {
        var parts = new List<string>();
        foreach (var raw in otherQuestionContents)
        {
            if (parts.Count >= maxItems) break;
            var t = (raw ?? "").Trim().Replace('\r', ' ').Replace('\n', ' ');
            if (t.Length == 0) continue;
            if (t.Length > maxLenEach) t = t[..maxLenEach].TrimEnd() + "…";
            // Separator không dùng ; (hrNote) — dùng |#|
            parts.Add(t.Replace("|#|", " "));
        }
        if (parts.Count == 0) return null;
        return "AVOID_QUESTIONS=" + string.Join("|#|", parts);
    }

    /// <summary>Ghi đè content/meta; giữ Id/Order/Plan/ảnh đính kèm.</summary>
    public static void ApplyRagResultToQuestion(
        InterviewQuestion target,
        RagGeneratedQuestionDto rag,
        PlanOutlineItemDto slot,
        bool includeSampleAnswers,
        bool includeScoringRubric)
    {
        if (string.IsNullOrWhiteSpace(rag.Question))
            throw new InvalidOperationException("RAG không trả nội dung câu hỏi.");

        var existingMeta = StudioRagQuestionMapper.ParseMeta(target.TagsJson);
        var blobPath = existingMeta.AttachedImageBlobPath;

        var type = StudioRagPlanMapper.MapQuestionType(
            string.IsNullOrWhiteSpace(rag.QuestionType) ? slot.Type : rag.QuestionType);
        var difficulty = StudioRagPlanMapper.MapDifficulty(
            string.IsNullOrWhiteSpace(rag.Difficulty) ? slot.Difficulty : rag.Difficulty);

        var rubricDoc = RubricNormalizer.NormalizeFromRagCriteria(
            rag.EvaluationCriteria?.Cast<object>().ToList());
        var rubricDisplay = RubricNormalizer.ToDisplayText(rubricDoc);

        var lockedGoal = (slot.Goal ?? "").Trim();
        var rationale = !string.IsNullOrWhiteSpace(lockedGoal)
            ? lockedGoal
            : (string.IsNullOrWhiteSpace(rag.Rationale) ? null : rag.Rationale.Trim());

        if (string.IsNullOrWhiteSpace(rubricDisplay) && !string.IsNullOrWhiteSpace(rationale))
            rubricDisplay = rationale;

        var citations = slot.Citations is { Count: > 0 }
            ? CitationsToObjects(slot.Citations)
            : rag.Citations ?? new List<object>();

        var meta = new StudioRagQuestionMapper.QuestionMeta
        {
            Skill = string.IsNullOrWhiteSpace(rag.Skill) ? slot.Skill : rag.Skill.Trim(),
            FocusArea = string.IsNullOrWhiteSpace(rag.FocusArea) ? slot.FocusArea : rag.FocusArea.Trim(),
            Rationale = rationale,
            CodeTemplateType = string.IsNullOrWhiteSpace(rag.CodeTemplateType) ? null : rag.CodeTemplateType.Trim(),
            CodeSnippet = string.IsNullOrWhiteSpace(rag.CodeSnippet) ? null : rag.CodeSnippet.Trim(),
            ImageHint = string.IsNullOrWhiteSpace(rag.ImageHint) ? null : rag.ImageHint.Trim(),
            AnswerMethod = AnswerMethodNormalizer.Resolve(
                rag.AnswerMethod ?? slot.AnswerMethod, rag.CodeTemplateType, rag.CodeSnippet),
            Citations = citations,
            SourceProvenance = rag.SourceProvenance,
            MissingAdminWarning = rag.MissingAdminWarning,
            AttachedImageBlobPath = blobPath
        };
        StudioRagQuestionMapper.ApplyRubricToMeta(meta, rubricDoc);

        target.Content = rag.Question.Trim();
        target.Type = type;
        target.Difficulty = difficulty;
        if (includeSampleAnswers)
            target.ExpectedAnswer = string.IsNullOrWhiteSpace(rag.SampleAnswer) ? null : rag.SampleAnswer.Trim();
        else
            target.ExpectedAnswer = null;

        if (includeScoringRubric)
            target.ScoringRubric = string.IsNullOrWhiteSpace(rubricDisplay) ? null : rubricDisplay;
        else
            target.ScoringRubric = null;

        target.GeneratedByModelName = "RAG";
        target.TagsJson = StudioRagQuestionMapper.SerializeMeta(meta);
        target.UpdatedAt = DateTime.UtcNow;
    }

    private static JsonObject BuildOutlineJson(PlanOutlineItemDto slot)
    {
        var obj = new JsonObject
        {
            ["order"] = slot.Order,
            ["type"] = slot.Type,
            ["difficulty"] = slot.Difficulty,
            ["skill"] = slot.Skill,
            ["focusArea"] = slot.FocusArea,
            ["focus_area"] = slot.FocusArea,
            ["goal"] = slot.Goal,
            ["answerMethod"] = slot.AnswerMethod,
            ["answer_method"] = slot.AnswerMethod
        };

        if (slot.Citations is { Count: > 0 })
        {
            var arr = new JsonArray();
            foreach (var c in slot.Citations)
            {
                if (string.IsNullOrWhiteSpace(c.SourceFile)) continue;
                var cit = new JsonObject
                {
                    ["sourceFile"] = c.SourceFile,
                    ["source_file"] = c.SourceFile
                };
                if (c.ChunkIndex is int ci)
                {
                    cit["chunkIndex"] = ci;
                    cit["chunk_index"] = ci;
                }
                if (!string.IsNullOrWhiteSpace(c.Excerpt))
                    cit["excerpt"] = c.Excerpt;
                if (!string.IsNullOrWhiteSpace(c.KnowledgeBase))
                {
                    cit["knowledgeBase"] = c.KnowledgeBase;
                    cit["knowledge_base"] = c.KnowledgeBase;
                }
                if (!string.IsNullOrWhiteSpace(c.Origin))
                    cit["origin"] = c.Origin;
                if (c.UsedFor is { Count: > 0 })
                {
                    var uf = new JsonArray();
                    foreach (var u in c.UsedFor)
                        uf.Add(JsonValue.Create(u));
                    cit["usedFor"] = uf;
                    cit["used_for"] = uf.DeepClone();
                }
                if (!string.IsNullOrWhiteSpace(c.Reason))
                    cit["reason"] = c.Reason;
                arr.Add(cit);
            }
            if (arr.Count > 0)
                obj["citations"] = arr;
        }

        return obj;
    }

    private static List<object> CitationsToObjects(IReadOnlyList<StudioQuestionCitationDto> citations)
    {
        var list = new List<object>();
        foreach (var c in citations)
        {
            var dict = new Dictionary<string, object?>
            {
                ["sourceFile"] = c.SourceFile,
                ["chunkIndex"] = c.ChunkIndex,
                ["excerpt"] = c.Excerpt,
                ["knowledgeBase"] = c.KnowledgeBase,
                ["origin"] = c.Origin,
                ["usedFor"] = c.UsedFor,
                ["reason"] = c.Reason
            };
            list.Add(dict);
        }
        return list;
    }
}
