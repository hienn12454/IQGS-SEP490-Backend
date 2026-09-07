using DomainLayer.Entities;

namespace ApplicationLayer.Helpers;

/// <summary>
/// HR Free: 1 lần hoàn thành tạo bộ (sinh câu / JD-fit) trong cửa sổ cooldown,
/// rồi khóa FE qua LastSuccessfulGenerateAt.
/// </summary>
public static class HrGenerateWindow
{
    /// <summary>ScopeKey UsageCounter riêng — không trộn với thống kê tổng kỳ (ScopeKey rỗng).</summary>
    public const string ScopeKey = "window";

    public static int ResolveMax(SubscriptionPlanLimits limits)
        => limits.GeneratePerWindow > 0 ? limits.GeneratePerWindow : 1;

    public static bool IsCooldownActive(DateTime? lastSuccessAt, int cooldownHours, DateTime utcNow)
    {
        if (!lastSuccessAt.HasValue)
            return false;

        var hours = Math.Max(1, cooldownHours);
        return utcNow < lastSuccessAt.Value.AddHours(hours);
    }

    /// <summary>
    /// Tính used + LastSuccessfulGenerateAt sau 1 lần hoàn thành tạo bộ / JD-fit.
    /// Last set khi đạt max — FE khóa 24h.
    /// </summary>
    public static (int UsedCount, DateTime? LastSuccessfulGenerateAt) NextMark(
        int currentUsed,
        DateTime? lastSuccessAt,
        int cooldownHours,
        int max,
        DateTime utcNow)
    {
        var hours = Math.Max(1, cooldownHours);
        var cap = Math.Max(1, max);
        var used = Math.Max(0, currentUsed);

        var windowExpired = lastSuccessAt.HasValue
            && utcNow >= lastSuccessAt.Value.AddHours(hours)
            && used >= cap;
        if (windowExpired)
            used = 0;

        used += 1;
        DateTime? nextLast = used >= cap ? utcNow : null;
        return (used, nextLast);
    }
}
