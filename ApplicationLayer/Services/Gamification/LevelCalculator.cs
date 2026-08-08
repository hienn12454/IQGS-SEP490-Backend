using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Settings;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services.Gamification;

/// <summary>
/// RequiredXp(level) = LevelBaseXp + (level-1) * LevelXpIncrement (mặc định 100 + (level-1)*50).
/// TotalXpForLevel(L) = sum của RequiredXp(1..L-1) — với tham số mặc định thu gọn thành
/// 25*(L-1)*(L+2), nhưng công thức tổng quát dưới đây tính đúng với mọi LevelBaseXp/LevelXpIncrement cấu hình được.
/// </summary>
public class LevelCalculator : ILevelCalculator
{
    private readonly long _baseXp;
    private readonly long _increment;

    public LevelCalculator(IOptions<GamificationOptions> options)
    {
        var o = options.Value;
        _baseXp = Math.Max(1, o.LevelBaseXp);
        _increment = Math.Max(0, o.LevelXpIncrement);
    }

    public long GetXpRequiredForNextLevel(int level)
    {
        var lvl = Math.Max(1, level);
        return _baseXp + (long)(lvl - 1) * _increment;
    }

    public long GetTotalXpForLevel(int level)
    {
        var target = Math.Max(1, level);
        if (target == 1) return 0;

        var n = target - 1; // số bước RequiredXp(1..n) cần cộng dồn
        // sum_{k=1}^{n} [baseXp + (k-1)*increment] = n*baseXp + increment * n*(n-1)/2
        return (long)n * _baseXp + _increment * ((long)n * (n - 1) / 2);
    }

    public int CalculateLevel(long totalXp)
    {
        var xp = Math.Max(0, totalXp);

        // Ước lượng ban đầu bằng nghiệm xấp xỉ (an toàn với totalXp rất lớn nhờ double sqrt),
        // sau đó hiệu chỉnh bằng vài bước lặp nguyên (long) để loại sai số floating-point.
        var level = Math.Max(1, EstimateLevel(xp));

        while (GetTotalXpForLevel(level + 1) <= xp)
            level++;
        while (level > 1 && GetTotalXpForLevel(level) > xp)
            level--;

        return level;
    }

    public long GetCurrentLevelXp(long totalXp)
    {
        var xp = Math.Max(0, totalXp);
        var level = CalculateLevel(xp);
        return xp - GetTotalXpForLevel(level);
    }

    public decimal GetProgressPercentage(long totalXp)
    {
        var xp = Math.Max(0, totalXp);
        var level = CalculateLevel(xp);
        var required = GetXpRequiredForNextLevel(level);
        if (required <= 0) return 100m;

        var current = GetCurrentLevelXp(xp);
        var pct = Math.Clamp((decimal)current / required * 100m, 0m, 100m);
        return Math.Round(pct, 2);
    }

    /// <summary>Nghiệm xấp xỉ của TotalXpForLevel(L) &lt;= totalXp (bậc 2, tổng quát theo baseXp/increment).</summary>
    private int EstimateLevel(long totalXp)
    {
        if (_increment == 0)
        {
            // Tuyến tính: TotalXpForLevel(L) = (L-1) * baseXp
            var l = totalXp / _baseXp + 1;
            return (int)Math.Clamp(l, 1, int.MaxValue);
        }

        // TotalXpForLevel(L) = (L-1)*baseXp + increment*(L-1)*(L-2)/2 <= totalXp
        // Đặt n = L-1: increment*n^2 + (2*baseXp - increment)*n - 2*totalXp <= 0
        var a = _increment;
        var b = 2.0 * _baseXp - _increment;
        var c = -2.0 * totalXp;
        var discriminant = b * b - 4.0 * a * c;
        if (discriminant < 0) discriminant = 0;

        var n = (-b + Math.Sqrt(discriminant)) / (2.0 * a);
        var level = Math.Floor(n) + 1;
        if (double.IsNaN(level) || double.IsInfinity(level)) level = 1;

        return (int)Math.Clamp(level, 1, int.MaxValue);
    }
}
