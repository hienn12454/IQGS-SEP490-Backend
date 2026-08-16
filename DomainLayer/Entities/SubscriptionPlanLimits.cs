namespace DomainLayer.Entities;

/// <summary>
/// Schema JSON cho Plan.LimitsJson và Subscription.LimitsSnapshotJson.
/// Gate đọc snapshot kỳ hiện tại — không đọc live Plan khi user đang active.
/// </summary>
public class SubscriptionPlanLimits
{
    /// <summary>Free HR: số giờ cooldown giữa 2 lần tạo bộ (mặc định 24).</summary>
    public int GenerateCooldownHours { get; set; } = 24;

    /// <summary>Premium HR: không giới hạn số lần tạo bộ.</summary>
    public bool GenerateUnlimited { get; set; }

    /// <summary>Số lần tạo lại plan tối đa trong 1 draft/progress (cả Free & Premium).</summary>
    public int PlanRegeneratePerDraft { get; set; } = 5;

    public bool CanExport { get; set; }

    /// <summary>Số request Ask-AI / kỳ (Premium HR mặc định 1000).</summary>
    public int AskAiPerMonth { get; set; }

    public bool CanPublish { get; set; } = true;

    /// <summary>
    /// Legacy: % câu Free được mở. Teaser Freemium = 100 (làm full bài).
    /// Giữ field để snapshot cũ deserialize được.
    /// </summary>
    public int FreeVisiblePercent { get; set; } = 20;

    /// <summary>Premium Candidate: ghi data gợi ý HR.</summary>
    public bool CanPersistHrRecommendation { get; set; }

    /// <summary>
    /// Legacy: feedback chỉ trên câu visible. Teaser Freemium: Free = false (không chặn giữa phiên).
    /// </summary>
    public bool FeedbackOnlyOnVisible { get; set; } = true;

    /// <summary>
    /// Premium: true — AI chấm + feedback đầy đủ mọi câu + insight.
    /// Free: false — chỉ teaser (FreeTeaserFeedbackCount câu).
    /// </summary>
    public bool CanDetailedAiFeedback { get; set; }

    /// <summary>Số câu AI feedback mẫu cho Free khi CanDetailedAiFeedback = false.</summary>
    public int FreeTeaserFeedbackCount { get; set; } = 1;

    /// <summary>Candidate Premium: được sinh bộ luyện tập từ CV + JD.</summary>
    public bool CanGeneratePersonalSet { get; set; }

    /// <summary>Số bộ Personal được sinh mỗi kỳ subscription (0 = không được).</summary>
    public int PersonalSetPerMonth { get; set; }

    public static SubscriptionPlanLimits HrFree() => new()
    {
        GenerateCooldownHours = 24,
        GenerateUnlimited = false,
        PlanRegeneratePerDraft = 5,
        CanExport = false,
        AskAiPerMonth = 0,
        CanPublish = true,
        FreeVisiblePercent = 100,
        CanPersistHrRecommendation = false,
        FeedbackOnlyOnVisible = false,
        CanDetailedAiFeedback = true,
        FreeTeaserFeedbackCount = 0
    };

    public static SubscriptionPlanLimits HrPremium() => new()
    {
        GenerateCooldownHours = 0,
        GenerateUnlimited = true,
        PlanRegeneratePerDraft = 5,
        CanExport = true,
        AskAiPerMonth = 1000,
        CanPublish = true,
        FreeVisiblePercent = 100,
        CanPersistHrRecommendation = false,
        FeedbackOnlyOnVisible = false,
        CanDetailedAiFeedback = true,
        FreeTeaserFeedbackCount = 0
    };

    public static SubscriptionPlanLimits CandidateFree() => new()
    {
        GenerateCooldownHours = 0,
        GenerateUnlimited = false,
        PlanRegeneratePerDraft = 0,
        CanExport = false,
        AskAiPerMonth = 0,
        CanPublish = false,
        FreeVisiblePercent = 100,
        CanPersistHrRecommendation = false,
        FeedbackOnlyOnVisible = false,
        CanDetailedAiFeedback = false,
        FreeTeaserFeedbackCount = 1,
        CanGeneratePersonalSet = false,
        PersonalSetPerMonth = 0
    };

    public static SubscriptionPlanLimits CandidatePremium() => new()
    {
        GenerateCooldownHours = 0,
        GenerateUnlimited = false,
        PlanRegeneratePerDraft = 0,
        CanExport = false,
        AskAiPerMonth = 0,
        CanPublish = false,
        FreeVisiblePercent = 100,
        CanPersistHrRecommendation = true,
        FeedbackOnlyOnVisible = false,
        CanDetailedAiFeedback = true,
        FreeTeaserFeedbackCount = 0,
        CanGeneratePersonalSet = true,
        PersonalSetPerMonth = 10
    };
}
