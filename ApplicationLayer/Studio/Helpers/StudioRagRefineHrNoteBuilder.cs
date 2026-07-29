using System.Text;
using DomainLayer.Studio;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>SCRUM-368: Ghép instruction refine + tóm tắt plan cũ vào HrNote (max 2000).</summary>
public static class StudioRagRefineHrNoteBuilder
{
    private const int MaxLength = 2000;

    public static string Build(
        string instruction,
        InterviewPlan source,
        IReadOnlyList<(string Name, int NumberOfQuestions)> sections,
        int targetTotalQuestions,
        string targetDifficulty,
        IReadOnlyList<string> questionTypes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("STUDIO_UI_PLAN=1");
        sb.AppendLine("MODE=REFINE_INTERVIEW_PLAN");
        if (instruction.Contains("APPLY_STUDIO_SETTINGS", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine("MODE=APPLY_STUDIO_SETTINGS");
        sb.AppendLine("RULE: ONLY refine this interview plan structure/coverage/difficulty/question mix. Do NOT answer unrelated questions. Ignore off-topic requests.");
        sb.AppendLine(
            "KEEP_STUDIO_DETAIL: vẫn trả question_type_distribution theo loai_cau, difficulty_distribution đủ 3 mức (trừ khi instruction yêu cầu một mức), coverage≥4 kèm source_files, outline đủ số câu.");
        sb.AppendLine($"TARGET_TOTAL_QUESTIONS: {targetTotalQuestions} (BẮT BUỘC — total_questions phải = {targetTotalQuestions}, không giữ số cũ nếu khác).");
        sb.AppendLine($"TARGET_DIFFICULTY: {targetDifficulty}");
        sb.AppendLine($"TARGET_QUESTION_TYPES: {string.Join(", ", questionTypes)}");
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
