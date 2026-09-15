namespace ApplicationLayer.Helpers;

/// <summary>
/// SCRUM-446: quy tắc anti-cheat thuần (không I/O) — dễ unit test.
/// Service gọi helper này trước khi ghi DB / tự nộp.
/// </summary>
public static class PracticeAntiCheatRules
{
    public static readonly TimeSpan DefaultTabLeaveDebounce = TimeSpan.FromSeconds(2);

    /// <summary>Clamp ngưỡng rời tab về 1–20 khi snapshot từ PlatformSettings.</summary>
    public static int ClampMaxTabLeaves(int value) => Math.Clamp(value, 1, 20);

    /// <summary>
    /// Quyết định có ghi nhận TAB_HIDDEN hay bỏ qua.
    /// </summary>
    public static bool ShouldCountTabLeave(
        bool antiCheatEnabled,
        string sessionStatus,
        DateTime? lastTabLeaveAt,
        DateTime nowUtc,
        TimeSpan? debounce = null)
    {
        if (!antiCheatEnabled)
            return false;
        if (!string.Equals(sessionStatus, DomainLayer.Constants.PracticeSessionStatus.InProgress, StringComparison.Ordinal))
            return false;

        var window = debounce ?? DefaultTabLeaveDebounce;
        if (lastTabLeaveAt.HasValue && nowUtc - lastTabLeaveAt.Value < window)
            return false;

        return true;
    }

    /// <summary>Đủ ngưỡng → tự nộp bài.</summary>
    public static bool ShouldAutoSubmit(int tabLeaveCount, int maxTabLeaves)
        => tabLeaveCount >= Math.Max(1, maxTabLeaves);
}
