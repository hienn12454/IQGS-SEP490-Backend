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
        var flattened = string.Join(' ', t.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        // HrNote dùng ";" để ngăn cách các key (CONTENT_MODE=...; HR_REGEN_NOTE=...).
        // Nếu HR gõ ";" trong lưu ý thì RAG sẽ tưởng lưu ý kết thúc ở đó → đổi thành ",".
        return flattened.Replace(';', ',');
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
        var hrInstruction = NormalizeInstruction(instruction);
        var hasHrInstruction = !string.IsNullOrWhiteSpace(hrInstruction);

        var sb = new StringBuilder();
        sb.Append("STUDIO_UI=1; STUDIO_REGEN=1; ");

        // Đặt lưu ý HR lên ĐẦU hrNote: RAG chỉ lấy ~500 ký tự đầu hrNote để truy vấn tài liệu.
        // Trước đây lưu ý nằm cuối (sau GUID, language, STRICT_FOCUS...) nên bị cắt mất,
        // RAG vẫn đi tìm tài liệu của chủ đề cũ dù HR gõ "hỏi về OOP".
        if (hasHrInstruction)
            sb.Append($"HR_REGEN_NOTE={hrInstruction}; ");

        sb.Append($"Studio project {projectId}; plan {planId}; question {questionId}; ");
        sb.Append($"CONTENT_MODE={contentMode}; CODE_TEMPLATES={templatesSegment}; {langInstruction}");
        var note = sb.ToString();

        // Chỉ khóa STRICT_FOCUS khi HR KHÔNG ghi lưu ý (regen "đổi câu khác cùng chủ đề").
        // Khi HR có lưu ý, họ có thể muốn chủ đề ngoài focus của plan (vd. OOP) —
        // giữ STRICT_FOCUS thì LLM bị ép quay về focus cũ và chỉ viết lại câu cũ.
        if (!hasHrInstruction)
            note = StudioRagPlanHrNoteBuilder.AppendQuestionFocusConstraint(note, focusNames);

        if (!string.IsNullOrWhiteSpace(avoidQuestionsNote))
            note = note.TrimEnd() + "; " + avoidQuestionsNote.Trim();
        return note;
    }

    /// <summary>SCRUM-496: gộp câu đang regen + siblings cho AVOID_QUESTIONS.</summary>
    public static IReadOnlyList<string> MergeAvoidContents(string? currentContent, IEnumerable<string>? siblingContents)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(currentContent))
            list.Add(currentContent);
        if (siblingContents is not null)
        {
            foreach (var s in siblingContents)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s);
            }
        }
        return list;
    }

    /// <summary>SCRUM-429: tóm tắt câu khác để LLM tránh trùng ý. SCRUM-496: gồm cả câu đang regen.</summary>
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

        // RAG chỉ bật TopicOverridden khi HR ghi lưu ý regen và LLM đã đổi sang chủ đề mới.
        // Khi đó skill/focus/goal/nguồn của slot cũ không còn đúng với nội dung câu mới nữa.
        var topicOverridden = rag.TopicOverridden;

        var lockedGoal = topicOverridden ? "" : (slot.Goal ?? "").Trim();
        var rationale = !string.IsNullOrWhiteSpace(lockedGoal)
            ? lockedGoal
            : (string.IsNullOrWhiteSpace(rag.Rationale) ? null : rag.Rationale.Trim());

        if (string.IsNullOrWhiteSpace(rubricDisplay) && !string.IsNullOrWhiteSpace(rationale))
            rubricDisplay = rationale;

        var citations = slot.Citations is { Count: > 0 } && !topicOverridden
            ? CitationsToObjects(slot.Citations)
            : rag.Citations ?? new List<object>();

        // SCRUM-495: mặc định khóa skill/focus/type từ slot HR (chống LLM tự lệch chủ đề).
        // Ngoại lệ: HR chủ động yêu cầu chủ đề khác qua lưu ý regen → lấy skill/focus RAG trả về.
        var (skill, focusArea) = ResolveSkillAndFocus(slot, rag, topicOverridden);
        var meta = new StudioRagQuestionMapper.QuestionMeta
        {
            Skill = skill,
            FocusArea = focusArea,
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

    /// <summary>Chọn skill/focus lưu vào câu: slot HR (mặc định) hoặc RAG (khi HR đổi chủ đề).</summary>
    private static (string? Skill, string? FocusArea) ResolveSkillAndFocus(
        PlanOutlineItemDto slot,
        RagGeneratedQuestionDto rag,
        bool topicOverridden)
    {
        var slotSkill = string.IsNullOrWhiteSpace(slot.Skill) ? null : slot.Skill.Trim();
        var slotFocus = string.IsNullOrWhiteSpace(slot.FocusArea) ? null : slot.FocusArea.Trim();
        if (!topicOverridden)
            return (slotSkill, slotFocus);

        // RAG không trả skill/focus thì vẫn giữ của slot, tránh để trống nhãn trên UI.
        var ragSkill = string.IsNullOrWhiteSpace(rag.Skill) ? slotSkill : rag.Skill.Trim();
        var ragFocus = string.IsNullOrWhiteSpace(rag.FocusArea) ? slotFocus : rag.FocusArea.Trim();
        return (ragSkill, ragFocus);
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
