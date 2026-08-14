using DomainLayer.Entities;

namespace ApplicationLayer.DTOs.Subscription;

public class SubscriptionPlanDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal PriceMonthly { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; }
    public SubscriptionPlanLimits Limits { get; set; } = new();

    /// <summary>Admin đổi limit: chỉ áp dụng từ kỳ sau cho subscriber hiện có.</summary>
    public bool AppliesToExistingSubscribersFromNextPeriod { get; set; } = true;
}

public class SubscriptionEntitlementsDto
{
    public bool CanExport { get; set; }
    public bool CanAskAi { get; set; }
    public bool GenerateUnlimited { get; set; }
    public int FreeVisiblePercent { get; set; }
    public bool CanPersistHrRecommendation { get; set; }
}

public class MySubscriptionDto
{
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal PriceMonthly { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    /// <summary>true nếu đã hủy gia hạn — Premium còn hiệu lực đến PeriodEnd.</summary>
    public bool CancelAtPeriodEnd { get; set; }
    public DateTime? LastSuccessfulGenerateAt { get; set; }
    public SubscriptionPlanLimits Limits { get; set; } = new();
    public int AskAiUsed { get; set; }
    public int AskAiLimit { get; set; }
    public int GenerateSetUsed { get; set; }
    public SubscriptionEntitlementsDto Entitlements { get; set; } = new();
}

public class UsageCounterDto
{
    public string UsageType { get; set; } = string.Empty;
    public string? ScopeKey { get; set; } = string.Empty;
    public int UsedCount { get; set; }
    public int ExtraFromPack { get; set; }
    public DateTime PeriodStart { get; set; }
}

public class UpgradeSubscriptionRequestDto
{
    /// <summary>SePay flow: FE có thể truyền nguồn gọi để trace (optional).</summary>
    public string? Source { get; set; }
}

public class UpgradePaymentIntentDto
{
    public string OrderCode { get; set; } = string.Empty;
    public string Provider { get; set; } = "SePay";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = "Pending";
    public DateTime ExpiresAt { get; set; }
    public string? PaymentUrl { get; set; }
    public string? QrContent { get; set; }
    public string? QrImageUrl { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? TransferContent { get; set; }
}

public class SePayWebhookRequestDto
{
    /// <summary>ID webhook/event từ SePay.</summary>
    public long? Id { get; set; }
    public string? Gateway { get; set; }
    public string? TransactionDate { get; set; }
    public string? AccountNumber { get; set; }
    public string? Code { get; set; }
    public string? Content { get; set; }
    /// <summary>Một số webhook SePay gửi ND CK ở field description thay vì content.</summary>
    public string? Description { get; set; }
    public string? TransferType { get; set; }
    public decimal? TransferAmount { get; set; }
    public string? ReferenceCode { get; set; }

    /// <summary>Optional signature nếu merchant tự cấu hình thêm lớp bảo vệ.</summary>
    public string? Signature { get; set; }

    // Backward-compatible fields
    public string? OrderCode { get; set; }
    public string? ExternalTransactionId { get; set; }
    public string? Status { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime? PaidAt { get; set; }
    public string? RawPayloadJson { get; set; }
}

public class SePayWebhookProcessResultDto
{
    public bool Processed { get; set; }
    public bool PaymentAccepted { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class PurchaseAskAiPackRequestDto
{
    public int ExtraRequests { get; set; } = 200;
    public decimal Amount { get; set; } = 99000;
}

public class UpdateSubscriptionPlanDto
{
    public string? Name { get; set; }
    public decimal? PriceMonthly { get; set; }
    public string? Currency { get; set; }
    public bool? IsActive { get; set; }
    public SubscriptionPlanLimits? Limits { get; set; }
}

public class CreateSubscriptionPlanDto
{
    public string Code { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal PriceMonthly { get; set; }
    public string Currency { get; set; } = "VND";
    public SubscriptionPlanLimits Limits { get; set; } = new();
}

public class AdminUsageSummaryDto
{
    public Guid UserId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public List<UsageCounterDto> Usage { get; set; } = new();
    public string Note { get; set; } = "Limit Plan template chỉ áp dụng từ kỳ sau cho subscriber hiện có.";
}

public class AdminUsageAggregateItemDto
{
    public string UsageType { get; set; } = string.Empty;
    public int TotalUsed { get; set; }
    public int UserCount { get; set; }
}

/// <summary>KPI subscription / SePay Paid từ DB nội bộ (SCRUM-385).</summary>
public class AdminSubscriptionStatsDto
{
    public decimal RevenueThisMonth { get; set; }
    public string Currency { get; set; } = "VND";
    public int PendingOrders { get; set; }
    public int PremiumHrActive { get; set; }
    public int PremiumCandidateActive { get; set; }
    public decimal EstimatedMrr { get; set; }
    public double PaidRateLast30Days { get; set; }
    public int PaidLast30Days { get; set; }
    public int ClosedLast30Days { get; set; }
}

public class AdminSubscriptionTransactionRowDto
{
    public Guid Id { get; set; }
    public string? OrderCode { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string? Audience { get; set; }
    public string? PlanCode { get; set; }
    public string? UserEmail { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class AdminSePayBankCardDto
{
    public bool Available { get; set; }
    public string? BankName { get; set; }
    public string? AccountHolderName { get; set; }
    public string? AccountNumberMasked { get; set; }
    public decimal Accumulated { get; set; }
    public string? LastTransaction { get; set; }
    public bool Active { get; set; }
    public string? Message { get; set; }
}

public class AdminSePayLiveTxRowDto
{
    public string Id { get; set; } = string.Empty;
    public string? TransactionDate { get; set; }
    public decimal AmountIn { get; set; }
    public string? TransactionContent { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? BankBrandName { get; set; }
    public int? WebhookSuccess { get; set; }
    /// <summary>Matched | Unmatched | PendingLocal | Unknown</summary>
    public string MatchStatus { get; set; } = "Unknown";
    public string? MatchedOrderCode { get; set; }
}

public class AdminSePayDailyInDto
{
    public string Date { get; set; } = string.Empty;
    public decimal AmountIn { get; set; }
}

public class AdminSePayOverviewDto
{
    public bool Available { get; set; }
    public string? Message { get; set; }
    public AdminSePayBankCardDto Bank { get; set; } = new();
    public List<AdminSePayLiveTxRowDto> RecentInflows { get; set; } = new();
    public List<AdminSePayDailyInDto> DailyInLast7Days { get; set; } = new();
    public DateTime? CachedAt { get; set; }
}

/// <summary>Admin gán Premium — SCRUM-386. periodMonths whitelist 1/3/6/12.</summary>
public class AdminGrantPremiumDto
{
    public int PeriodMonths { get; set; } = 1;
    public string? Note { get; set; }
}

/// <summary>Admin gia hạn / reset kỳ Premium đang active.</summary>
public class AdminUpdateUserSubscriptionDto
{
    public int PeriodMonths { get; set; } = 1;
    public string? Note { get; set; }
}

/// <summary>Admin thu hồi Premium → Free.</summary>
public class AdminRevokePremiumDto
{
    public string? Note { get; set; }
}

/// <summary>Một dòng lịch sử giao dịch subscription của user (SCRUM-386).</summary>
public class AdminUserSubscriptionHistoryItemDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string? OrderCode { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}

/// <summary>Subscription hiện tại + lịch sử đăng ký/giao dịch.</summary>
public class AdminUserSubscriptionDetailDto
{
    public MySubscriptionDto Subscription { get; set; } = new();
    public List<AdminUserSubscriptionHistoryItemDto> History { get; set; } = new();
}
