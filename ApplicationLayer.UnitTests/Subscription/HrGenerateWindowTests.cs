using ApplicationLayer.Helpers;
using ApplicationLayer.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Xunit;

namespace ApplicationLayer.UnitTests.Subscription;

public sealed class HrGenerateWindowTests
{
    private static readonly DateTime T0 = new(2026, 8, 18, 4, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    public void ResolveMax_UsesFieldOrFallback1(int configured, int expected)
    {
        var limits = new SubscriptionPlanLimits { GeneratePerWindow = configured };
        Assert.Equal(expected, HrGenerateWindow.ResolveMax(limits));
    }

    [Fact]
    public void IsCooldownActive_NullLast_False()
    {
        Assert.False(HrGenerateWindow.IsCooldownActive(null, 24, T0));
    }

    [Fact]
    public void IsCooldownActive_Within24h_True()
    {
        Assert.True(HrGenerateWindow.IsCooldownActive(T0.AddHours(-1), 24, T0));
    }

    [Fact]
    public void IsCooldownActive_After24h_False()
    {
        Assert.False(HrGenerateWindow.IsCooldownActive(T0.AddHours(-25), 24, T0));
    }

    [Fact]
    public void NextMark_FirstOfOne_SetsLastToLockFe()
    {
        var (used, last) = HrGenerateWindow.NextMark(0, lastSuccessAt: null, 24, 1, T0);
        Assert.Equal(1, used);
        Assert.Equal(T0, last);
    }

    [Fact]
    public void NextMark_AfterCooldown_ResetsWindow()
    {
        var (used, last) = HrGenerateWindow.NextMark(
            currentUsed: 1,
            lastSuccessAt: T0.AddHours(-25),
            cooldownHours: 24,
            max: 1,
            utcNow: T0);

        Assert.Equal(1, used);
        Assert.Equal(T0, last);
    }
}

public sealed class CheckGenerateSetWindowTests
{
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task CheckGenerate_Free_NoLast_DoesNotThrow()
    {
        var gate = CreateHrGate(lastSuccess: null);
        var ex = await Record.ExceptionAsync(() => gate.CheckGenerateSetAsync(UserId));
        Assert.Null(ex);
    }

    [Fact]
    public async Task CheckGenerate_Free_LastWithin24h_ThrowsCooldown()
    {
        var gate = CreateHrGate(lastSuccess: DateTime.UtcNow.AddHours(-1));
        var ex = await Assert.ThrowsAsync<SubscriptionGateException>(() => gate.CheckGenerateSetAsync(UserId));
        Assert.Equal(SubscriptionErrorCodes.CooldownActive, ex.ErrorCode);
        Assert.Contains("1 lần", ex.Message, StringComparison.Ordinal);
        Assert.Contains("đánh giá JD", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckGenerate_Free_LastOlderThan24h_DoesNotThrow()
    {
        var gate = CreateHrGate(lastSuccess: DateTime.UtcNow.AddHours(-25));
        var ex = await Record.ExceptionAsync(() => gate.CheckGenerateSetAsync(UserId));
        Assert.Null(ex);
    }

    [Fact]
    public async Task CheckGenerate_PremiumUnlimited_DoesNotThrow()
    {
        var limits = SubscriptionPlanLimits.HrPremium();
        var sub = new DomainLayer.Entities.Subscription
        {
            UserId = UserId,
            Status = SubscriptionStatus.Active,
            LimitsSnapshotJson = SubscriptionLimitsHelper.Serialize(limits),
            LastSuccessfulGenerateAt = DateTime.UtcNow,
            Plan = new SubscriptionPlan { Code = SubscriptionPlanCodes.HrPremium, Audience = SubscriptionAudience.HR, Name = "HR Premium" }
        };
        var gate = new SubscriptionGateService(new LastAwareMetering(sub));
        var ex = await Record.ExceptionAsync(() => gate.CheckGenerateSetAsync(UserId));
        Assert.Null(ex);
    }

    /// <summary>SCRUM-445: used >= max + Last null → vẫn chặn (khớp FE canGenerateNow).</summary>
    [Fact]
    public async Task CheckGenerate_Free_WindowFullWithoutLast_ThrowsCooldown()
    {
        var gate = CreateHrGate(lastSuccess: null, windowUsed: 1);
        var ex = await Assert.ThrowsAsync<SubscriptionGateException>(() => gate.CheckGenerateSetAsync(UserId));
        Assert.Equal(SubscriptionErrorCodes.CooldownActive, ex.ErrorCode);
    }

    /// <summary>SCRUM-445: used >= max nhưng cooldown đã hết → cho qua (NextMark reset).</summary>
    [Fact]
    public async Task CheckGenerate_Free_WindowFullAfterCooldown_DoesNotThrow()
    {
        var gate = CreateHrGate(lastSuccess: DateTime.UtcNow.AddHours(-25), windowUsed: 1);
        var ex = await Record.ExceptionAsync(() => gate.CheckGenerateSetAsync(UserId));
        Assert.Null(ex);
    }

    [Fact]
    public async Task CheckQuestionRegen_Free_AtLimit_Throws()
    {
        var planId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var gate = CreateHrGate(lastSuccess: null, windowUsed: 0, questionRegenUsed: 2);
        var ex = await Assert.ThrowsAsync<SubscriptionGateException>(
            () => gate.CheckQuestionRegenAsync(UserId, planId));
        Assert.Equal(SubscriptionErrorCodes.QuestionRegenLimit, ex.ErrorCode);
    }

    [Fact]
    public async Task CheckQuestionRegen_Free_UnderLimit_DoesNotThrow()
    {
        var planId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var gate = CreateHrGate(lastSuccess: null, windowUsed: 0, questionRegenUsed: 1);
        var ex = await Record.ExceptionAsync(() => gate.CheckQuestionRegenAsync(UserId, planId));
        Assert.Null(ex);
    }

    [Fact]
    public async Task CheckQuestionRegen_Premium_DoesNotThrow()
    {
        var limits = SubscriptionPlanLimits.HrPremium();
        var sub = new DomainLayer.Entities.Subscription
        {
            UserId = UserId,
            Status = SubscriptionStatus.Active,
            LimitsSnapshotJson = SubscriptionLimitsHelper.Serialize(limits),
            Plan = new SubscriptionPlan { Code = SubscriptionPlanCodes.HrPremium, Audience = SubscriptionAudience.HR, Name = "HR Premium" }
        };
        var gate = new SubscriptionGateService(new LastAwareMetering(sub, windowUsed: 0, questionRegenUsed: 99));
        var ex = await Record.ExceptionAsync(
            () => gate.CheckQuestionRegenAsync(UserId, Guid.NewGuid()));
        Assert.Null(ex);
    }

    [Fact]
    public async Task CheckGenerate_Premium_WindowFull_DoesNotThrow()
    {
        var limits = SubscriptionPlanLimits.HrPremium();
        var sub = new DomainLayer.Entities.Subscription
        {
            UserId = UserId,
            Status = SubscriptionStatus.Active,
            LimitsSnapshotJson = SubscriptionLimitsHelper.Serialize(limits),
            LastSuccessfulGenerateAt = null,
            Plan = new SubscriptionPlan { Code = SubscriptionPlanCodes.HrPremium, Audience = SubscriptionAudience.HR, Name = "HR Premium" }
        };
        var gate = new SubscriptionGateService(new LastAwareMetering(sub, windowUsed: 99));
        var ex = await Record.ExceptionAsync(() => gate.CheckGenerateSetAsync(UserId));
        Assert.Null(ex);
    }

    private static SubscriptionGateService CreateHrGate(
        DateTime? lastSuccess,
        int windowUsed = 0,
        int questionRegenUsed = 0)
    {
        var limits = SubscriptionPlanLimits.HrFree();
        var sub = new DomainLayer.Entities.Subscription
        {
            UserId = UserId,
            Status = SubscriptionStatus.Active,
            LimitsSnapshotJson = SubscriptionLimitsHelper.Serialize(limits),
            LastSuccessfulGenerateAt = lastSuccess,
            Plan = new SubscriptionPlan { Code = SubscriptionPlanCodes.HrFree, Audience = SubscriptionAudience.HR, Name = "HR Free" }
        };
        return new SubscriptionGateService(new LastAwareMetering(sub, windowUsed, questionRegenUsed));
    }

    private sealed class LastAwareMetering : IUsageMeteringService
    {
        private readonly DomainLayer.Entities.Subscription _sub;
        private readonly int _windowUsed;
        private readonly int _questionRegenUsed;

        public LastAwareMetering(
            DomainLayer.Entities.Subscription sub,
            int windowUsed = 0,
            int questionRegenUsed = 0)
        {
            _sub = sub;
            _windowUsed = windowUsed;
            _questionRegenUsed = questionRegenUsed;
        }

        public Task<DomainLayer.Entities.Subscription> GetOrThrowSubscriptionAsync(Guid userId) => Task.FromResult(_sub);
        public Task<DomainLayer.Entities.Subscription> EnsureCurrentPeriodAsync(DomainLayer.Entities.Subscription subscription) => Task.FromResult(subscription);
        public Task<UsageSnapshotDto> GetUsageAsync(Guid userId, string usageType, string? scopeKey = null)
        {
            if (string.Equals(usageType, UsageType.HrQuestionRegen, StringComparison.Ordinal))
                return Task.FromResult(new UsageSnapshotDto { UsedCount = _questionRegenUsed, ExtraFromPack = 0, LimitFromSnapshot = 2 });

            var used = string.Equals(scopeKey, HrGenerateWindow.ScopeKey, StringComparison.Ordinal)
                ? _windowUsed
                : 0;
            return Task.FromResult(new UsageSnapshotDto { UsedCount = used, ExtraFromPack = 0, LimitFromSnapshot = 1 });
        }
        public Task IncrementAsync(Guid userId, string usageType, string? scopeKey = null) => Task.CompletedTask;
        public Task AddAskAiPackAsync(Guid userId, int extraRequests) => Task.CompletedTask;
        public Task MarkGenerateSuccessAsync(Guid userId) => Task.CompletedTask;
    }
}
