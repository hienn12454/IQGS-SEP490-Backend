using ApplicationLayer.DTOs.Gamification;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

/// <summary>
/// Gamification cho Candidate: XP/Level, streak, daily goal, activity heatmap, achievements, lịch sử XP.
/// Toàn bộ endpoint chỉ đọc/điều chỉnh dữ liệu của CHÍNH candidate đang đăng nhập (UserId lấy từ JWT) —
/// không có endpoint nào nhận userId hay amount XP từ client (xem GamificationService — XP chỉ được
/// award nội bộ từ CandidatePracticeSessionService, dựa trên trạng thái đã persist ở backend).
/// </summary>
[ApiController]
[Route("api/candidate/gamification")]
[Authorize(Roles = "Candidate")]
public class CandidateGamificationController : ControllerBase
{
    private readonly IGamificationService _gamificationService;

    public CandidateGamificationController(IGamificationService gamificationService)
    {
        _gamificationService = gamificationService;
    }

    /// <summary>Trạng thái gamification hiện tại: TotalXp/Level/progress trong level, streak, daily goal hôm nay, tổng số phiên luyện tập.</summary>
    [HttpGet("progress")]
    public async Task<IActionResult> GetProgress()
    {
        var result = await _gamificationService.GetProgressAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Activity heatmap trong khoảng [from, to] (inclusive) — mỗi ngày gồm XP kiếm được, số câu/phiên hoàn thành, có hoạt động hay không.</summary>
    /// <remarks>Bỏ trống from/to → mặc định 30 ngày gần nhất. Khoảng ngày bị giới hạn tối đa theo GamificationOptions.MaxActivityRangeDays.</remarks>
    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity([FromQuery] ActivityQueryDto query)
    {
        var to = query.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = query.From ?? to.AddDays(-29);

        var result = await _gamificationService.GetActivityAsync(User.GetUserId(), from, to);
        return SuccessResp.Ok(result);
    }

    /// <summary>Danh sách achievement (đã mở khoá + chưa) — code-defined rules.</summary>
    [HttpGet("achievements")]
    public async Task<IActionResult> GetAchievements()
    {
        var result = await _gamificationService.GetAchievementsAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Lịch sử giao dịch XP, phân trang, mới nhất trước.</summary>
    [HttpGet("xp-history")]
    public async Task<IActionResult> GetXpHistory([FromQuery] XpHistoryQueryDto query)
    {
        var result = await _gamificationService.GetXpHistoryAsync(User.GetUserId(), query.Page, query.PageSize);
        return SuccessResp.Ok(result);
    }

    /// <summary>Đổi mục tiêu XP/ngày — chỉ chấp nhận giá trị trong GamificationOptions.AllowedDailyGoalXpValues (mặc định 20/50/80/120).</summary>
    [HttpPatch("daily-goal")]
    public async Task<IActionResult> UpdateDailyGoal([FromBody] UpdateDailyGoalDto dto)
    {
        var result = await _gamificationService.UpdateDailyGoalAsync(User.GetUserId(), dto.DailyGoalXp);
        return SuccessResp.Ok(result);
    }
}
