using System.Security.Claims;
using ApplicationLayer.DTOs.Subscription;
using ApplicationLayer.ResponseCode;
using ApplicationLayer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

/// <summary>Admin quản lý template gói + gán Premium user + stats SePay (SCRUM-385/386).</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminSubscriptionPlansController : ControllerBase
{
    private readonly IAdminSubscriptionPlanService _service;

    public AdminSubscriptionPlansController(IAdminSubscriptionPlanService service)
    {
        _service = service;
    }

    [HttpGet("plans")]
    public async Task<IActionResult> List()
        => SuccessResp.Ok(await _service.ListAsync());

    [HttpPost("plans")]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionPlanDto dto)
        => SuccessResp.Ok(await _service.CreateAsync(dto));

    /// <summary>Cập nhật template gói. Limits mới áp dụng từ kỳ tiếp theo cho subscriber đang active.</summary>
    [HttpPut("plans/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubscriptionPlanDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return SuccessResp.Ok(new
        {
            plan = result,
            message = "Áp dụng từ kỳ sau cho subscriber hiện có. User mới đăng ký nhận limit mới ngay."
        });
    }

    [HttpGet("users/{userId:guid}/usage")]
    public async Task<IActionResult> GetUserUsage(Guid userId)
        => SuccessResp.Ok(await _service.GetUserUsageAsync(userId));

    /// <summary>SCRUM-386: xem subscription (EnsureCurrentPeriod — hết hạn Premium tự Free).</summary>
    [HttpGet("users/{userId:guid}/subscription")]
    public async Task<IActionResult> GetUserSubscription(Guid userId)
        => SuccessResp.Ok(await _service.GetUserSubscriptionAsync(userId));

    /// <summary>Admin gán Premium + reset kỳ (1/3/6/12 tháng). Hết PeriodEnd → Free.</summary>
    [HttpPost("users/{userId:guid}/subscription/activate-premium")]
    public async Task<IActionResult> ActivatePremium(Guid userId, [FromBody] AdminGrantPremiumDto? dto)
        => SuccessResp.Ok(await _service.GrantPremiumAsync(
            userId,
            dto ?? new AdminGrantPremiumDto(),
            GetAdminUserId()));

    /// <summary>Gia hạn / reset kỳ khi user đang Premium.</summary>
    [HttpPut("users/{userId:guid}/subscription")]
    public async Task<IActionResult> UpdateUserSubscription(Guid userId, [FromBody] AdminUpdateUserSubscriptionDto dto)
        => SuccessResp.Ok(await _service.UpdateUserSubscriptionPeriodAsync(userId, dto, GetAdminUserId()));

    /// <summary>Thu hồi Premium → Free ngay.</summary>
    [HttpPost("users/{userId:guid}/subscription/revoke")]
    public async Task<IActionResult> RevokePremium(Guid userId, [FromBody] AdminRevokePremiumDto? dto)
        => SuccessResp.Ok(await _service.RevokePremiumAsync(
            userId,
            dto ?? new AdminRevokePremiumDto(),
            GetAdminUserId()));

    [HttpGet("usage/summary")]
    public async Task<IActionResult> GetUsageSummary()
        => SuccessResp.Ok(await _service.GetUsageSummaryAsync());

    /// <summary>KPI doanh thu / Premium / conversion từ DB nội bộ.</summary>
    [HttpGet("subscription/stats")]
    public async Task<IActionResult> GetSubscriptionStats()
        => SuccessResp.Ok(await _service.GetPaymentStatsAsync());

    /// <summary>Giao dịch subscription gần đây (kèm user/plan).</summary>
    [HttpGet("subscription/transactions")]
    public async Task<IActionResult> ListSubscriptionTransactions([FromQuery] int take = 20)
        => SuccessResp.Ok(await _service.ListRecentTransactionsAsync(take));

    /// <summary>Overview trực quan từ SePay User API v2 + khớp đơn local.</summary>
    [HttpGet("subscription/sepay-overview")]
    public async Task<IActionResult> GetSePayOverview([FromQuery] bool refresh = false)
        => SuccessResp.Ok(await _service.GetSePayOverviewAsync(forceRefresh: refresh));

    private Guid GetAdminUserId()
    {
        var raw = User.FindFirst("sub")?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? throw new UnauthorizedAccessException("Missing user id claim.");
        return Guid.Parse(raw);
    }
}
