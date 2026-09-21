using ApplicationLayer.DTOs.Candidate;
using DomainLayer.Constants;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-461: gợi ý level liền kề khi Target Readiness = READY.
/// Fresher → Junior → Middle → Senior; Senior không có next.
/// </summary>
public static class NextLevelSuggestion
{
    public sealed record Result(
        string? SuggestedNextLevel,
        bool Available,
        string? Message);

    /// <summary>Level kế tiếp trong thang toàn cục; null nếu đã Senior hoặc không hợp lệ.</summary>
    public static string? TryGetNextLevel(string? currentLevel)
    {
        if (!CoachSeniorityLevel.IsValid(currentLevel))
            return null;

        var normalizedIdx = Array.FindIndex(
            CoachSeniorityLevel.All,
            l => string.Equals(l, currentLevel!.Trim(), StringComparison.OrdinalIgnoreCase));
        if (normalizedIdx < 0 || normalizedIdx >= CoachSeniorityLevel.All.Length - 1)
            return null;

        return CoachSeniorityLevel.All[normalizedIdx + 1];
    }

    /// <summary>
    /// Gắn SuggestedNext* lên DTO. Chỉ khi READY và còn level kế.
    /// Adaptive: luôn Available. Framework: Available khi catalog có đúng (roleKey, nextLevel).
    /// </summary>
    public static Result Build(
        string? targetReadinessStatus,
        string? currentTargetLevel,
        string? resolutionMode,
        bool nextFrameworkExistsExact)
    {
        if (!string.Equals(
                targetReadinessStatus,
                CompetencyTargetReadinessStatus.Ready,
                StringComparison.OrdinalIgnoreCase))
        {
            return new Result(null, false, null);
        }

        var next = TryGetNextLevel(currentTargetLevel);
        if (next is null)
            return new Result(null, false, null);

        var isAdaptive = string.Equals(
            resolutionMode,
            CompetencyResolutionMode.Adaptive,
            StringComparison.OrdinalIgnoreCase);

        if (isAdaptive)
        {
            return new Result(
                next,
                true,
                $"Bạn đã sẵn sàng {currentTargetLevel}. Có thể luyện tiếp hướng {next} (blueprint adaptive).");
        }

        if (nextFrameworkExistsExact)
        {
            return new Result(
                next,
                true,
                $"Bạn đã sẵn sàng {currentTargetLevel}. Xác nhận để chạy diagnostic hướng {next}.");
        }

        return new Result(
            next,
            false,
            $"Chưa có competency framework cho level {next}. Liên hệ Admin import framework rồi thử lại.");
    }

    public static void ApplyToDto(CoachAssessmentDto dto, Result result)
    {
        dto.SuggestedNextLevel = result.SuggestedNextLevel;
        dto.SuggestedNextLevelAvailable = result.Available;
        dto.SuggestedNextLevelMessage = result.Message;
    }
}
