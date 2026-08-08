using System.Text.RegularExpressions;

namespace ApplicationLayer.Helpers;

/// <summary>
/// Parse meta nhúng trong rationale khi Studio Save flatten template vào Question Set History
/// (SCRUM-396 / SCRUM-399): template=...;snippet=...;lang=...;imageHint=...;diagramHint=...
/// </summary>
public static class QuestionRationaleMetaParser
{
    private static readonly Regex MetaKeyRegex = new(
        @"^(template|snippet|lang|language|imageHint|imagePrompt|diagramHint|diagram)=",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public sealed class ParsedMeta
    {
        public string? CodeTemplateType { get; init; }
        public string? CodeSnippet { get; init; }
        public string? SnippetLanguage { get; init; }
        /// <summary>Phần rationale thật (bỏ meta keys) — null nếu chỉ còn meta.</summary>
        public string? CleanRationale { get; init; }
    }

    public static ParsedMeta Parse(string? rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
            return new ParsedMeta();

        string? template = null;
        string? snippet = null;
        string? lang = null;
        var textParts = new List<string>();

        foreach (var rawPart in rationale.Split(';', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(rawPart))
                continue;

            if (!MetaKeyRegex.IsMatch(rawPart))
            {
                textParts.Add(rawPart);
                continue;
            }

            var eq = rawPart.IndexOf('=');
            if (eq <= 0 || eq >= rawPart.Length - 1)
                continue;

            var key = rawPart[..eq].Trim();
            var value = rawPart[(eq + 1)..].Trim();
            if (string.IsNullOrEmpty(value))
                continue;

            if (key.Equals("template", StringComparison.OrdinalIgnoreCase))
                template = value.ToUpperInvariant().Replace(' ', '_');
            else if (key.Equals("snippet", StringComparison.OrdinalIgnoreCase))
                snippet = NormalizeSnippetEscapes(value);
            else if (key.Equals("lang", StringComparison.OrdinalIgnoreCase)
                     || key.Equals("language", StringComparison.OrdinalIgnoreCase))
                lang = value;
            // imageHint / diagramHint: HR-only — bỏ, không expose Candidate
        }

        var clean = textParts.Count > 0 ? string.Join("; ", textParts) : null;
        return new ParsedMeta
        {
            CodeTemplateType = template,
            CodeSnippet = snippet,
            SnippetLanguage = lang,
            CleanRationale = clean
        };
    }

    /// <summary>Chuẩn hóa \\n / \\t literal (Studio flatten) thành newline/tab thật.</summary>
    private static string NormalizeSnippetEscapes(string snippet)
    {
        if (!snippet.Contains('\\'))
            return snippet;

        if (!snippet.Contains('\n') && (snippet.Contains("\\n") || snippet.Contains("\\t")))
            return snippet.Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\t", "\t", StringComparison.Ordinal);

        if (snippet.Contains("\\n", StringComparison.Ordinal))
        {
            var literalCount = CountOccurrences(snippet, "\\n");
            var realCount = snippet.Count(c => c == '\n');
            if (literalCount >= realCount)
            {
                return snippet.Replace("\\n", "\n", StringComparison.Ordinal)
                    .Replace("\\t", "\t", StringComparison.Ordinal);
            }
        }

        return snippet;
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
