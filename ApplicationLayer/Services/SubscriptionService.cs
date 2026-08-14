using ApplicationLayer.DTOs.Subscription;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ApplicationLayer.Services;

public interface ISubscriptionService
{
    Task AssignFreePlanAsync(Guid userId, string audience);
    Task<List<SubscriptionPlanDto>> ListPlansAsync(string audience);
    Task<MySubscriptionDto> GetMySubscriptionAsync(Guid userId);
    Task<UpgradePaymentIntentDto> CreateUpgradePaymentAsync(Guid userId);
    Task<UpgradePaymentIntentDto> GetUpgradePaymentStatusAsync(Guid userId, string orderCode);
    Task<SePayWebhookProcessResultDto> ConfirmUpgradePaymentAsync(
        SePayWebhookRequestDto webhook,
        string? rawBody = null,
        string? signatureHeader = null,
        string? timestampHeader = null);
    Task<MySubscriptionDto> CancelAsync(Guid userId);
    Task<MySubscriptionDto> PurchaseAskAiPackAsync(Guid userId, int extraRequests, decimal amount);
    Task<List<UsageCounterDto>> GetMyUsageAsync(Guid userId);
}

public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionPlanRepository _planRepo;
    private readonly ISubscriptionRepository _subscriptionRepo;
    private readonly ISubscriptionTransactionRepository _txRepo;
    private readonly IUsageCounterRepository _usageRepo;
    private readonly IUsageMeteringService _metering;
    private readonly ISePayGateway _sePayGateway;
    private readonly SePaySettings _sePaySettings;
    private readonly ISubscriptionPaymentRealtimeNotifier _paymentNotifier;
    private readonly IUserRepository _userRepo;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public SubscriptionService(
        ISubscriptionPlanRepository planRepo,
        ISubscriptionRepository subscriptionRepo,
        ISubscriptionTransactionRepository txRepo,
        IUsageCounterRepository usageRepo,
        IUsageMeteringService metering,
        ISePayGateway sePayGateway,
        IOptions<SePaySettings> sePaySettings,
        ISubscriptionPaymentRealtimeNotifier paymentNotifier,
        IUserRepository userRepo,
        IEmailService emailService,
        IConfiguration config)
    {
        _planRepo = planRepo;
        _subscriptionRepo = subscriptionRepo;
        _txRepo = txRepo;
        _usageRepo = usageRepo;
        _metering = metering;
        _sePayGateway = sePayGateway;
        _sePaySettings = sePaySettings.Value;
        _paymentNotifier = paymentNotifier;
        _userRepo = userRepo;
        _emailService = emailService;
        _config = config;
    }

    public async Task AssignFreePlanAsync(Guid userId, string audience)
    {
        var existing = await _subscriptionRepo.GetByUserIdAsync(userId);
        if (existing != null) return;

        var freeCode = audience == SubscriptionAudience.HR
            ? SubscriptionPlanCodes.HrFree
            : SubscriptionPlanCodes.CandidateFree;

        var plan = await _planRepo.GetByCodeAsync(freeCode)
            ?? throw new ServerFailureException($"Chưa seed plan {freeCode}.");

        var now = DateTime.UtcNow;
        await _subscriptionRepo.AddAsync(new Subscription
        {
            UserId = userId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = now.AddMonths(1),
            LimitsSnapshotJson = plan.LimitsJson,
            StartedAt = now
        });
    }

    public async Task<List<SubscriptionPlanDto>> ListPlansAsync(string audience)
    {
        var normalized = NormalizeAudience(audience);
        var plans = await _planRepo.ListActiveByAudienceAsync(normalized);
        return plans.Select(ToPlanDto).ToList();
    }

    public async Task<MySubscriptionDto> GetMySubscriptionAsync(Guid userId)
    {
        var sub = await _metering.GetOrThrowSubscriptionAsync(userId);
        return await BuildMyDtoAsync(sub);
    }

    public async Task<UpgradePaymentIntentDto> CreateUpgradePaymentAsync(Guid userId)
    {
        if (!_sePaySettings.Enabled)
            throw new BadRequestException("Tạm thời chưa bật thanh toán SePay.");

        var sub = await _metering.GetOrThrowSubscriptionAsync(userId);
        var plan = sub.Plan ?? await _planRepo.GetByIdAsync(sub.PlanId)
            ?? throw new NotFoundException("Không xác định được plan hiện tại.");
        var audience = plan.Audience;

        var premiumCode = audience == SubscriptionAudience.HR
            ? SubscriptionPlanCodes.HrPremium
            : SubscriptionPlanCodes.CandidatePremium;

        if (plan.Code == premiumCode)
            throw new BadRequestException("Bạn đang ở gói Premium rồi.");

        var premium = await _planRepo.GetByCodeAsync(premiumCode)
            ?? throw new ServerFailureException($"Chưa seed plan {premiumCode}.");

        // Hủy mọi Pending upgrade cũ khi user tạo đơn mới (crash/vào lại + bấm tạo).
        await CancelPendingUpgradesAsync(sub.Id);

        var orderCode = BuildOrderCode(audience);
        var sePayOrder = await _sePayGateway.CreateUpgradeOrderAsync(new SePayCreateOrderRequest
        {
            OrderCode = orderCode,
            Amount = premium.PriceMonthly,
            Currency = premium.Currency,
            Description = $"Upgrade {audience} Premium"
        });

        await _txRepo.AddAsync(new SubscriptionTransaction
        {
            SubscriptionId = sub.Id,
            Type = SubscriptionTransactionTypes.Upgrade,
            Amount = sePayOrder.Amount,
            Currency = sePayOrder.Currency,
            Status = SubscriptionTransactionStatus.Pending,
            Provider = sePayOrder.Provider,
            OrderCode = sePayOrder.OrderCode,
            ExternalTransactionId = sePayOrder.ExternalTransactionId,
            ExpiresAt = sePayOrder.ExpiresAt,
            RawPayloadJson = sePayOrder.RawPayloadJson,
            Note = $"SePay upgrade order cho {premium.Code}"
        });

        return ToUpgradeIntentDto(sePayOrder);
    }

    /// <summary>Cancel tất cả Pending Upgrade cùng subscription — supersede bởi đơn mới.</summary>
    private async Task CancelPendingUpgradesAsync(Guid subscriptionId)
    {
        var pendings = await _txRepo.ListPendingUpgradesBySubscriptionAsync(subscriptionId);
        foreach (var old in pendings)
        {
            old.Status = SubscriptionTransactionStatus.Cancelled;
            var note = string.IsNullOrWhiteSpace(old.Note)
                ? "Superseded by new order"
                : $"{old.Note} | Superseded by new order";
            old.Note = note.Length > 500 ? note[..500] : note;
            await _txRepo.UpdateAsync(old);
        }
    }

    public async Task<UpgradePaymentIntentDto> GetUpgradePaymentStatusAsync(Guid userId, string orderCode)
    {
        var sub = await _metering.GetOrThrowSubscriptionAsync(userId);
        var tx = await _txRepo.GetByOrderCodeAsync(orderCode)
            ?? throw new NotFoundException("Không tìm thấy đơn nâng cấp.");

        if (tx.SubscriptionId != sub.Id)
            throw new ForbiddenException("Bạn không có quyền xem đơn này.");

        if (tx.Status == SubscriptionTransactionStatus.Pending
            && tx.ExpiresAt.HasValue
            && tx.ExpiresAt.Value <= DateTime.UtcNow)
        {
            tx.Status = SubscriptionTransactionStatus.Expired;
            await _txRepo.UpdateAsync(tx);
        }

        // Tái tạo QR/transfer (không lưu DB) — FE poll fallback/status không mất QR.
        var code = tx.OrderCode ?? orderCode;
        var presentation = await _sePayGateway.CreateUpgradeOrderAsync(new SePayCreateOrderRequest
        {
            OrderCode = code,
            Amount = tx.Amount,
            Currency = tx.Currency,
            Description = "Upgrade status rehydrate"
        });

        return new UpgradePaymentIntentDto
        {
            OrderCode = code,
            Provider = tx.Provider,
            Amount = tx.Amount,
            Currency = tx.Currency,
            Status = tx.Status,
            ExpiresAt = tx.ExpiresAt ?? DateTime.UtcNow,
            PaymentUrl = presentation.PaymentUrl,
            QrContent = presentation.QrContent,
            QrImageUrl = presentation.QrImageUrl,
            BankName = presentation.BankName ?? _sePaySettings.BankName,
            BankAccountName = presentation.BankAccountName ?? _sePaySettings.BankAccountName,
            BankAccountNumber = presentation.BankAccountNumber ?? _sePaySettings.BankAccountNumber,
            TransferContent = presentation.TransferContent ?? $"IQGS {code}"
        };
    }

    public async Task<SePayWebhookProcessResultDto> ConfirmUpgradePaymentAsync(
        SePayWebhookRequestDto webhook,
        string? rawBody = null,
        string? signatureHeader = null,
        string? timestampHeader = null)
    {
        var normalized = NormalizeWebhookPayload(webhook, rawBody);
        var orderCode = normalized.OrderCode;
        if (string.IsNullOrWhiteSpace(orderCode))
            throw new BadRequestException("Webhook thiếu mã đơn (orderCode) trong content/code.");

        var tx = await _txRepo.GetByOrderCodeAsync(orderCode);
        if (tx is null)
        {
            return new SePayWebhookProcessResultDto
            {
                Processed = false,
                PaymentAccepted = false,
                OrderCode = orderCode,
                Message = "Bỏ qua: không tìm thấy order."
            };
        }

        if (tx.Status == SubscriptionTransactionStatus.Paid)
        {
            // Idempotent webhook — vẫn push SignalR để FE (mới connect) kịp đóng modal.
            await TryNotifyPaymentPaidAsync(tx, orderCode);
            return new SePayWebhookProcessResultDto
            {
                Processed = true,
                PaymentAccepted = true,
                OrderCode = orderCode,
                Message = "Đơn đã được xác nhận trước đó."
            };
        }

        // Terminal states: không nâng gói (CK ND cũ sau khi tạo đơn mới / failed).
        if (tx.Status is SubscriptionTransactionStatus.Cancelled
            or SubscriptionTransactionStatus.Expired
            or SubscriptionTransactionStatus.Failed)
        {
            return new SePayWebhookProcessResultDto
            {
                Processed = true,
                PaymentAccepted = false,
                OrderCode = orderCode,
                Message = $"Bỏ qua: đơn đã {tx.Status}, không nhận thanh toán."
            };
        }

        if (tx.ExpiresAt.HasValue && tx.ExpiresAt.Value <= DateTime.UtcNow)
        {
            tx.Status = SubscriptionTransactionStatus.Expired;
            tx.RawPayloadJson = normalized.RawPayloadJson;
            await _txRepo.UpdateAsync(tx);
            return new SePayWebhookProcessResultDto
            {
                Processed = true,
                PaymentAccepted = false,
                OrderCode = orderCode,
                Message = "Đơn đã hết hạn."
            };
        }

        // Verify chữ ký SePay (header) trước khi đổi plan.
        var signatureOk = _sePayGateway.VerifyWebhookSignature(
            normalized.RawPayloadJson ?? string.Empty,
            timestampHeader,
            signatureHeader ?? normalized.Signature);
        if (!signatureOk)
            throw new BadRequestException("Signature webhook không hợp lệ.");

        var status = NormalizePaymentStatus(normalized.Status);

        // SePay thường không gửi currency — mặc định VND.
        var currency = string.IsNullOrWhiteSpace(normalized.Currency) ? "VND" : normalized.Currency;
        if (!string.Equals(tx.Currency, currency, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Currency webhook không khớp.");
        if (tx.Amount != normalized.Amount)
            throw new BadRequestException("Amount webhook không khớp.");

        tx.ExternalTransactionId = normalized.ExternalTransactionId ?? tx.ExternalTransactionId;
        tx.RawPayloadJson = normalized.RawPayloadJson;
        tx.Status = status;

        if (status != SubscriptionTransactionStatus.Paid)
        {
            await _txRepo.UpdateAsync(tx);
            return new SePayWebhookProcessResultDto
            {
                Processed = true,
                PaymentAccepted = false,
                OrderCode = orderCode,
                Message = "Đã ghi nhận webhook nhưng chưa thanh toán thành công."
            };
        }

        tx.ConfirmedAt = normalized.PaidAt ?? DateTime.UtcNow;
        await _txRepo.UpdateAsync(tx);
        var upgradedSub = await ApplyPremiumByTransactionAsync(tx);
        // SCRUM-387: push realtime → FE đóng QR modal ngay, không chờ poll
        await TryNotifyPaymentPaidAsync(tx, orderCode);
        // Chúc mừng nâng cấp Premium + hóa đơn — best-effort, không làm fail webhook.
        await TrySendPremiumEmailsAsync(upgradedSub, tx);

        return new SePayWebhookProcessResultDto
        {
            Processed = true,
            PaymentAccepted = true,
            OrderCode = orderCode,
            Message = "Thanh toán thành công, đã nâng Premium."
        };
    }

    /// <summary>Đẩy PaymentPaid tới user sở hữu đơn; lỗi push không làm fail webhook.</summary>
    private async Task TryNotifyPaymentPaidAsync(SubscriptionTransaction tx, string orderCode)
    {
        try
        {
            var sub = await _subscriptionRepo.GetByIdWithPlanAsync(tx.SubscriptionId);
            if (sub is null) return;

            await _paymentNotifier.NotifyPaymentPaidAsync(
                sub.UserId,
                orderCode,
                tx.Amount,
                tx.Currency);
        }
        catch
        {
            // Không ném — webhook 200 vẫn quan trọng với SePay.
        }
    }

    /// <summary>Gửi email chúc mừng nâng cấp Premium + hóa đơn thanh toán; lỗi gửi mail không làm fail webhook.</summary>
    private async Task TrySendPremiumEmailsAsync(Subscription sub, SubscriptionTransaction tx)
    {
        try
        {
            var user = await _userRepo.GetByIdAsync(sub.UserId);
            if (user is null) return;

            var planName = sub.Plan?.Name ?? "Premium";
            var frontendUrl = (_config["AppSettings:FrontendUrl"] ?? "https://iqgs.com").TrimEnd('/');
            var appLink = $"{frontendUrl}/subscription";

            await _emailService.SendPremiumActivatedEmailAsync(
                user.Email, user.FullName, planName,
                sub.CurrentPeriodStart, sub.CurrentPeriodEnd, appLink);

            await _emailService.SendSubscriptionInvoiceEmailAsync(
                user.Email, user.FullName,
                tx.OrderCode ?? tx.Id.ToString(),
                planName, tx.Amount, tx.Currency,
                tx.ConfirmedAt ?? DateTime.UtcNow,
                sub.CurrentPeriodStart, sub.CurrentPeriodEnd, appLink);
        }
        catch
        {
            // Không ném — webhook 200 vẫn quan trọng với SePay.
        }
    }

    public async Task<MySubscriptionDto> CancelAsync(Guid userId)
    {
        var sub = await _metering.GetOrThrowSubscriptionAsync(userId);
        var plan = sub.Plan ?? await _planRepo.GetByIdAsync(sub.PlanId)
            ?? throw new NotFoundException("Không tìm thấy gói.");

        var isPremium = plan.Code is SubscriptionPlanCodes.HrPremium or SubscriptionPlanCodes.CandidatePremium;
        if (!isPremium)
            return await BuildMyDtoAsync(sub);

        // SCRUM-407: giữ Premium đến hết kỳ — không hạ Free giữa kỳ.
        sub.CancelAtPeriodEnd = true;
        sub.CancelledAt = DateTime.UtcNow;
        sub.Status = SubscriptionStatus.Active;
        await _subscriptionRepo.UpdateAsync(sub);

        await _txRepo.AddAsync(new SubscriptionTransaction
        {
            SubscriptionId = sub.Id,
            Type = SubscriptionTransactionTypes.Cancel,
            Amount = 0,
            Currency = plan.Currency,
            Status = "Paid",
            Provider = "Sandbox",
            Note = $"Cancel at period end → {plan.Code} đến {sub.CurrentPeriodEnd:O}"
        });

        return await BuildMyDtoAsync(sub);
    }

    public async Task<MySubscriptionDto> PurchaseAskAiPackAsync(Guid userId, int extraRequests, decimal amount)
    {
        var sub = await _metering.GetOrThrowSubscriptionAsync(userId);
        var limits = SubscriptionLimitsHelper.Deserialize(sub.LimitsSnapshotJson);
        if (limits.AskAiPerMonth <= 0)
            throw new SubscriptionGateException(
                SubscriptionErrorCodes.FeatureRequiresPremium,
                "Mua pack Ask-AI chỉ dành cho gói Premium.");

        await _metering.AddAskAiPackAsync(userId, extraRequests);

        await _txRepo.AddAsync(new SubscriptionTransaction
        {
            SubscriptionId = sub.Id,
            Type = SubscriptionTransactionTypes.AskAiPack,
            Amount = amount,
            Currency = "VND",
            Status = "Paid",
            Provider = "Sandbox",
            Note = $"+{extraRequests} Ask-AI requests"
        });

        return await GetMySubscriptionAsync(userId);
    }

    public async Task<List<UsageCounterDto>> GetMyUsageAsync(Guid userId)
    {
        var sub = await _metering.GetOrThrowSubscriptionAsync(userId);
        var rows = await _usageRepo.ListBySubscriptionPeriodAsync(sub.Id, sub.CurrentPeriodStart);
        return rows.Select(r => new UsageCounterDto
        {
            UsageType = r.UsageType,
            ScopeKey = r.ScopeKey,
            UsedCount = r.UsedCount,
            ExtraFromPack = r.ExtraFromPack,
            PeriodStart = r.PeriodStart
        }).ToList();
    }

    private async Task<MySubscriptionDto> BuildMyDtoAsync(Subscription sub)
    {
        var plan = sub.Plan ?? await _planRepo.GetByIdAsync(sub.PlanId)
            ?? throw new NotFoundException("Không tìm thấy gói.");
        var limits = SubscriptionLimitsHelper.Deserialize(sub.LimitsSnapshotJson);
        var askAi = await _metering.GetUsageAsync(sub.UserId, UsageType.HrAskAi);
        var generate = await _metering.GetUsageAsync(sub.UserId, UsageType.HrGenerateSet);

        return new MySubscriptionDto
        {
            PlanCode = plan.Code,
            PlanName = plan.Name,
            Audience = plan.Audience,
            Status = sub.Status,
            PriceMonthly = plan.PriceMonthly,
            Currency = plan.Currency,
            PeriodStart = sub.CurrentPeriodStart,
            PeriodEnd = sub.CurrentPeriodEnd,
            CancelAtPeriodEnd = sub.CancelAtPeriodEnd,
            LastSuccessfulGenerateAt = sub.LastSuccessfulGenerateAt,
            Limits = limits,
            AskAiUsed = askAi.UsedCount,
            AskAiLimit = askAi.EffectiveLimit,
            GenerateSetUsed = generate.UsedCount,
            Entitlements = new SubscriptionEntitlementsDto
            {
                CanExport = limits.CanExport,
                CanAskAi = limits.AskAiPerMonth > 0,
                GenerateUnlimited = limits.GenerateUnlimited,
                FreeVisiblePercent = limits.FreeVisiblePercent,
                CanPersistHrRecommendation = limits.CanPersistHrRecommendation
            }
        };
    }

    private static SubscriptionPlanDto ToPlanDto(SubscriptionPlan p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        Audience = p.Audience,
        Name = p.Name,
        PriceMonthly = p.PriceMonthly,
        Currency = p.Currency,
        IsActive = p.IsActive,
        Limits = SubscriptionLimitsHelper.Deserialize(p.LimitsJson),
        AppliesToExistingSubscribersFromNextPeriod = true
    };

    private static string NormalizeAudience(string audience)
    {
        if (string.Equals(audience, SubscriptionAudience.HR, StringComparison.OrdinalIgnoreCase))
            return SubscriptionAudience.HR;
        if (string.Equals(audience, SubscriptionAudience.Candidate, StringComparison.OrdinalIgnoreCase))
            return SubscriptionAudience.Candidate;
        throw new BadRequestException("audience phải là HR hoặc Candidate.");
    }

    private async Task<Subscription> ApplyPremiumByTransactionAsync(SubscriptionTransaction tx)
    {
        var sub = await _subscriptionRepo.GetByIdWithPlanAsync(tx.SubscriptionId)
            ?? throw new NotFoundException("Không tìm thấy subscription.");
        var currentPlan = sub.Plan ?? await _planRepo.GetByIdAsync(sub.PlanId)
            ?? throw new NotFoundException("Không tìm thấy gói hiện tại.");
        var audience = currentPlan.Audience;

        var premiumCode = audience == SubscriptionAudience.HR
            ? SubscriptionPlanCodes.HrPremium
            : SubscriptionPlanCodes.CandidatePremium;
        var premium = await _planRepo.GetByCodeAsync(premiumCode)
            ?? throw new ServerFailureException($"Chưa seed plan {premiumCode}.");

        // SCRUM-386: Premium có thời hạn 1 tháng kể từ lúc thanh toán; hết CurrentPeriodEnd → Free.
        var now = DateTime.UtcNow;
        await _usageRepo.DeleteBySubscriptionPeriodAsync(sub.Id, sub.CurrentPeriodStart);

        sub.PlanId = premium.Id;
        sub.Plan = premium;
        sub.Status = SubscriptionStatus.Active;
        sub.CancelledAt = null;
        sub.CancelAtPeriodEnd = false;
        sub.LimitsSnapshotJson = premium.LimitsJson;
        sub.CurrentPeriodStart = now;
        sub.CurrentPeriodEnd = now.AddMonths(1);
        sub.UpdatedAt = now;
        await _subscriptionRepo.UpdateAsync(sub);
        return sub;
    }

    private static string BuildOrderCode(string audience)
    {
        var prefix = audience == SubscriptionAudience.HR ? "HR" : "CA";
        // Không dùng dấu '-' — SePay/VietQR/ngân hàng thường strip ký tự đặc biệt trong ND CK.
        // Ví dụ: UPGCA20260730102113E398B24A
        return $"UPG{prefix}{DateTime.UtcNow:yyyyMMddHHmmss}{Guid.NewGuid():N}"[..28].ToUpperInvariant();
    }

    private static string NormalizePaymentStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return SubscriptionTransactionStatus.Pending;

        return status.Trim().ToUpperInvariant() switch
        {
            "PAID" or "SUCCESS" or "COMPLETED" => SubscriptionTransactionStatus.Paid,
            "FAILED" or "REJECTED" => SubscriptionTransactionStatus.Failed,
            "EXPIRED" => SubscriptionTransactionStatus.Expired,
            _ => SubscriptionTransactionStatus.Pending
        };
    }

    private static UpgradePaymentIntentDto ToUpgradeIntentDto(SePayCreateOrderResult order)
        => new()
        {
            OrderCode = order.OrderCode,
            Provider = order.Provider,
            Amount = order.Amount,
            Currency = order.Currency,
            Status = SubscriptionTransactionStatus.Pending,
            ExpiresAt = order.ExpiresAt,
            PaymentUrl = order.PaymentUrl,
            QrContent = order.QrContent,
            QrImageUrl = order.QrImageUrl,
            BankName = order.BankName,
            BankAccountName = order.BankAccountName,
            BankAccountNumber = order.BankAccountNumber,
            TransferContent = order.TransferContent
        };

    private static NormalizedWebhookPayload NormalizeWebhookPayload(
        SePayWebhookRequestDto webhook,
        string? rawBody = null)
    {
        // Ưu tiên parse UPG* từ content/description (ND CK). Field `code` của SePay
        // thường là mã thanh toán nội bộ, không phải orderCode IQGS.
        var orderCode = ResolveOrderCode(webhook);
        var amount = webhook.TransferAmount ?? webhook.Amount;

        // SePay webhook "tiền vào" đôi khi không gửi transferType — nếu có số tiền > 0 coi như Paid.
        string status;
        if (!string.IsNullOrWhiteSpace(webhook.Status))
        {
            status = webhook.Status!;
        }
        else if (string.Equals(webhook.TransferType, "in", StringComparison.OrdinalIgnoreCase)
                 || (string.IsNullOrWhiteSpace(webhook.TransferType) && amount > 0))
        {
            status = "PAID";
        }
        else
        {
            status = "FAILED";
        }

        var externalId = webhook.ExternalTransactionId
            ?? webhook.ReferenceCode
            ?? (webhook.Id.HasValue ? webhook.Id.Value.ToString() : null);
        var paidAt = webhook.PaidAt ?? ParseDateTime(webhook.TransactionDate);
        var rawPayload = !string.IsNullOrWhiteSpace(rawBody)
            ? rawBody
            : !string.IsNullOrWhiteSpace(webhook.RawPayloadJson)
                ? webhook.RawPayloadJson
                : JsonSerializer.Serialize(webhook);

        return new NormalizedWebhookPayload
        {
            OrderCode = orderCode,
            Amount = amount,
            Currency = string.IsNullOrWhiteSpace(webhook.Currency) ? "VND" : webhook.Currency,
            Status = status,
            ExternalTransactionId = externalId,
            PaidAt = paidAt,
            Signature = webhook.Signature,
            RawPayloadJson = rawPayload
        };
    }

    private static string ResolveOrderCode(SePayWebhookRequestDto webhook)
    {
        // SePay log thực tế dùng `description`; docs dùng `content`.
        var textBlob = string.Join(" ",
            new[] { webhook.Content, webhook.Description }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var fromText = ExtractOrderCodeFromContent(textBlob);
        if (!string.IsNullOrWhiteSpace(fromText)) return fromText;

        if (LooksLikeIqgsOrderCode(webhook.OrderCode)) return webhook.OrderCode!.Trim();
        if (LooksLikeIqgsOrderCode(webhook.Code)) return webhook.Code!.Trim();

        return (webhook.OrderCode ?? webhook.Code ?? string.Empty).Trim();
    }

    private static bool LooksLikeIqgsOrderCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var key = value.Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
        return key.StartsWith("UPGHR", StringComparison.Ordinal) || key.StartsWith("UPGCA", StringComparison.Ordinal);
    }

    private static string? ExtractOrderCodeFromContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        // Format mới (không dấu '-'): UPGCA20260730102539FE5BD442D
        // Quan trọng: KHÔNG nuốt "-CHUYEN" từ chuỗi bank "....FE5BD442D-CHUYEN TIEN".
        var compact = Regex.Match(
            content,
            @"UPG(?:HR|CA)\d{14}[A-Fa-f0-9]{6,20}",
            RegexOptions.IgnoreCase);
        if (compact.Success) return compact.Value;

        // Format cũ có dấu '-': UPG-CA-20260730102113-e398b24ab52840
        var dashed = Regex.Match(
            content,
            @"UPG-(?:HR|CA)-\d{14}-[A-Fa-f0-9]{6,32}",
            RegexOptions.IgnoreCase);
        return dashed.Success ? dashed.Value : null;
    }

    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, out var parsed))
            return DateTime.SpecifyKind(parsed, DateTimeKind.Local).ToUniversalTime();
        return null;
    }

    private sealed class NormalizedWebhookPayload
    {
        public string OrderCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string Status { get; set; } = "Pending";
        public string? ExternalTransactionId { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? Signature { get; set; }
        public string? RawPayloadJson { get; set; }
    }
}
