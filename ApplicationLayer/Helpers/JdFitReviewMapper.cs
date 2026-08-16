using System.Text.RegularExpressions;
using ApplicationLayer.DTOs.Rag;

namespace ApplicationLayer.Helpers;

/// <summary>Chuẩn hóa kết quả JD Fit Review — không expose điểm số.</summary>
public static class JdFitReviewMapper
{
    public static readonly HashSet<string> Verdicts = new(StringComparer.OrdinalIgnoreCase)
    {
        "unfit", "fair", "good", "excellent"
    };

    public static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase)
    {
        "onJd", "weak", "offJd", "duplicate"
    };

    public static readonly HashSet<string> ActionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "add", "rewrite", "remove"
    };

    private static readonly Regex NumericScore = new(
        @"(?:\b\d{1,3}\s*/\s*100\b)|(?:\b\d{1,3}\s*%)|(?:\bscore\s*[:=]\s*\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? NormalizeVerdict(string? raw)
    {
        var key = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "unfit" or "poor" or "not a fit" or "chưa phù hợp" => "unfit",
            "fair" or "partial" or "tương đối" => "fair",
            "good" or "tốt" => "good",
            "excellent" or "great" or "tuyệt vời" => "excellent",
            _ => Verdicts.Contains(key) ? key : null
        };
    }

    public static string? NormalizeFlag(string? raw)
    {
        var key = (raw ?? string.Empty).Trim().Replace("_", "").Replace("-", "").Replace(" ", "");
        return key.ToLowerInvariant() switch
        {
            "onjd" or "fit" => "onJd",
            "weak" => "weak",
            "offjd" => "offJd",
            "duplicate" => "duplicate",
            _ => null
        };
    }

    public static string? StripNumeric(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        var cleaned = NumericScore.Replace(text, "");
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim(' ', ',', ';', '.', '-');
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    public static EvaluateQuestionSetResult Map(EvaluateQuestionSetResult rag)
    {
        return new EvaluateQuestionSetResult
        {
            Success = rag.Success,
            Verdict = NormalizeVerdict(rag.Verdict),
            SummaryVi = StripNumeric(rag.SummaryVi),
            SummaryEn = StripNumeric(rag.SummaryEn),
            QuestionFlags = (rag.QuestionFlags ?? new())
                .Select(f => new EvaluateQuestionSetFlagDto
                {
                    QuestionId = f.QuestionId,
                    Order = f.Order,
                    Flag = NormalizeFlag(f.Flag) ?? "",
                    NoteVi = StripNumeric(f.NoteVi),
                    NoteEn = StripNumeric(f.NoteEn),
                    Sources = MapSources(f.Sources)
                })
                .Where(f => !string.IsNullOrEmpty(f.Flag))
                .ToList(),
            MissingTopics = (rag.MissingTopics ?? new())
                .Select(t => StripNumeric(t) ?? t.Trim())
                .Where(t => t.Length > 0)
                .ToList(),
            SuggestedActions = (rag.SuggestedActions ?? new())
                .Select(a =>
                {
                    var type = (a.Type ?? "").Trim().ToLowerInvariant();
                    if (!ActionTypes.Contains(type))
                        return null;
                    return new EvaluateQuestionSetActionDto
                    {
                        Type = type,
                        QuestionId = a.QuestionId,
                        ReasonVi = StripNumeric(a.ReasonVi),
                        ReasonEn = StripNumeric(a.ReasonEn)
                    };
                })
                .Where(a => a is not null)
                .Cast<EvaluateQuestionSetActionDto>()
                .ToList(),
            JdSources = MapSources(rag.JdSources),
            ProcessingTimeMs = rag.ProcessingTimeMs,
            Error = rag.Error,
            Detail = rag.Detail
        };
    }

    public static List<JdFitSourceDto> MapSources(IEnumerable<JdFitSourceDto>? sources)
    {
        var result = new List<JdFitSourceDto>();
        var seen = new HashSet<int>();
        foreach (var s in sources ?? Enumerable.Empty<JdFitSourceDto>())
        {
            if (s.ChunkIndex < 0 || !seen.Add(s.ChunkIndex))
                continue;
            var excerpt = (s.Excerpt ?? "").Trim();
            if (excerpt.Length > 400)
                excerpt = excerpt[..400];
            if (string.IsNullOrWhiteSpace(excerpt))
                continue;
            result.Add(new JdFitSourceDto { ChunkIndex = s.ChunkIndex, Excerpt = excerpt });
            if (result.Count >= 8)
                break;
        }
        return result;
    }
}
