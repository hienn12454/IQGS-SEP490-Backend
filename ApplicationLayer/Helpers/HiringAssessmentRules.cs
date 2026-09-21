namespace ApplicationLayer.Helpers;

/// <summary>
/// SCRUM-464: quy tắc bộ Tuyển vs Practice — thuần, dễ unit test.
/// </summary>
public static class HiringAssessmentRules
{
    /// <summary>
    /// Anti-cheat phiên practice chỉ bật khi bộ là Tuyển AND HR bật AND Admin platform bật.
    /// </summary>
    public static bool ShouldEnableAntiCheat(
        bool isHiringAssessment,
        bool hrAntiCheatEnabled,
        bool adminAntiCheatEnabled)
        => isHiringAssessment && hrAntiCheatEnabled && adminAntiCheatEnabled;

    /// <summary>
    /// Lần complete đầu trên bộ Tuyển = bài test chính thức (ghi lịch sử HR).
    /// Practice hoặc đã có official trước đó → không đánh dấu.
    /// </summary>
    public static bool IsFirstOfficialComplete(
        bool isHiringAssessment,
        bool alreadyHasOfficialComplete)
        => isHiringAssessment && !alreadyHasOfficialComplete;
}
