using ApplicationLayer.DTOs.Subscription;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using ApplicationLayer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace WebAPI.Controllers;

[ApiController]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private static readonly JsonSerializerOptions WebhookJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SubscriptionController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    /// <summary>Danh sách gói active theo audience (HR | Candidate).</summary>
    [HttpGet("api/plans")]
    [AllowAnonymous]
    public async Task<IActionResult> ListPlans([FromQuery] string audience = "HR")
    {
        var plans = await _subscriptionService.ListPlansAsync(audience);
        return SuccessResp.Ok(plans);
    }

    [HttpGet("api/me/subscription")]
    [Authorize]
    public async Task<IActionResult> GetMySubscription()
    {
        var dto = await _subscriptionService.GetMySubscriptionAsync(GetUserId());
        return SuccessResp.Ok(dto);
    }

    [HttpGet("api/me/usage")]
    [Authorize]
    public async Task<IActionResult> GetMyUsage()
    {
        var dto = await _subscriptionService.GetMyUsageAsync(GetUserId());
        return SuccessResp.Ok(dto);
    }

    /// <summary>Tạo đơn SePay để nâng Premium, chưa nâng ngay.</summary>
    [HttpPost("api/me/subscription/upgrade")]
    [Authorize]
    public async Task<IActionResult> Upgrade([FromBody] UpgradeSubscriptionRequestDto? _)
    {
        var dto = await _subscriptionService.CreateUpgradePaymentAsync(GetUserId());
        return SuccessResp.Ok(dto);
    }

    [HttpGet("api/me/subscription/upgrade/{orderCode}")]
    [Authorize]
    public async Task<IActionResult> UpgradeStatus([FromRoute] string orderCode)
    {
        var dto = await _subscriptionService.GetUpgradePaymentStatusAsync(GetUserId(), orderCode);
        return SuccessResp.Ok(dto);
    }

    /// <summary>
    /// Webhook SePay — public. Đọc raw body để verify HMAC (nếu bật),
    /// trả { success: true } theo yêu cầu SePay.
    /// </summary>
    [HttpPost("api/payments/sepay/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> SePayWebhook()
    {
        Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0;
        }

        SePayWebhookRequestDto? payload = null;
        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            try
            {
                payload = JsonSerializer.Deserialize<SePayWebhookRequestDto>(rawBody, WebhookJsonOptions);
            }
            catch (JsonException)
            {
                return BadRequest(new { success = false, message = "Payload webhook không hợp lệ." });
            }
        }

        payload ??= new SePayWebhookRequestDto();
        payload.RawPayloadJson = rawBody;

        var signature = Request.Headers["X-SePay-Signature"].FirstOrDefault()
                        ?? Request.Headers["x-sepay-signature"].FirstOrDefault();
        var timestamp = Request.Headers["X-SePay-Timestamp"].FirstOrDefault()
                        ?? Request.Headers["x-sepay-timestamp"].FirstOrDefault();

        var result = await _subscriptionService.ConfirmUpgradePaymentAsync(
            payload, rawBody, signature, timestamp);

        // SePay yêu cầu HTTP 200 + success=true trong ~30s.
        return Ok(new
        {
            success = true,
            processed = result.Processed,
            paymentAccepted = result.PaymentAccepted,
            orderCode = result.OrderCode,
            message = result.Message
        });
    }

    [HttpPost("api/me/subscription/cancel")]
    [Authorize]
    public async Task<IActionResult> Cancel()
    {
        var dto = await _subscriptionService.CancelAsync(GetUserId());
        return SuccessResp.Ok(dto);
    }

    /// <summary>Mua pack Ask-AI (sandbox) — cộng Extra trong kỳ hiện tại.</summary>
    [HttpPost("api/me/packs/ask-ai")]
    [Authorize(Roles = "HR")]
    public async Task<IActionResult> PurchaseAskAiPack([FromBody] PurchaseAskAiPackRequestDto dto)
    {
        var result = await _subscriptionService.PurchaseAskAiPackAsync(
            GetUserId(), dto.ExtraRequests, dto.Amount);
        return SuccessResp.Ok(result);
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst("sub")?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? throw new UnauthorizedAccessException("Thiếu claim user.");
        return Guid.Parse(sub);
    }
}
