using System.Text.Json;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Studio.Contracts;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>Parse/persist Studio Phase 1 AI configuration (JD extract + recommendation draft).</summary>
public static class StudioAiConfigurationHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string SerializeExtractedInformation(
        IReadOnlyList<string> responsibilities,
        string? summary)
    {
        var payload = new
        {
            responsibilities = responsibilities.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray(),
            summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim()
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static (IReadOnlyList<string> Responsibilities, string? Summary) ParseExtractedInformation(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return ([], null);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var responsibilities = new List<string>();
            if (root.TryGetProperty("responsibilities", out var resp) && resp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in resp.EnumerateArray())
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        responsibilities.Add(s.Trim());
                }
            }

            string? summary = null;
            if (root.TryGetProperty("summary", out var sum) && sum.ValueKind == JsonValueKind.String)
                summary = sum.GetString()?.Trim();

            return (responsibilities, summary);
        }
        catch
        {
            return ([], null);
        }
    }

    public static JobProfileDto BuildJobProfile(
        string? title,
        string? detectedRole,
        string? detectedSeniority,
        string? detectedLanguage,
        string[] skills,
        string? extractedInformationJson)
    {
        var (responsibilities, summary) = ParseExtractedInformation(extractedInformationJson);
        var experienceLevel = StudioJdSeniority.ToRagExperienceLevel(detectedSeniority);
        return new JobProfileDto(
            JobTitle: FirstNonEmpty(title, detectedRole),
            ExperienceLevel: experienceLevel,
            DetectedRole: detectedRole,
            DetectedLanguage: NormalizeLanguage(detectedLanguage),
            Skills: skills,
            Responsibilities: responsibilities,
            Summary: summary);
    }

    public static RagJobProfileInput ToRagJobProfile(JobProfileDto profile)
        => new()
        {
            JobTitle = profile.JobTitle,
            ExperienceLevel = profile.ExperienceLevel,
            DetectedRole = profile.DetectedRole,
            DetectedLanguage = profile.DetectedLanguage,
            Skills = profile.Skills.ToList(),
            Responsibilities = profile.Responsibilities.ToList(),
            Summary = profile.Summary
        };

    public static RecommendedConfigurationDto? ParseRecommendedConfiguration(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return MapRecommendedConfiguration(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    public static RecommendedConfigurationDto MapRecommendedConfiguration(JsonElement cfg)
    {
        var total = cfg.TryGetProperty("numberOfQuestions", out var nq) ? nq.GetInt32() : 10;
        var difficulty = StudioQuestionTaxonomyMapper.NormalizeDifficulty(
            cfg.TryGetProperty("difficulty", out var diff) ? diff.GetString() : null);

        var distribution = new List<QuestionDistributionItemDto>();
        if (cfg.TryGetProperty("questionDistribution", out var dist) && dist.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dist.EnumerateArray())
            {
                var cat = StudioQuestionTaxonomyMapper.NormalizeCategory(
                    item.TryGetProperty("category", out var c) ? c.GetString() : null);
                var pct = item.TryGetProperty("percentage", out var p) ? p.GetInt32() : 0;
                var count = item.TryGetProperty("questionCount", out var qc) ? qc.GetInt32() : 0;
                distribution.Add(new QuestionDistributionItemDto(cat, pct, count));
            }
        }

        var focusAreas = new List<StudioFocusAreaItemDto>();
        if (cfg.TryGetProperty("focusAreas", out var fa) && fa.ValueKind == JsonValueKind.Array)
        {
            var idx = 0;
            foreach (var item in fa.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) continue;
                var weight = StudioFocusAreaWeightHelper.NormalizeToPercent(
                    item.TryGetProperty("weight", out var w) ? w.GetDecimal() : 0m);
                var order = item.TryGetProperty("orderIndex", out var o) ? o.GetInt32() : idx;
                var desc = item.TryGetProperty("description", out var d) ? d.GetString() : null;
                var reason = item.TryGetProperty("sourceReason", out var r) ? r.GetString() : null;
                focusAreas.Add(new StudioFocusAreaItemDto(name.Trim(), weight, order, desc, reason));
                idx++;
            }
        }

        var styles = new List<string>();
        if (cfg.TryGetProperty("questionStyles", out var qs) && qs.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in qs.EnumerateArray())
            {
                var norm = StudioQuestionTaxonomyMapper.NormalizeStyle(item.GetString());
                if (norm is not null && !styles.Contains(norm, StringComparer.Ordinal))
                    styles.Add(norm);
            }
        }

        var coding = new List<string>();
        if (cfg.TryGetProperty("codingTaskTypes", out var ct) && ct.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in ct.EnumerateArray())
            {
                var raw = item.GetString();
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var key = raw.Trim().ToUpperInvariant().Replace(' ', '_');
                if (key == "SYSTEM_DESIGN") continue;
                if (StudioQuestionTaxonomyMapper.CanonicalCodingTaskTypes.Contains(key) && !coding.Contains(key))
                    coding.Add(key);
            }
        }

        var codingRecommended = cfg.TryGetProperty("codingTasksRecommended", out var cr) && cr.GetBoolean();

        return new RecommendedConfigurationDto(
            total,
            difficulty,
            distribution,
            focusAreas,
            styles,
            coding,
            codingRecommended);
    }

    public static string SerializeRecommendedConfiguration(RecommendedConfigurationDto dto)
    {
        var payload = new
        {
            numberOfQuestions = dto.NumberOfQuestions,
            difficulty = dto.Difficulty,
            questionDistribution = dto.QuestionDistribution.Select(d => new
            {
                category = d.Category,
                percentage = d.Percentage,
                questionCount = d.QuestionCount
            }),
            focusAreas = dto.FocusAreas.Select(f => new
            {
                name = f.Name,
                weight = f.Weight,
                description = f.Description,
                sourceReason = f.SourceReason,
                orderIndex = f.OrderIndex
            }),
            questionStyles = dto.QuestionStyles,
            codingTaskTypes = dto.CodingTaskTypes,
            codingTasksRecommended = dto.CodingTasksRecommended
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static IReadOnlyList<QuestionDistributionItemDto> ParseQuestionDistribution(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<QuestionDistributionItemDto>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string SerializeQuestionDistribution(IReadOnlyList<QuestionDistributionItemDto> items)
        => JsonSerializer.Serialize(items, JsonOptions);

    public static IReadOnlyList<string> ParseQuestionStyles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string SerializeQuestionStyles(IReadOnlyList<string> styles)
        => JsonSerializer.Serialize(styles, JsonOptions);

    /// <summary>
    /// Giữ % từ AI, scale questionCount theo tổng câu HR (numberOfQuestions cột phải).
    /// </summary>
    public static IReadOnlyList<QuestionDistributionItemDto> ScaleDistributionToTotal(
        IReadOnlyList<QuestionDistributionItemDto> source,
        int totalQuestions)
    {
        if (source.Count == 0 || totalQuestions < 1) return source;
        var pcts = source.Select(d => Math.Clamp(d.Percentage, 0, 100)).ToList();
        var pctSum = pcts.Sum();
        if (pctSum <= 0)
        {
            // Fallback: chia đều nếu AI không trả %
            var baseCount = totalQuestions / source.Count;
            var rem = totalQuestions % source.Count;
            return source.Select((d, i) =>
            {
                var count = baseCount + (i < rem ? 1 : 0);
                var pct = totalQuestions > 0 ? (int)Math.Round(100.0 * count / totalQuestions) : 0;
                return d with { QuestionCount = count, Percentage = pct };
            }).ToList();
        }

        var counts = new int[source.Count];
        var assigned = 0;
        for (var i = 0; i < source.Count; i++)
        {
            counts[i] = (int)Math.Floor(totalQuestions * (pcts[i] / (double)pctSum));
            assigned += counts[i];
        }
        var leftover = totalQuestions - assigned;
        // Phân phần dư theo phần thập phân lớn nhất
        var order = Enumerable.Range(0, source.Count)
            .OrderByDescending(i => totalQuestions * (pcts[i] / (double)pctSum) - counts[i])
            .ToList();
        for (var k = 0; k < leftover && k < order.Count; k++)
            counts[order[k]]++;

        return source.Select((d, i) =>
        {
            var pct = totalQuestions > 0 ? (int)Math.Round(100.0 * counts[i] / totalQuestions) : 0;
            return d with { QuestionCount = counts[i], Percentage = pct };
        }).ToList();
    }

    public static List<StudioFocusAreaItemDto> MapFocusAreas(IEnumerable<DomainLayer.Studio.StudioFocusArea> rows)
        => rows
            .OrderBy(x => x.OrderIndex)
            .Select(x => new StudioFocusAreaItemDto(
                x.Name,
                StudioFocusAreaWeightHelper.NormalizeToPercent(x.Weight),
                x.OrderIndex,
                x.Description,
                x.SourceReason))
            .ToList();

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return null;
    }

    private static string? NormalizeLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var key = value.Trim().ToLowerInvariant();
        return key is "vietnamese" or "english" ? key : null;
    }
}
