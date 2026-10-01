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

    [Fact]
    public async Task CheckStartPractice_Free_WhenUsedBelowLimit_DoesNotThrow()
    {
        var gate = CreateGate(planCode: SubscriptionPlanCodes.CandidateFree, freeSnapshot: true, usedCount: 4);

        var ex = await Record.ExceptionAsync(() => gate.CheckStartPracticeAsync(UserId));

        Assert.Null(ex);
    }

    [Fact]
    public async Task CheckStartPractice_Free_WhenUsedAtLimit_ThrowsQuotaExceeded()
    {
        var gate = CreateGate(planCode: SubscriptionPlanCodes.CandidateFree, freeSnapshot: true, usedCount: 5);

        var ex = await Assert.ThrowsAsync<SubscriptionGateException>(() => gate.CheckStartPracticeAsync(UserId));

        Assert.Equal(SubscriptionErrorCodes.QuotaExceeded, ex.ErrorCode);
        Assert.Contains("lượt luyện tập", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckStartPractice_Premium_Unlimited_DoesNotThrow()
    {
        var gate = CreateGate(planCode: SubscriptionPlanCodes.CandidatePremium, freeSnapshot: false, usedCount: 100);

        var ex = await Record.ExceptionAsync(() => gate.CheckStartPracticeAsync(UserId));

        Assert.Null(ex);
    }

    [Fact]
    public async Task ShouldGrantFullAi_Free_FirstSession_ReturnsTrue()
    {
        var gate = CreateGate(planCode: SubscriptionPlanCodes.CandidateFree, freeSnapshot: true, usedCount: 0);

        Assert.True(await gate.ShouldGrantFullAiFeedbackAsync(UserId));
    }

    [Fact]
    public async Task ShouldGrantFullAi_Free_AfterQuota_ReturnsFalse()
    {
        var gate = CreateGate(planCode: SubscriptionPlanCodes.CandidateFree, freeSnapshot: true, usedCount: 1);

        Assert.False(await gate.ShouldGrantFullAiFeedbackAsync(UserId));
    }

    [Fact]
    public async Task ShouldGrantFullAi_Premium_AlwaysTrue()
    {
        var gate = CreateGate(planCode: SubscriptionPlanCodes.CandidatePremium, freeSnapshot: false, usedCount: 99);

        Assert.True(await gate.ShouldGrantFullAiFeedbackAsync(UserId));
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

        return new SubscriptionGateService(new FakeUsageMeteringService(sub, usedCount, limits));
    }

    private sealed class FakeUsageMeteringService : IUsageMeteringService
    {
        private readonly DomainLayer.Entities.Subscription _sub;
        private readonly int _usedCount;
        private readonly SubscriptionPlanLimits _limits;

        public FakeUsageMeteringService(
            DomainLayer.Entities.Subscription sub,
            int usedCount,
            SubscriptionPlanLimits limits)
        {
            _sub = sub;
            _usedCount = usedCount;
            _limits = limits;
        }

        public Task<DomainLayer.Entities.Subscription> GetOrThrowSubscriptionAsync(Guid userId)
            => Task.FromResult(_sub);

        public Task<DomainLayer.Entities.Subscription> EnsureCurrentPeriodAsync(DomainLayer.Entities.Subscription subscription)
            => Task.FromResult(subscription);

        public Task<UsageSnapshotDto> GetUsageAsync(Guid userId, string usageType, string? scopeKey = null)
        {
            var limit = usageType switch
            {
                UsageType.CandidatePractice => _limits.PracticePerMonth,
                UsageType.CandidateFullAiFeedback => _limits.FullAiFeedbackPerMonth,
                UsageType.CandidatePersonalSet => _limits.PersonalSetPerMonth,
                _ => 10
            };
            return Task.FromResult(new UsageSnapshotDto
            {
                UsedCount = _usedCount,
                ExtraFromPack = 0,
                LimitFromSnapshot = limit
            });
        }

        public Task IncrementAsync(Guid userId, string usageType, string? scopeKey = null)
            => Task.CompletedTask;

        public Task AddAskAiPackAsync(Guid userId, int extraRequests)
            => Task.CompletedTask;

        public Task MarkGenerateSuccessAsync(Guid userId)
            => Task.CompletedTask;
    }
}
