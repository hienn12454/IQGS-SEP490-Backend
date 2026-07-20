using System.Text.RegularExpressions;

namespace ApplicationLayer.Helpers;

/// <summary>
/// Gate trước khi gọi RAG evaluate — bỏ qua AI (tiết kiệm token) khi câu trả lời trống,
/// từ chối trả lời ("không biết"), hoặc spam/không phải câu trả lời thực.
/// </summary>
public static partial class AnswerEvaluationGate
{
    public const int MinMeaningfulLength = 10;

    private static readonly HashSet<string> RefusalPhrases = new(StringComparer.Ordinal)
    {
        "không biết",
        "khong biet",
        "ko biết",
        "ko biet",
        "không ro",
        "không rõ",
        "khong ro",
        "khong rõ",
        "chưa biết",
        "chua biet",
        "i don't know",
        "i dont know",
        "idk",
        "n/a",
        "na",
        "none",
        ".",
        "-",
        "???",
        "?",
        "??"
    };

    /// <summary>
    /// Trả true nếu nên bỏ qua gọi RAG. <paramref name="reason"/> là thông báo tiếng Việt ghi vào feedback.
    /// </summary>
    public static bool TrySkip(string? answerText, out string reason)
    {
        var trimmed = (answerText ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            reason = "Câu trả lời trống — điểm 0, không gọi AI.";
            return true;
        }

        if (trimmed.Length < MinMeaningfulLength)
        {
            reason = "Câu trả lời quá ngắn — điểm 0, không gọi AI.";
            return true;
        }

        var normalized = NormalizeForMatch(trimmed);

        if (RefusalPhrases.Contains(normalized)
            || RefusalPhrases.Contains(StripTrailingPunctuation(normalized)))
        {
            reason = "Câu trả lời từ chối / không giải đáp câu hỏi — điểm 0, không gọi AI.";
            return true;
        }

        if (IsLowLetterRatio(trimmed) || IsMostlyRepeatedCharacter(trimmed))
        {
            reason = "Câu trả lời không hợp lệ (spam / không có nội dung) — điểm 0, không gọi AI.";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static string NormalizeForMatch(string text)
    {
        // FormC + lower — giữ dấu tiếng Việt để so khớp phrase có dấu; thêm bản không dấu riêng nếu cần
        var lower = text.Trim().ToLowerInvariant();
        lower = CollapseWhitespaceRegex().Replace(lower, " ");
        return lower;
    }

    private static string StripTrailingPunctuation(string text)
        => text.TrimEnd('.', '!', '?', ',', ';', '…', ' ').Trim();

    private static bool IsLowLetterRatio(string text)
    {
        var letters = 0;
        var total = 0;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
                continue;
            total++;
            if (char.IsLetter(ch))
                letters++;
        }

        if (total == 0)
            return true;

        return (double)letters / total < 0.30;
    }

    private static bool IsMostlyRepeatedCharacter(string text)
    {
        var letters = text.Where(char.IsLetter).Select(char.ToLowerInvariant).ToList();
        if (letters.Count < MinMeaningfulLength)
            return false;

        var mostCommonCount = letters.GroupBy(c => c).Max(g => g.Count());
        return (double)mostCommonCount / letters.Count >= 0.80;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespaceRegex();
}
