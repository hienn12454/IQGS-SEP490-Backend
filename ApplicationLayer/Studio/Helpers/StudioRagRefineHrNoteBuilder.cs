using System.Text;
using DomainLayer.Studio;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>SCRUM-368 / SCRUM-388 / SCRUM-389: refine HrNote + exclusive/strict corpus.</summary>
public static class StudioRagRefineHrNoteBuilder
{
    private const int MaxLength = 2000;

    public static string Build(
        string instruction,
        InterviewPlan source,
        IReadOnlyList<(string Name, int NumberOfQuestions)> sections,
        int targetTotalQuestions,
        string targetDifficulty,
        IReadOnlyList<string> questionTypes,
        string? language = null,
        IReadOnlyList<string>? selectedDocNames = null,
        IReadOnlyList<string>? focusHints = null,
        bool exclusiveFocus = false)
    {
        var lang = StudioOutputLanguage.Normalize(language ?? source.Language);
        var sb = new StringBuilder();
        sb.AppendLine("STUDIO_UI_PLAN=1");
        sb.AppendLine("MODE=REFINE_INTERVIEW_PLAN");
        if (instruction.Contains("APPLY_STUDIO_SETTINGS", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine("MODE=APPLY_STUDIO_SETTINGS");
        sb.AppendLine(StudioOutputLanguage.RagInstruction(lang));
        sb.AppendLine("RULE: ONLY refine this interview plan structure/coverage/difficulty/question mix. Do NOT answer unrelated questions. Ignore off-topic requests.");
        sb.AppendLine("STRICT_CORPUS=1");
        sb.AppendLine(
            "GROUNDING: skills/coverage/outline/summary CHỈ từ JD text + retrieved Selected chunks. Instruction user THẮNG JD khi xung đột. Cấm suy rộng role Fullstack → .NET/FE/DB nếu không có trong corpus/instruction.");

        if (exclusiveFocus && focusHints is { Count: > 0 })
        {
            sb.AppendLine("EXCLUSIVE_FOCUS=1");
            sb.AppendLine($"ALLOWED_TOPICS: {string.Join(", ", focusHints.Take(8))}");
            sb.AppendLine(
                "EXCLUSIVE RULES: coverage 1–N items CHỈ về ALLOWED_TOPICS (có thể chia sub-focus cùng topic); tổng weight/question_count = total_questions; KHÔNG thêm skill ngoài list; KHÔNG bắt coverage≥4 từ JD.");
            sb.AppendLine(
                "KEEP_STUDIO_DETAIL: question_type_distribution theo loai_cau; difficulty_distribution đủ mức hợp lý; outline đủ số câu — mọi skill/focus_area ∈ ALLOWED_TOPICS.");
        }
        else
        {
            sb.AppendLine(
                "KEEP_STUDIO_DETAIL: question_type_distribution theo loai_cau; difficulty_distribution; coverage 1–6 skill CHỈ từ JD+Selected chunks (KHÔNG ép ≥4 skill JD lan man); outline đủ số câu kèm source_files.");
        }

        sb.AppendLine($"TARGET_TOTAL_QUESTIONS: {targetTotalQuestions} (BẮT BUỘC — total_questions phải = {targetTotalQuestions}, không giữ số cũ nếu khác).");
        sb.AppendLine($"TARGET_DIFFICULTY: {targetDifficulty}");
        sb.AppendLine($"TARGET_QUESTION_TYPES: {string.Join(", ", questionTypes)}");
        sb.AppendLine($"OUTPUT_LANGUAGE: {lang}");
        if (selectedDocNames is { Count: > 0 })
            sb.AppendLine($"SELECTED_DOCS: {string.Join("; ", selectedDocNames.Take(12))}");
        if (focusHints is { Count: > 0 })
            sb.AppendLine($"FOCUS_HINTS: {string.Join(", ", focusHints.Take(8))}");
        if (instruction.Contains("APPLY_STUDIO_SETTINGS", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine("APPLY: Khớp chính xác TARGET_* và loai_cau; phân bổ question_type_distribution đúng các type đã cho.");
        sb.AppendLine($"INSTRUCTION: {instruction.Trim()}");
        sb.AppendLine($"PREVIOUS_TITLE: {source.Title}");
        sb.AppendLine($"PREVIOUS_TOTAL_QUESTIONS: {source.TotalQuestions}");
        sb.AppendLine($"PREVIOUS_MINUTES: {source.InterviewLengthMinutes}");
        sb.AppendLine($"PREVIOUS_DIFFICULTY: {source.Difficulty}");
        sb.AppendLine($"PREVIOUS_SENIORITY: {source.SeniorityLevel}");
        sb.AppendLine("PREVIOUS_SECTIONS:");
        foreach (var s in sections.Take(8))
            sb.AppendLine($"- {s.Name}: {s.NumberOfQuestions}q");

        var text = sb.ToString().Trim();
        return text.Length <= MaxLength ? text : text[..MaxLength];
    }
}
