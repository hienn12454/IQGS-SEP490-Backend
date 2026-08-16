using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public interface ISubscriptionGateService
{
    Task<SubscriptionPlanLimits> GetLimitsAsync(Guid userId);
    Task CheckGenerateSetAsync(Guid userId);
    Task CheckPlanRegenerateAsync(Guid userId, string draftId);
    Task CheckAskAiAsync(Guid userId);
    Task CheckExportAsync(Guid userId);
    Task CheckFeedbackAsync(Guid userId, int questionIndexZeroBased, int totalQuestions);
    Task<bool> CanPersistHrRecommendationAsync(Guid userId);
    Task<int> GetVisibleQuestionCountAsync(Guid userId, int totalQuestions);
    /// <summary>Premium: full AI feedback; Free: chỉ teaser.</summary>
    Task<bool> CanDetailedAiFeedbackAsync(Guid userId);
    Task<int> GetFreeTeaserFeedbackCountAsync(Guid userId);
    Task CheckGeneratePersonalSetAsync(Guid userId);
    /// <summary>AI Coach (diagnostic/drill): chỉ cần Premium, không trừ hạn mức bộ JD.</summary>
    Task CheckCoachGenerationAsync(Guid userId);
}

public class SubscriptionGateService : ISubscriptionGateService
{
    private readonly IUsageMeteringService _metering;

    public SubscriptionGateService(IUsageMeteringService metering)
    {
        _metering = metering;
    }

    public async Task<SubscriptionPlanLimits> GetLimitsAsync(Guid userId)
    {
        var sub = await _metering.GetOrThrowSubscriptionAsync(userId);
        return SubscriptionLimitsHelper.Deserialize(sub.LimitsSnapshotJson);
    }

    public async Task CheckGenerateSetAsync(Guid userId)
    {
        var sub = await _metering.GetOrThrowSubscriptionAsync(userId);
        var limits = SubscriptionLimitsHelper.Deserialize(sub.LimitsSnapshotJson);

        if (limits.GenerateUnlimited)
            return;

        var hours = Math.Max(1, limits.GenerateCooldownHours);
        if (sub.LastSuccessfulGenerateAt.HasValue)
        {
            var nextAllowed = sub.LastSuccessfulGenerateAt.Value.AddHours(hours);
            if (DateTime.UtcNow < nextAllowed)
            {
                var remain = nextAllowed - DateTime.UtcNow;
                throw new SubscriptionGateException(
                    SubscriptionErrorCodes.CooldownActive,
                    $"Gói Free chỉ tạo bộ 1 lần / {hours} giờ. Thử lại sau {FormatRemain(remain)} hoặc nâng Premium.");
            }
        }
    }

    public async Task CheckPlanRegenerateAsync(Guid userId, string draftId)
    {
        if (string.IsNullOrWhiteSpace(draftId))
            throw new BadRequestException("draftId không được để trống.");

        var limits = await GetLimitsAsync(userId);
        var max = Math.Max(0, limits.PlanRegeneratePerDraft);
        var usage = await _metering.GetUsageAsync(userId, UsageType.HrPlanRegenerate, draftId.Trim());

        if (usage.UsedCount >= max)
        {
            throw new SubscriptionGateException(
                SubscriptionErrorCodes.PlanRegenerateLimit,
                $"Đã hết {max} lần tạo lại plan trong phiên này. Hãy mở phiên Studio mới.");
        }
    }

    public async Task CheckAskAiAsync(Guid userId)
    {
        var limits = await GetLimitsAsync(userId);
        if (limits.AskAiPerMonth <= 0 && !(await IsPremiumAskAiEligible(limits)))
        {
            throw new SubscriptionGateException(
                SubscriptionErrorCodes.FeatureRequiresPremium,
                "Hỏi AI từng câu chỉ dành cho gói Premium.");
        }

        if (limits.AskAiPerMonth <= 0)
        {
            throw new SubscriptionGateException(
                SubscriptionErrorCodes.FeatureRequiresPremium,
                "Hỏi AI từng câu chỉ dành cho gói Premium.");
        }

        var usage = await _metering.GetUsageAsync(userId, UsageType.HrAskAi);
        if (usage.UsedCount >= usage.EffectiveLimit)
        {
            throw new SubscriptionGateException(
                SubscriptionErrorCodes.QuotaExceeded,
                $"Đã hết {usage.EffectiveLimit} request Ask-AI trong kỳ này. Mua pack hoặc chờ kỳ tiếp theo.");
        }
    }

    private static Task<bool> IsPremiumAskAiEligible(SubscriptionPlanLimits limits)
        => Task.FromResult(limits.AskAiPerMonth > 0);

    public async Task CheckExportAsync(Guid userId)
    {
        var limits = await GetLimitsAsync(userId);
        if (!limits.CanExport)
        {
            throw new SubscriptionGateException(
                SubscriptionErrorCodes.FeatureRequiresPremium,
                "Xuất bộ câu hỏi chỉ dành cho gói Premium.");
        }
    }

    /// <summary>
    /// Legacy no-op cho Teaser Freemium — Free được làm/lưu mọi câu;
    /// độ sâu AI feedback quyết định lúc complete (CanDetailedAiFeedback).
    /// </summary>
    public Task CheckFeedbackAsync(Guid userId, int questionIndexZeroBased, int totalQuestions)
        => Task.CompletedTask;

    public async Task<bool> CanPersistHrRecommendationAsync(Guid userId)
    {
        var limits = await GetLimitsAsync(userId);
        return limits.CanPersistHrRecommendation;
    }

    /// <summary>Teaser Freemium: luôn mở full câu hỏi (không khóa giữa phiên).</summary>
    public Task<int> GetVisibleQuestionCountAsync(Guid userId, int totalQuestions)
        => Task.FromResult(Math.Max(0, totalQuestions));

    public async Task<bool> CanDetailedAiFeedbackAsync(Guid userId)
    {
        var limits = await GetLimitsAsync(userId);
        if (limits.CanDetailedAiFeedback)
            return true;

        // Snapshot cũ (trước Teaser Freemium): Premium Candidate có CanPersistHrRecommendation
        if (limits.CanPersistHrRecommendation)
            return true;

        return false;
    }

    public async Task<int> GetFreeTeaserFeedbackCountAsync(Guid userId)
    {
        var limits = await GetLimitsAsync(userId);
        if (limits.CanDetailedAiFeedback)
            return 0;
        return Math.Max(1, limits.FreeTeaserFeedbackCount);
    }

    public async Task CheckGeneratePersonalSetAsync(Guid userId)
    {
        var limits = await GetLimitsAsync(userId);
        if (!limits.CanGeneratePersonalSet)
        {
            throw new SubscriptionGateException(
                SubscriptionErrorCodes.FeatureRequiresPremium,
                "Sinh bộ luyện tập từ CV + JD chỉ dành cho gói Premium.");
        }

        var max = Math.Max(0, limits.PersonalSetPerMonth);
        if (max <= 0)
        {
            throw new SubscriptionGateException(
                SubscriptionErrorCodes.FeatureRequiresPremium,
                "Sinh bộ luyện tập từ CV + JD chỉ dành cho gói Premium.");
        }

        var usage = await _metering.GetUsageAsync(userId, UsageType.CandidatePersonalSet);
        if (usage.UsedCount >= max)
        {
            throw new SubscriptionGateException(
                SubscriptionErrorCodes.QuotaExceeded,
                $"Đã hết {max} bộ luyện tập từ JD trong kỳ này. Thử lại kỳ tới hoặc liên hệ hỗ trợ.");
        }
    }

    public async Task CheckCoachGenerationAsync(Guid userId)
    {
        var limits = await GetLimitsAsync(userId);
        if (!limits.CanGeneratePersonalSet && !limits.CanDetailedAiFeedback)
        {
            throw new SubscriptionGateException(
                SubscriptionErrorCodes.FeatureRequiresPremium,
                "AI Coach (kiểm tra CV / luyện skill) dành cho gói Premium.");
        }
    }

    private static string FormatRemain(TimeSpan remain)
    {
        if (remain.TotalHours >= 1)
            return $"{(int)Math.Ceiling(remain.TotalHours)} giờ";
        return $"{Math.Max(1, (int)Math.Ceiling(remain.TotalMinutes))} phút";
    }
}
