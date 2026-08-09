namespace ApplicationLayer.Interfaces.Services;

/// <summary>
/// Quy đổi TotalXp ↔ Level. Công thức: RequiredXp(level) = LevelBaseXp + (level-1) * LevelXpIncrement
/// (XP cần để đi từ `level` lên `level+1`). Pure — không I/O, an toàn với TotalXp rất lớn.
/// </summary>
public interface ILevelCalculator
{
    /// <summary>Level hiện tại ứng với tổng XP đã tích luỹ (level bắt đầu từ 1).</summary>
    int CalculateLevel(long totalXp);

    /// <summary>Tổng XP tích luỹ (ngưỡng) cần để ĐẠT tới `level` (level 1 = 0 XP).</summary>
    long GetTotalXpForLevel(int level);

    /// <summary>XP còn thiếu/incremental cần để đi từ `level` lên `level + 1`.</summary>
    long GetXpRequiredForNextLevel(int level);

    /// <summary>XP đã tích luỹ TRONG level hiện tại (totalXp trừ đi ngưỡng bắt đầu level đó).</summary>
    long GetCurrentLevelXp(long totalXp);

    /// <summary>Phần trăm hoàn thành level hiện tại (0-100), làm tròn 2 chữ số thập phân.</summary>
    decimal GetProgressPercentage(long totalXp);
}
