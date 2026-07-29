using System.Text;
using ApplicationLayer.Studio.Contracts;
using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>SCRUM-388: Tóm tắt thay đổi settings + citations cho chat assistant.</summary>
public static class StudioChatRefineMessageBuilder
{
    public static string Build(
        int revision,
        string title,
        int totalQuestions,
        IReadOnlyList<string> changedFields,
        IReadOnlyList<string> citationFiles,
        IReadOnlyList<string>? focusHints = null,
        bool exclusiveFocus = false,
        bool usedLocalPatch = false)
    {
        var sb = new StringBuilder();
        sb.Append($"Đã cập nhật plan (revision {revision}): {title} — {totalQuestions} câu hỏi.");
        if (usedLocalPatch)
            sb.Append(" Nhanh (không gọi RAG).");
        if (exclusiveFocus && focusHints is { Count: > 0 })
            sb.Append($" Exclusive: {string.Join(", ", focusHints.Take(5))}.");
        if (changedFields.Count > 0)
            sb.Append($" Settings: {string.Join(", ", changedFields)}.");
        if (!exclusiveFocus && focusHints is { Count: > 0 })
            sb.Append($" Focus: {string.Join(", ", focusHints.Take(5))}.");
        if (citationFiles.Count > 0)
            sb.Append($" Nguồn RAG: {string.Join(", ", citationFiles.Take(6))}.");
        else if (!usedLocalPatch)
            sb.Append(" (Chưa có citation file — plan dựa JD/instruction hoặc corpus trống.)");
        return sb.ToString();
    }

    public static List<string> DescribeChanges(
        StudioRefineInstructionParser.StudioChatSettingsIntent intent,
        int numberOfQuestions,
        int minutes,
        QuestionDifficulty difficulty,
        IReadOnlyList<string> questionTypes,
        string language,
        string? tone,
        string? outputFormat,
        bool? sample,
        bool? rubric)
    {
        var changed = new List<string>();
        if (intent.NumberOfQuestions is not null || intent.QuestionDelta != 0)
            changed.Add($"số câu={numberOfQuestions}");
        if (intent.InterviewLengthMinutes is not null)
            changed.Add($"thời lượng={minutes} phút");
        else if (intent.NumberOfQuestions is not null || intent.QuestionDelta != 0)
            changed.Add($"thời lượng≈{minutes} phút (gợi ý)");
        if (intent.Difficulty is not null)
            changed.Add($"độ khó={difficulty}");
        if (intent.QuestionTypes is { Count: > 0 } || intent.TechnicalOnly)
            changed.Add($"loại=[{string.Join(", ", questionTypes)}]");
        if (intent.Language is not null)
            changed.Add($"ngôn ngữ={language}");
        if (intent.QuestionTone is not null && tone is not null)
            changed.Add($"văn phong={tone}");
        if (intent.OutputFormat is not null && outputFormat is not null)
            changed.Add($"format={outputFormat}");
        if (intent.IncludeSampleAnswers is not null)
            changed.Add($"sampleAnswers={sample}");
        if (intent.IncludeScoringRubric is not null)
            changed.Add($"scoringRubric={rubric}");
        return changed;
    }
}
