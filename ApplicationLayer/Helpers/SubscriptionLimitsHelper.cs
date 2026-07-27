using System.Text.Json;
using DomainLayer.Entities;

namespace ApplicationLayer.Helpers;

/// <summary>Serialize/deserialize LimitsJson và LimitsSnapshotJson.</summary>
public static class SubscriptionLimitsHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static string Serialize(SubscriptionPlanLimits limits)
        => JsonSerializer.Serialize(limits, JsonOptions);

    public static SubscriptionPlanLimits Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new SubscriptionPlanLimits();

        try
        {
            return JsonSerializer.Deserialize<SubscriptionPlanLimits>(json, JsonOptions)
                   ?? new SubscriptionPlanLimits();
        }
        catch
        {
            return new SubscriptionPlanLimits();
        }
    }

    /// <summary>Số câu Candidate Free được mở: max(1, ceil(total * percent/100)); Premium percent=100.</summary>
    public static int GetVisibleQuestionCount(int totalQuestions, int freeVisiblePercent)
    {
        if (totalQuestions <= 0) return 0;
        var percent = Math.Clamp(freeVisiblePercent, 0, 100);
        if (percent >= 100) return totalQuestions;
        if (percent <= 0) return 0;
        return Math.Max(1, (int)Math.Ceiling(totalQuestions * percent / 100.0));
    }
}
