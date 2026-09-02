using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ApplicationLayer.Studio.Contracts;
using DomainLayer.Studio;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// Embed HR final settings snapshot + fingerprint vào SourcePlanJson để detect stale plan.
/// </summary>
public static class StudioPlanSettingsSnapshotHelper
{
    public sealed record PlanSettingsSnapshot(
        int NumberOfQuestions,
        string Difficulty,
        IReadOnlyList<QuestionDistributionItemDto> QuestionDistribution,
        IReadOnlyList<StudioFocusAreaItemDto> FocusAreas,
        IReadOnlyList<string> QuestionStyles,
        IReadOnlyList<string> CodingTaskTypes,
        string Fingerprint);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static PlanSettingsSnapshot BuildFrom(StudioSettings settings)
    {
        var distribution = BuildDistribution(settings);
        var focusAreas = StudioAiConfigurationHelper.MapFocusAreas(
            settings.FocusAreas.Where(f => f.IsActive));
        var styles = BuildStyles(settings);
        var coding = BuildCodingTaskTypes(settings.CodeTemplatesJson);
        var difficulty = StudioQuestionTaxonomyMapper.NormalizeDifficulty(settings.Difficulty.ToString());
        var fingerprint = ComputeFingerprint(
            settings.NumberOfQuestions,
            difficulty,
            distribution,
            focusAreas,
            styles,
            coding);

        return new PlanSettingsSnapshot(
            settings.NumberOfQuestions,
            difficulty,
            distribution,
            focusAreas,
            styles,
            coding,
            fingerprint);
    }

    public static string EmbedInSourcePlanJson(string? sourcePlanJson, PlanSettingsSnapshot snapshot)
    {
        var root = ParseRootObject(sourcePlanJson);
        root["hrSettingsSnapshot"] = BuildSnapshotNode(snapshot);
        return root.ToJsonString(JsonOptions);
    }

    public static PlanSettingsSnapshot? TryExtract(string? sourcePlanJson)
    {
        if (string.IsNullOrWhiteSpace(sourcePlanJson)) return null;
        try
        {
            var node = JsonNode.Parse(sourcePlanJson);
            JsonObject? root = ResolveRoot(node);
            if (root?["hrSettingsSnapshot"] is not JsonObject snap) return null;

            var numberOfQuestions = snap["numberOfQuestions"]?.GetValue<int>() ?? 0;
            var difficulty = StudioQuestionTaxonomyMapper.NormalizeDifficulty(
                snap["difficulty"]?.GetValue<string>());

            var distribution = ParseDistributionNode(snap["questionDistribution"]);
            var focusAreas = ParseFocusAreasNode(snap["focusAreas"]);
            var styles = ParseStringArrayNode(snap["questionStyles"])
                .Select(s => StudioQuestionTaxonomyMapper.NormalizeStyle(s))
                .Where(s => s is not null)
                .Cast<string>()
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();
            var coding = ParseStringArrayNode(snap["codingTaskTypes"])
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var storedFingerprint = snap["fingerprint"]?.GetValue<string>();
            var fingerprint = !string.IsNullOrWhiteSpace(storedFingerprint)
                ? storedFingerprint!
                : ComputeFingerprint(numberOfQuestions, difficulty, distribution, focusAreas, styles, coding);

            return new PlanSettingsSnapshot(
                numberOfQuestions,
                difficulty,
                distribution,
                focusAreas,
                styles,
                coding,
                fingerprint);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsStale(PlanSettingsSnapshot? embedded, StudioSettings currentSettings)
    {
        if (embedded is null) return true;
        var current = BuildFrom(currentSettings);
        return !string.Equals(embedded.Fingerprint, current.Fingerprint, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject ParseRootObject(string? sourcePlanJson)
    {
        if (string.IsNullOrWhiteSpace(sourcePlanJson))
            return new JsonObject();

        var node = JsonNode.Parse(sourcePlanJson)
            ?? throw new InvalidOperationException("SourcePlanJson không parse được.");
        return ResolveRoot(node)
            ?? throw new InvalidOperationException("SourcePlanJson không phải object.");
    }

    private static JsonObject? ResolveRoot(JsonNode? node)
    {
        if (node is JsonObject wrap && wrap["plan"] is JsonObject nested)
            return nested;
        return node as JsonObject;
    }

    private static JsonObject BuildSnapshotNode(PlanSettingsSnapshot snapshot)
    {
        var dist = new JsonArray();
        foreach (var d in snapshot.QuestionDistribution)
        {
            dist.Add(new JsonObject
            {
                ["category"] = d.Category,
                ["percentage"] = d.Percentage,
                ["questionCount"] = d.QuestionCount
            });
        }

        var focus = new JsonArray();
        foreach (var f in snapshot.FocusAreas)
        {
            focus.Add(new JsonObject
            {
                ["name"] = f.Name,
                ["weight"] = f.Weight,
                ["orderIndex"] = f.OrderIndex,
                ["description"] = f.Description,
                ["sourceReason"] = f.SourceReason
            });
        }

        var styles = new JsonArray();
        foreach (var s in snapshot.QuestionStyles)
            styles.Add(s);

        var coding = new JsonArray();
        foreach (var c in snapshot.CodingTaskTypes)
            coding.Add(c);

        return new JsonObject
        {
            ["numberOfQuestions"] = snapshot.NumberOfQuestions,
            ["difficulty"] = snapshot.Difficulty,
            ["questionDistribution"] = dist,
            ["focusAreas"] = focus,
            ["questionStyles"] = styles,
            ["codingTaskTypes"] = coding,
            ["fingerprint"] = snapshot.Fingerprint
        };
    }

    private static List<QuestionDistributionItemDto> BuildDistribution(StudioSettings settings)
    {
        var distribution = StudioAiConfigurationHelper.ParseQuestionDistribution(settings.QuestionDistributionJson)
            .Select(d => new QuestionDistributionItemDto(
                StudioQuestionTaxonomyMapper.NormalizeCategory(d.Category),
                d.Percentage,
                d.QuestionCount))
            .OrderBy(d => d.Category, StringComparer.Ordinal)
            .ToList();

        if (distribution.Count == 0 && settings.NumberOfQuestions > 0)
        {
            var types = StudioQuestionTypesHelper.ParseOrDefault(settings.QuestionTypesJson);
            var (derived, _) = StudioQuestionTaxonomyMapper.FromLegacyQuestionTypes(types, settings.NumberOfQuestions);
            distribution = derived
                .Select(d => new QuestionDistributionItemDto(
                    StudioQuestionTaxonomyMapper.NormalizeCategory(d.Category),
                    d.Percentage,
                    d.QuestionCount))
                .OrderBy(d => d.Category, StringComparer.Ordinal)
                .ToList();
        }

        return distribution;
    }

    private static List<string> BuildStyles(StudioSettings settings)
    {
        var styles = StudioAiConfigurationHelper.ParseQuestionStyles(settings.QuestionStylesJson)
            .Select(s => StudioQuestionTaxonomyMapper.NormalizeStyle(s))
            .Where(s => s is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        if (styles.Count == 0)
        {
            var types = StudioQuestionTypesHelper.ParseOrDefault(settings.QuestionTypesJson);
            styles = StudioQuestionTaxonomyMapper.ExtractStylesFromLegacyTypes(types)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();
        }

        return styles;
    }

    private static List<string> BuildCodingTaskTypes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return (JsonSerializer.Deserialize<List<string>>(json) ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static List<QuestionDistributionItemDto> ParseDistributionNode(JsonNode? node)
    {
        var result = new List<QuestionDistributionItemDto>();
        if (node is not JsonArray arr) return result;
        foreach (var item in arr.OfType<JsonObject>())
        {
            var cat = StudioQuestionTaxonomyMapper.NormalizeCategory(item["category"]?.GetValue<string>());
            var pct = item["percentage"]?.GetValue<int>() ?? 0;
            var count = item["questionCount"]?.GetValue<int>()
                ?? item["question_count"]?.GetValue<int>()
                ?? 0;
            result.Add(new QuestionDistributionItemDto(cat, pct, count));
        }
        return result.OrderBy(d => d.Category, StringComparer.Ordinal).ToList();
    }

    private static List<StudioFocusAreaItemDto> ParseFocusAreasNode(JsonNode? node)
    {
        var result = new List<StudioFocusAreaItemDto>();
        if (node is not JsonArray arr) return result;
        foreach (var item in arr.OfType<JsonObject>())
        {
            var name = item["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var weight = item["weight"]?.GetValue<decimal>() ?? 0m;
            var order = item["orderIndex"]?.GetValue<int>()
                ?? item["order_index"]?.GetValue<int>()
                ?? result.Count;
            var desc = item["description"]?.GetValue<string>();
            var reason = item["sourceReason"]?.GetValue<string>()
                ?? item["source_reason"]?.GetValue<string>();
            result.Add(new StudioFocusAreaItemDto(
                name.Trim(),
                StudioFocusAreaWeightHelper.NormalizeToPercent(weight),
                order,
                desc,
                reason));
        }
        return result
            .OrderBy(f => f.OrderIndex)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> ParseStringArrayNode(JsonNode? node)
    {
        if (node is not JsonArray arr) return [];
        return arr
            .Select(x => x?.GetValue<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();
    }

    private static string ComputeFingerprint(
        int numberOfQuestions,
        string difficulty,
        IReadOnlyList<QuestionDistributionItemDto> distribution,
        IReadOnlyList<StudioFocusAreaItemDto> focusAreas,
        IReadOnlyList<string> questionStyles,
        IReadOnlyList<string> codingTaskTypes)
    {
        var payload = new
        {
            numberOfQuestions,
            difficulty = difficulty.ToLowerInvariant(),
            questionDistribution = distribution
                .OrderBy(d => d.Category, StringComparer.Ordinal)
                .Select(d => new
                {
                    category = d.Category,
                    percentage = d.Percentage,
                    questionCount = d.QuestionCount
                }),
            focusAreas = focusAreas
                .OrderBy(f => f.OrderIndex)
                .ThenBy(f => f.Name, StringComparer.Ordinal)
                .Select(f => new
                {
                    name = f.Name.Trim(),
                    weight = StudioFocusAreaWeightHelper.NormalizeForFingerprint(f.Weight),
                    orderIndex = f.OrderIndex,
                    description = string.IsNullOrWhiteSpace(f.Description) ? null : f.Description.Trim(),
                    sourceReason = string.IsNullOrWhiteSpace(f.SourceReason) ? null : f.SourceReason.Trim()
                }),
            questionStyles = questionStyles
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray(),
            codingTaskTypes = codingTaskTypes
                .Select(c => c.ToUpperInvariant())
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
