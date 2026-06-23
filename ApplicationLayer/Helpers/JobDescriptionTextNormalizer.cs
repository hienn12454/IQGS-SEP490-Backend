using System.Text.RegularExpressions;

namespace ApplicationLayer.Helpers;

/// <summary>
/// Chuẩn hóa JD text trước khi gọi RAG validate-jd — xử lý client gửi một dòng dài hoặc literal \n.
/// </summary>
public static partial class JobDescriptionTextNormalizer
{
    private const int MinLinesForSentenceSplit = 5;
    private const int MinCharsForSentenceSplit = 200;

    public static string PrepareForValidation(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

        // Một số client/Swagger gửi chuỗi literal "\n" thay vì ký tự xuống dòng.
        if (!normalized.Contains('\n') && normalized.Contains("\\n", StringComparison.Ordinal))
            normalized = normalized.Replace("\\n", "\n", StringComparison.Ordinal);

        if (CountMeaningfulLines(normalized) >= MinLinesForSentenceSplit)
            return normalized;

        // JD một dòng nhưng nhiều câu — tách theo dấu câu để RAG đếm đủ meaningful_lines.
        if (!normalized.Contains('\n') && normalized.Length >= MinCharsForSentenceSplit)
        {
            var sentences = SentenceEndSplitRegex()
                .Split(normalized)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            if (sentences.Count >= MinLinesForSentenceSplit)
                return string.Join('\n', sentences);
        }

        return normalized;
    }

    public static int CountMeaningfulLines(string text)
        => text.Split('\n').Count(line => !string.IsNullOrWhiteSpace(line));

    [GeneratedRegex(@"(?<=[.!?…])\s+")]
    private static partial Regex SentenceEndSplitRegex();
}
