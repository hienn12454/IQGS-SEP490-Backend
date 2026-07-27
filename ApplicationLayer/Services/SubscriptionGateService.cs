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

    public async Task CheckFeedbackAsync(Guid userId, int questionIndexZeroBased, int totalQuestions)
    {
        var limits = await GetLimitsAsync(userId);
        if (!limits.FeedbackOnlyOnVisible)
            return;

        var visible = SubscriptionLimitsHelper.GetVisibleQuestionCount(totalQuestions, limits.FreeVisiblePercent);
        if (questionIndexZeroBased < 0 || questionIndexZeroBased >= visible)
        {
            throw new SubscriptionGateException(
                SubscriptionErrorCodes.FeatureRequiresPremium,
                $"Gói Free chỉ AI Feedback cho khoảng {limits.FreeVisiblePercent}% câu đầu. Nâng Premium để mở toàn bộ.");
        }
    }

    public async Task<bool> CanPersistHrRecommendationAsync(Guid userId)
    {
        var limits = await GetLimitsAsync(userId);
        return limits.CanPersistHrRecommendation;
    }

    public async Task<int> GetVisibleQuestionCountAsync(Guid userId, int totalQuestions)
    {
        var limits = await GetLimitsAsync(userId);
        return SubscriptionLimitsHelper.GetVisibleQuestionCount(totalQuestions, limits.FreeVisiblePercent);
    }

    private static string FormatRemain(TimeSpan remain)
    {
        if (remain.TotalHours >= 1)
            return $"{(int)Math.Ceiling(remain.TotalHours)} giờ";
        return $"{Math.Max(1, (int)Math.Ceiling(remain.TotalMinutes))} phút";
    }
}
