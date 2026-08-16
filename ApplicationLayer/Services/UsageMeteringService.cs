using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class UsageSnapshotDto
{
    public int UsedCount { get; set; }
    public int ExtraFromPack { get; set; }
    public int LimitFromSnapshot { get; set; }
    public int EffectiveLimit => LimitFromSnapshot + ExtraFromPack;
    public int Remaining => Math.Max(0, EffectiveLimit - UsedCount);
}

public interface IUsageMeteringService
{
    Task<Subscription> EnsureCurrentPeriodAsync(Subscription subscription);
    Task<Subscription> GetOrThrowSubscriptionAsync(Guid userId);
    Task IncrementAsync(Guid userId, string usageType, string? scopeKey = null);
    Task<UsageSnapshotDto> GetUsageAsync(Guid userId, string usageType, string? scopeKey = null);
    Task AddAskAiPackAsync(Guid userId, int extraRequests);
    Task MarkGenerateSuccessAsync(Guid userId);
}

public class UsageMeteringService : IUsageMeteringService
{
    private readonly ISubscriptionRepository _subscriptionRepo;
    private readonly ISubscriptionPlanRepository _planRepo;
    private readonly IUsageCounterRepository _usageRepo;
    private readonly IUserRepository _userRepo;

    public UsageMeteringService(
        ISubscriptionRepository subscriptionRepo,
        ISubscriptionPlanRepository planRepo,
        IUsageCounterRepository usageRepo,
        IUserRepository userRepo)
    {
        _subscriptionRepo = subscriptionRepo;
        _planRepo = planRepo;
        _usageRepo = usageRepo;
        _userRepo = userRepo;
    }

    public async Task<Subscription> GetOrThrowSubscriptionAsync(Guid userId)
    {
        var sub = await _subscriptionRepo.GetByUserIdWithPlanAsync(userId);
        if (sub is null)
        {
            await BackfillFreeSubscriptionAsync(userId);
            sub = await _subscriptionRepo.GetByUserIdWithPlanAsync(userId)
                ?? throw new NotFoundException("Không tìm thấy subscription của tài khoản.");
        }

        return await EnsureCurrentPeriodAsync(sub);
    }

    /// <summary>User đăng ký trước SCRUM-380 — tự gán Free theo RoleId.</summary>
    private async Task BackfillFreeSubscriptionAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAnyStatusAsync(userId)
            ?? throw new NotFoundException("Không tìm thấy user.");

        var freeCode = user.RoleId == UserRole.HRId
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

    /// <summary>
    /// Hết kỳ anniversary:
    /// - Premium (SePay/Admin): tự hạ Free + snapshot Free + kỳ Free mới 1 tháng (SCRUM-386).
    /// - Free: reset usage, copy limit template, trượt period +1 tháng.
    /// </summary>
    public async Task<Subscription> EnsureCurrentPeriodAsync(Subscription subscription)
    {
        var now = DateTime.UtcNow;
        var changed = false;

        while (now >= subscription.CurrentPeriodEnd)
        {
            var oldStart = subscription.CurrentPeriodStart;
            await _usageRepo.DeleteBySubscriptionPeriodAsync(subscription.Id, oldStart);

            var plan = subscription.Plan
                ?? await _planRepo.GetByIdAsync(subscription.PlanId)
                ?? throw new NotFoundException("Không tìm thấy gói subscription.");

            var isPremium = plan.Code is SubscriptionPlanCodes.HrPremium
                or SubscriptionPlanCodes.CandidatePremium;

            if (isPremium)
            {
                // Hết hạn Premium → ngưng gói, về Free theo audience
                var freeCode = plan.Audience == SubscriptionAudience.HR
                    ? SubscriptionPlanCodes.HrFree
                    : SubscriptionPlanCodes.CandidateFree;
                var free = await _planRepo.GetByCodeAsync(freeCode)
                    ?? throw new ServerFailureException($"Chưa seed plan {freeCode}.");

                subscription.PlanId = free.Id;
                subscription.Plan = free;
                subscription.LimitsSnapshotJson = free.LimitsJson;
                subscription.Status = SubscriptionStatus.Active;
                subscription.CancelledAt = now;
                subscription.CancelAtPeriodEnd = false;
                subscription.CurrentPeriodStart = subscription.CurrentPeriodEnd;
                subscription.CurrentPeriodEnd = subscription.CurrentPeriodStart.AddMonths(1);
            }
            else
            {
                subscription.LimitsSnapshotJson = plan.LimitsJson;
                subscription.CurrentPeriodStart = subscription.CurrentPeriodEnd;
                subscription.CurrentPeriodEnd = subscription.CurrentPeriodStart.AddMonths(1);
                subscription.Plan = plan;
            }

            changed = true;
        }

        if (changed)
            await _subscriptionRepo.UpdateAsync(subscription);

        return subscription;
    }

    public async Task IncrementAsync(Guid userId, string usageType, string? scopeKey = null)
    {
        var sub = await GetOrThrowSubscriptionAsync(userId);
        var counter = await _usageRepo.GetAsync(sub.Id, sub.CurrentPeriodStart, usageType, scopeKey);
        if (counter is null)
        {
            await _usageRepo.AddAsync(new UsageCounter
            {
                SubscriptionId = sub.Id,
                PeriodStart = sub.CurrentPeriodStart,
                UsageType = usageType,
                ScopeKey = scopeKey ?? string.Empty,
                UsedCount = 1,
                ExtraFromPack = 0
            });
            return;
        }

        counter.UsedCount += 1;
        await _usageRepo.UpdateAsync(counter);
    }

    public async Task<UsageSnapshotDto> GetUsageAsync(Guid userId, string usageType, string? scopeKey = null)
    {
        var sub = await GetOrThrowSubscriptionAsync(userId);
        var limits = SubscriptionLimitsHelper.Deserialize(sub.LimitsSnapshotJson);
        var counter = await _usageRepo.GetAsync(sub.Id, sub.CurrentPeriodStart, usageType, scopeKey);

        var limitFromSnapshot = usageType switch
        {
            UsageType.HrAskAi => limits.AskAiPerMonth,
            UsageType.HrPlanRegenerate => limits.PlanRegeneratePerDraft,
            UsageType.CandidatePersonalSet => limits.PersonalSetPerMonth,
            _ => 0
        };

        return new UsageSnapshotDto
        {
            UsedCount = counter?.UsedCount ?? 0,
            ExtraFromPack = counter?.ExtraFromPack ?? 0,
            LimitFromSnapshot = limitFromSnapshot
        };
    }

    public async Task AddAskAiPackAsync(Guid userId, int extraRequests)
    {
        if (extraRequests <= 0)
            throw new BadRequestException("Số request pack phải > 0.");

        var sub = await GetOrThrowSubscriptionAsync(userId);
        var counter = await _usageRepo.GetAsync(sub.Id, sub.CurrentPeriodStart, UsageType.HrAskAi, null);
        if (counter is null)
        {
            await _usageRepo.AddAsync(new UsageCounter
            {
                SubscriptionId = sub.Id,
                PeriodStart = sub.CurrentPeriodStart,
                UsageType = UsageType.HrAskAi,
                ScopeKey = string.Empty,
                UsedCount = 0,
                ExtraFromPack = extraRequests
            });
            return;
        }

        counter.ExtraFromPack += extraRequests;
        await _usageRepo.UpdateAsync(counter);
    }

    public async Task MarkGenerateSuccessAsync(Guid userId)
    {
        var sub = await GetOrThrowSubscriptionAsync(userId);
        sub.LastSuccessfulGenerateAt = DateTime.UtcNow;
        await _subscriptionRepo.UpdateAsync(sub);
        await IncrementAsync(userId, UsageType.HrGenerateSet);
    }
}
