using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Xunit;

namespace ApplicationLayer.UnitTests.Subscription;

public sealed class SubscriptionGateServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task CheckCoach_PremiumPlan_StaleFreeSnapshot_DoesNotThrow()
    {
        var gate = CreateGate(planCode: SubscriptionPlanCodes.CandidatePremium, freeSnapshot: true);

        var ex = await Record.ExceptionAsync(() => gate.CheckCoachGenerationAsync(UserId));

        Assert.Null(ex);
    }

    [Fact]
    public async Task CheckCoach_FreePlan_BothFlagsFalse_ThrowsFeatureRequiresPremium()
    {
        var gate = CreateGate(planCode: SubscriptionPlanCodes.CandidateFree, freeSnapshot: true);

        var ex = await Assert.ThrowsAsync<SubscriptionGateException>(() => gate.CheckCoachGenerationAsync(UserId));

        Assert.Equal(SubscriptionErrorCodes.FeatureRequiresPremium, ex.ErrorCode);
        Assert.Contains("AI Coach", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckCoach_ExpiredPremiumAsFree_ThrowsFeatureRequiresPremium()
    {
        // GetOrThrow đã hạ Free trước khi vào gate — mock Plan.Code = CANDIDATE_FREE.
        var gate = CreateGate(planCode: SubscriptionPlanCodes.CandidateFree, freeSnapshot: true);

        var ex = await Assert.ThrowsAsync<SubscriptionGateException>(() => gate.CheckCoachGenerationAsync(UserId));

        Assert.Equal(SubscriptionErrorCodes.FeatureRequiresPremium, ex.ErrorCode);
    }

    [Fact]
    public async Task CheckPersonalSet_PremiumPlan_StaleFreeSnapshot_DoesNotThrowPremiumError()
    {
        var gate = CreateGate(planCode: SubscriptionPlanCodes.CandidatePremium, freeSnapshot: true, usedCount: 0);

        var ex = await Record.ExceptionAsync(() => gate.CheckGeneratePersonalSetAsync(UserId));

        Assert.Null(ex);
    }

    private static SubscriptionGateService CreateGate(string planCode, bool freeSnapshot, int usedCount = 0)
    {
        var limits = freeSnapshot
            ? SubscriptionPlanLimits.CandidateFree()
            : SubscriptionPlanLimits.CandidatePremium();

        var sub = new DomainLayer.Entities.Subscription
        {
            UserId = UserId,
            Status = SubscriptionStatus.Active,
            LimitsSnapshotJson = SubscriptionLimitsHelper.Serialize(limits),
            Plan = new SubscriptionPlan
            {
                Code = planCode,
                Audience = SubscriptionAudience.Candidate,
                Name = planCode
            }
        };

        return new SubscriptionGateService(new FakeUsageMeteringService(sub, usedCount));
    }

    private sealed class FakeUsageMeteringService : IUsageMeteringService
    {
        private readonly DomainLayer.Entities.Subscription _sub;
        private readonly int _usedCount;

        public FakeUsageMeteringService(DomainLayer.Entities.Subscription sub, int usedCount)
        {
            _sub = sub;
            _usedCount = usedCount;
        }

        public Task<DomainLayer.Entities.Subscription> GetOrThrowSubscriptionAsync(Guid userId)
            => Task.FromResult(_sub);

        public Task<DomainLayer.Entities.Subscription> EnsureCurrentPeriodAsync(DomainLayer.Entities.Subscription subscription)
            => Task.FromResult(subscription);

        public Task<UsageSnapshotDto> GetUsageAsync(Guid userId, string usageType, string? scopeKey = null)
            => Task.FromResult(new UsageSnapshotDto { UsedCount = _usedCount, ExtraFromPack = 0, LimitFromSnapshot = 10 });

        public Task IncrementAsync(Guid userId, string usageType, string? scopeKey = null)
            => Task.CompletedTask;

        public Task AddAskAiPackAsync(Guid userId, int extraRequests)
            => Task.CompletedTask;

        public Task MarkGenerateSuccessAsync(Guid userId)
            => Task.CompletedTask;
    }
}
