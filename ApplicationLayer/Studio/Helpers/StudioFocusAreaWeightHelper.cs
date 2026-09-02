namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// FocusArea.weight luôn 0–100 (percentage). Legacy 0–1 được chuẩn hóa khi đọc.
/// </summary>
public static class StudioFocusAreaWeightHelper
{
    public const decimal TargetSum = 100m;
    public const decimal SumTolerance = 0.5m;

    public static decimal NormalizeToPercent(decimal weight)
    {
        if (weight <= 0) return 0m;
        var pct = weight <= 1m ? weight * 100m : weight;
        pct = Math.Round(pct, 2, MidpointRounding.AwayFromZero);
        return Math.Clamp(pct, 0m, 100m);
    }

    public static decimal NormalizeForFingerprint(decimal weightPercent)
        => Math.Round(NormalizeToPercent(weightPercent), 2, MidpointRounding.AwayFromZero);

    public static bool IsValidSum(IEnumerable<decimal> weights)
    {
        var sum = weights.Select(NormalizeToPercent).Sum();
        return Math.Abs(sum - TargetSum) <= SumTolerance;
    }
}
