namespace DomainLayer.Constants;

/// <summary>Audience của gói subscription.</summary>
public static class SubscriptionAudience
{
    public const string HR = "HR";
    public const string Candidate = "Candidate";
}

/// <summary>Trạng thái subscription của user.</summary>
public static class SubscriptionStatus
{
    public const string Active = "Active";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
}

/// <summary>Mã gói seed cố định.</summary>
public static class SubscriptionPlanCodes
{
    public const string HrFree = "HR_FREE";
    public const string HrPremium = "HR_PREMIUM";
    public const string CandidateFree = "CANDIDATE_FREE";
    public const string CandidatePremium = "CANDIDATE_PREMIUM";

    public static readonly Guid HrFreeId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    public static readonly Guid HrPremiumId = Guid.Parse("11111111-1111-1111-1111-111111111102");
    public static readonly Guid CandidateFreeId = Guid.Parse("11111111-1111-1111-1111-111111111103");
    public static readonly Guid CandidatePremiumId = Guid.Parse("11111111-1111-1111-1111-111111111104");
}

/// <summary>Loại usage được meter theo kỳ anniversary.</summary>
public static class UsageType
{
    public const string HrGenerateSet = "HrGenerateSet";
    public const string HrPlanRegenerate = "HrPlanRegenerate";
    public const string HrAskAi = "HrAskAi";
    public const string HrExport = "HrExport";
    public const string CandidateFeedback = "CandidateFeedback";
    public const string CandidatePersonalSet = "CandidatePersonalSet";
}

/// <summary>Mã lỗi feature gate subscription (HTTP 403).</summary>
public static class SubscriptionErrorCodes
{
    public const string QuotaExceeded = "QUOTA_EXCEEDED";
    public const string CooldownActive = "COOLDOWN_ACTIVE";
    public const string PlanRegenerateLimit = "PLAN_REGENERATE_LIMIT";
    public const string FeatureRequiresPremium = "FEATURE_REQUIRES_PREMIUM";
}

/// <summary>Loại giao dịch sandbox.</summary>
public static class SubscriptionTransactionTypes
{
    public const string Upgrade = "Upgrade";
    public const string Cancel = "Cancel";
    public const string AskAiPack = "AskAiPack";
}

/// <summary>Trạng thái giao dịch subscription.</summary>
public static class SubscriptionTransactionStatus
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Failed = "Failed";
    public const string Expired = "Expired";
    /// <summary>Hủy khi user tạo đơn mới (supersede).</summary>
    public const string Cancelled = "Cancelled";
}
