using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-457: TargetScore Adaptive lấy từ policy — MVP fallback OverallReadyThreshold (70).
/// Khi TargetScoreByLevelJson có key theo level thì dùng key đó, không đổi scoring engine.
/// </summary>
public static class CompetencyTargetScorePolicy
{
    public static double Resolve(CompetencyScoringPolicy policy, string? targetLevel)
    {
        var json = policy.TargetScoreByLevelJson;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var level = (targetLevel ?? CoachSeniorityLevel.Junior).Trim();
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (string.Equals(prop.Name, level, StringComparison.OrdinalIgnoreCase)
                            && prop.Value.TryGetDouble(out var v)
                            && v >= 0 && v <= 100)
                            return v;
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // JSON hỏng → fallback threshold.
            }
        }

        return policy.OverallReadyThreshold > 0 ? policy.OverallReadyThreshold : 70;
    }
}
