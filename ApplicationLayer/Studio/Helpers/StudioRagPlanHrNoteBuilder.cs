using System.Text;
using ApplicationLayer.Studio.Contracts;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// HrNote cho Studio generate plan — yêu cầu RAG trả outline/distribution đủ để FE hiển thị card chi tiết.
/// Marker STUDIO_UI_PLAN: RAG plan_generation_service nhận diện và bổ sung hướng dẫn UI.
/// SCRUM-417: inject Position / Role / Level HR đã confirm.
/// Phase E: fallback segments khi structured request fields không có.
/// </summary>
public static class StudioRagPlanHrNoteBuilder
{
    private const int MaxLength = 2000;

    public static string BuildInitial(
        Guid projectId,
        int selectedDocs,
        int totalQuestions,
        int interviewMinutes,
        string? language = null,
        string? targetPosition = null,
        string? confirmedRole = null,
        string? confirmedSeniority = null,
        IReadOnlyList<QuestionDistributionItemDto>? questionDistribution = null,
        IReadOnlyList<StudioFocusAreaItemDto>? focusAreas = null,
        IReadOnlyList<string>? questionStyles = null,
        IReadOnlyList<string>? codingTaskTypes = null,
        string? contentMode = null)
        => BuildInitialCore(
            projectId,
            selectedDocs,
            totalQuestions,
            interviewMinutes,
            language,
            targetPosition,
            confirmedRole,
            confirmedSeniority,
            questionDistribution,
            focusAreas,
            questionStyles,
            codingTaskTypes,
            contentMode);

    private static string BuildInitialCore(
        Guid projectId,
        int selectedDocs,
        int totalQuestions,
        int interviewMinutes,
        string? language,
        string? targetPosition,
        string? confirmedRole,
        string? confirmedSeniority,
        IReadOnlyList<QuestionDistributionItemDto>? questionDistribution,
        IReadOnlyList<StudioFocusAreaItemDto>? focusAreas,
        IReadOnlyList<string>? questionStyles,
        IReadOnlyList<string>? codingTaskTypes,
        string? contentMode)
    {
        var lang = StudioOutputLanguage.Normalize(language);
        var isEn = lang == StudioOutputLanguage.English;
        var natural = isEn ? "English" : "Vietnamese (Tiếng Việt)";

        var position = string.IsNullOrWhiteSpace(targetPosition) ? null : targetPosition.Trim();
        var role = string.IsNullOrWhiteSpace(confirmedRole) ? null : confirmedRole.Trim();
        var seniority = StudioJdSeniority.NormalizeDisplay(confirmedSeniority);
        var roleTitleHint = role ?? position;

        var sb = new StringBuilder();
        sb.AppendLine("STUDIO_UI_PLAN=1");
        sb.AppendLine("STRICT_CORPUS=1");
        sb.AppendLine(StudioOutputLanguage.RagInstruction(lang));
        if (position is not null)
            sb.AppendLine($"Vị trí mục tiêu: {position}");
        if (role is not null)
            sb.AppendLine($"Vai trò: {role}");
        if (seniority is not null)
            sb.AppendLine($"Cấp độ bắt buộc (HR đã xác nhận): {seniority}");
        sb.AppendLine($"Studio project {projectId}; selectedDocs={selectedDocs}; targetMinutes={interviewMinutes}.");
        sb.AppendLine("YÊU CẦU HIỂN THỊ STUDIO (BẮT BUỘC):");
        sb.AppendLine(
            $"- total_questions={totalQuestions}; difficulty_distribution (easy/medium/hard) cộng đúng {totalQuestions}, không dồn hết 1 mức.");
        sb.AppendLine(
            $"- question_type_distribution: đủ các loại trong settings (thường technical/system_design/problem_solving/behavioral) — mỗi loại count>0 nếu được chọn, reason bằng {natural}.");
        sb.AppendLine(
            $"- recommended_question_outline: đúng số câu; mỗi item có type/difficulty/skill/focus_area/goal rõ (goal bằng {natural}).");
        sb.AppendLine(
            $"- coverage: ĐỦ mọi skill trong list skills HR/JD (1 coverage item / skill); question_count cộng đúng {totalQuestions}; không bỏ skill; không bịa skill ngoài list; summary 2–3 câu bằng {natural}.");
        sb.AppendLine(
            "- Mỗi coverage PHẢI có source_files = tên file RAG thật từ retrieve (để UI hiện focus từ tài liệu nào).");
        sb.AppendLine(
            "- Section UI mong đợi map từ type: Technical Foundations, System Design, Problem Solving, Behavioral.");
        if (roleTitleHint is not null)
            sb.AppendLine($"- roleTitle phải bám sát: {roleTitleHint}.");
        if (seniority is not null)
            sb.AppendLine(
                $"- experience_level BẮT BUỘC = {seniority.ToLowerInvariant()} (HR đã xác nhận — không đổi).");

        AppendStructuredFallback(sb, questionDistribution, focusAreas, questionStyles, codingTaskTypes, contentMode);

        var text = sb.ToString().Trim();
        return text.Length <= MaxLength ? text : text[..MaxLength];
    }

    private static void AppendStructuredFallback(
        StringBuilder sb,
        IReadOnlyList<QuestionDistributionItemDto>? questionDistribution,
        IReadOnlyList<StudioFocusAreaItemDto>? focusAreas,
        IReadOnlyList<string>? questionStyles,
        IReadOnlyList<string>? codingTaskTypes,
        string? contentMode)
    {
        if (focusAreas is { Count: > 0 })
        {
            // SCRUM-434: gửi đủ FOCUS_HINTS (không Take(8))
            var hints = focusAreas
                .OrderBy(f => f.OrderIndex)
                .Select(f => f.Name.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n));
            sb.AppendLine($"FOCUS_HINTS: {string.Join(", ", hints)}");
        }

        if (questionDistribution is { Count: > 0 })
        {
            var segments = questionDistribution
                .OrderBy(d => d.Category, StringComparer.Ordinal)
                .Select(d =>
                    $"{StudioQuestionTaxonomyMapper.NormalizeCategory(d.Category)}:{d.Percentage}:{d.QuestionCount}");
            sb.AppendLine($"TARGET_QUESTION_DISTRIBUTION={string.Join(",", segments)}");
        }

        var stylesNote = questionStyles is { Count: > 0 }
            ? StudioQuestionTaxonomyMapper.ToQuestionStylesHrNote(questionStyles)
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(stylesNote))
            sb.AppendLine(stylesNote);

        if (codingTaskTypes is { Count: > 0 })
        {
            var templates = codingTaskTypes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase);
            sb.AppendLine($"CODE_TEMPLATES={string.Join(",", templates)}");
        }

        if (!string.IsNullOrWhiteSpace(contentMode))
            sb.AppendLine($"CONTENT_MODE={contentMode.Trim()}");
    }

    /// <summary>Sinh câu hỏi: HR đã chốt focus trên plan — JD chỉ là ngữ cảnh vị trí.</summary>
    public static string AppendQuestionFocusConstraint(string hrNote, IReadOnlyList<string> focusNames)
    {
        var names = focusNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0) return hrNote;
        var extra =
            "STRICT_FOCUS=1; FOCUS_AREAS=" + string.Join("|", names) +
            ". Chỉ sinh câu hỏi về các focus này. skill và focus_area mỗi câu PHẢI thuộc list. " +
            "JD chỉ giải thích vì sao skill đó cần cho vị trí — KHÔNG mở rộng sang skill khác trong JD.";
        var combined = string.IsNullOrWhiteSpace(hrNote) ? extra : hrNote.TrimEnd() + " " + extra;
        return combined.Length <= 4000 ? combined : combined[..4000];
    }
}
