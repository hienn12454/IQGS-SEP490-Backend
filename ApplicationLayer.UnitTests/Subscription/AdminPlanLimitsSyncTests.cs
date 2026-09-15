using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.Subscription;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.Extensions.Options;
using Xunit;
using SubscriptionEntity = DomainLayer.Entities.Subscription;

namespace ApplicationLayer.UnitTests.Subscription;

public sealed class AdminPlanLimitsSyncTests
{
    private static readonly Guid PlanId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task UpdateAsync_WithLimits_SyncsActiveSnapshots()
    {
        var plan = CreatePremiumPlan(askAi: 1000);
        var planRepo = new FakePlanRepo(plan);
        var subRepo = new FakeSubscriptionRepo();
        var sut = CreateSut(planRepo, subRepo);

        var result = await sut.UpdateAsync(PlanId, new UpdateSubscriptionPlanDto
        {
            Limits = new SubscriptionPlanLimits
            {
                AskAiPerMonth = 300,
                GenerateUnlimited = true,
                CanExport = true,
                CanPublish = true
            }
        });

        Assert.False(result.AppliesToExistingSubscribersFromNextPeriod);
        Assert.Equal(1, subRepo.SyncCallCount);
        Assert.Equal(PlanId, subRepo.LastSyncedPlanId);
        Assert.Contains("\"askAiPerMonth\":300", subRepo.LastSyncedJson!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(300, SubscriptionLimitsHelper.Deserialize(plan.LimitsJson).AskAiPerMonth);
    }

    [Fact]
    public async Task UpdateAsync_NameOnly_DoesNotSyncSnapshots()
    {
        var plan = CreatePremiumPlan(askAi: 1000);
        var planRepo = new FakePlanRepo(plan);
        var subRepo = new FakeSubscriptionRepo();
        var sut = CreateSut(planRepo, subRepo);

        var result = await sut.UpdateAsync(PlanId, new UpdateSubscriptionPlanDto
        {
            Name = "HR Premium Updated"
        });

        Assert.Equal(0, subRepo.SyncCallCount);
        Assert.False(result.AppliesToExistingSubscribersFromNextPeriod);
        Assert.Equal("HR Premium Updated", plan.Name);
        Assert.Equal(1000, SubscriptionLimitsHelper.Deserialize(plan.LimitsJson).AskAiPerMonth);
    }

    private static SubscriptionPlan CreatePremiumPlan(int askAi)
    {
        var limits = SubscriptionPlanLimits.HrPremium();
        limits.AskAiPerMonth = askAi;
        return new SubscriptionPlan
        {
            Id = PlanId,
            Code = SubscriptionPlanCodes.HrPremium,
            Audience = SubscriptionAudience.HR,
            Name = "HR Premium",
            PriceMonthly = 699000,
            Currency = "VND",
            IsActive = true,
            LimitsJson = SubscriptionLimitsHelper.Serialize(limits)
        };
    }

    private static AdminSubscriptionPlanService CreateSut(
        ISubscriptionPlanRepository planRepo,
        ISubscriptionRepository subRepo)
        => new(
            planRepo,
            subRepo,
            new UnusedUsageCounterRepo(),
            new UnusedTxRepo(),
            new UnusedMetering(),
            new UnusedUserRepo(),
            new UnusedSePay(),
            Options.Create(new SePaySettings()));

    private sealed class FakePlanRepo(SubscriptionPlan plan) : ISubscriptionPlanRepository
    {
        public Task<List<SubscriptionPlan>> ListActiveByAudienceAsync(string audience) => Throw<List<SubscriptionPlan>>();
        public Task<List<SubscriptionPlan>> ListAllAsync() => Throw<List<SubscriptionPlan>>();
        public Task<SubscriptionPlan?> GetByIdAsync(Guid id)
            => Task.FromResult(id == plan.Id ? plan : null);
        public Task<SubscriptionPlan?> GetByCodeAsync(string code) => Throw<SubscriptionPlan?>();
        public Task AddAsync(SubscriptionPlan p) => Throw();
        public Task UpdateAsync(SubscriptionPlan p) => Task.CompletedTask;
    }

    private sealed class FakeSubscriptionRepo : ISubscriptionRepository
    {
        public int SyncCallCount { get; private set; }
        public Guid? LastSyncedPlanId { get; private set; }
        public string? LastSyncedJson { get; private set; }

        public Task<SubscriptionEntity?> GetByIdWithPlanAsync(Guid subscriptionId) => Throw<SubscriptionEntity?>();
        public Task<SubscriptionEntity?> GetByUserIdAsync(Guid userId) => Throw<SubscriptionEntity?>();
        public Task<SubscriptionEntity?> GetByUserIdWithPlanAsync(Guid userId) => Throw<SubscriptionEntity?>();
        public Task AddAsync(SubscriptionEntity subscription) => Throw();
        public Task UpdateAsync(SubscriptionEntity subscription) => Throw();
        public Task<int> CountActiveByPlanCodeAsync(string planCode) => Throw<int>();
        public Task<Dictionary<Guid, (string PlanCode, DateTime CurrentPeriodEnd)>> GetPlanSummariesByUserIdsAsync(
            IReadOnlyCollection<Guid> userIds)
            => Throw<Dictionary<Guid, (string, DateTime)>>();

        public Task<int> SyncLimitsSnapshotForActiveByPlanIdAsync(Guid planId, string limitsJson)
        {
            SyncCallCount++;
            LastSyncedPlanId = planId;
            LastSyncedJson = limitsJson;
            return Task.FromResult(1);
        }
    }

    private sealed class UnusedUsageCounterRepo : IUsageCounterRepository
    {
        public Task<UsageCounter?> GetAsync(Guid subscriptionId, DateTime periodStart, string usageType, string? scopeKey)
            => Throw<UsageCounter?>();
        public Task<List<UsageCounter>> ListBySubscriptionPeriodAsync(Guid subscriptionId, DateTime periodStart)
            => Throw<List<UsageCounter>>();
        public Task AddAsync(UsageCounter counter) => Throw();
        public Task UpdateAsync(UsageCounter counter) => Throw();
        public Task DeleteBySubscriptionPeriodAsync(Guid subscriptionId, DateTime periodStart) => Throw();
        public Task<List<UsageCounter>> ListAllAsync() => Throw<List<UsageCounter>>();
    }

    private sealed class UnusedTxRepo : ISubscriptionTransactionRepository
    {
        public Task AddAsync(SubscriptionTransaction transaction) => Throw();
        public Task UpdateAsync(SubscriptionTransaction transaction) => Throw();
        public Task<SubscriptionTransaction?> GetByOrderCodeAsync(string orderCode) => Throw<SubscriptionTransaction?>();
        public Task<SubscriptionTransaction?> GetByExternalTransactionIdAsync(string externalTransactionId)
            => Throw<SubscriptionTransaction?>();
        public Task<List<SubscriptionTransaction>> ListBySubscriptionAsync(Guid subscriptionId, int take = 50)
            => Throw<List<SubscriptionTransaction>>();
        public Task<List<SubscriptionTransaction>> ListRecentWithDetailsAsync(int take = 20)
            => Throw<List<SubscriptionTransaction>>();
        public Task<List<SubscriptionTransaction>> ListSePayUpgradesSinceAsync(DateTime sinceUtc)
            => Throw<List<SubscriptionTransaction>>();
        public Task<List<SubscriptionTransaction>> ListPendingUpgradesBySubscriptionAsync(Guid subscriptionId)
            => Throw<List<SubscriptionTransaction>>();
        public Task<List<SubscriptionTransaction>> ListExpiredPendingUpgradesAsync(DateTime utcNow, int take = 200)
            => Throw<List<SubscriptionTransaction>>();
    }

    private sealed class UnusedMetering : IUsageMeteringService
    {
        public Task<SubscriptionEntity> EnsureCurrentPeriodAsync(SubscriptionEntity subscription) => Throw<SubscriptionEntity>();
        public Task<SubscriptionEntity> GetOrThrowSubscriptionAsync(Guid userId) => Throw<SubscriptionEntity>();
        public Task IncrementAsync(Guid userId, string usageType, string? scopeKey = null) => Throw();
        public Task<UsageSnapshotDto> GetUsageAsync(Guid userId, string usageType, string? scopeKey = null)
            => Throw<UsageSnapshotDto>();
        public Task AddAskAiPackAsync(Guid userId, int extraRequests) => Throw();
        public Task MarkGenerateSuccessAsync(Guid userId) => Throw();
    }

    private sealed class UnusedUserRepo : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id) => Throw<User?>();
        public Task<IEnumerable<User>> GetAllAsync() => Throw<IEnumerable<User>>();
        public Task AddAsync(User entity) => Throw();
        public Task UpdateAsync(User entity) => Throw();
        public Task DeleteAsync(User entity) => Throw();
        public Task<bool> ExistsAsync(Guid id) => Throw<bool>();
        public Task<User?> GetByEmailAsync(string email) => Throw<User?>();
        public Task<User?> GetByGoogleIdAsync(string googleId) => Throw<User?>();
        public Task<User?> GetByGithubIdAsync(string githubId) => Throw<User?>();
        public Task<User?> GetByRefreshTokenAsync(string refreshToken) => Throw<User?>();
        public Task<User?> GetByPasswordResetTokenAsync(string tokenHash) => Throw<User?>();
        public Task<User?> GetByEmailVerificationTokenAsync(string tokenHash) => Throw<User?>();
        public Task<(List<User> Users, int Total)> GetPagedAsync(UserQueryDto query) => Throw<(List<User>, int)>();
        public Task<User?> GetByIdAnyStatusAsync(Guid id) => Throw<User?>();
        public Task<User?> GetByEmailAnyStatusAsync(string email) => Throw<User?>();
        public Task<User?> GetByGoogleIdAnyStatusAsync(string googleId) => Throw<User?>();
        public Task<User?> GetByGithubIdAnyStatusAsync(string githubId) => Throw<User?>();
        public Task LoadRoleAsync(User user) => Throw();
    }

    private sealed class UnusedSePay : ISePayGateway
    {
        public Task<SePayCreateOrderResult> CreateUpgradeOrderAsync(SePayCreateOrderRequest request, CancellationToken ct = default)
            => Throw<SePayCreateOrderResult>();
        public bool VerifyWebhookSignature(string rawBody, string? timestamp, string? signatureHeader) => false;
        public Task<List<SePayBankAccountDto>> ListBankAccountsAsync(CancellationToken ct = default)
            => Throw<List<SePayBankAccountDto>>();
        public Task<List<SePayLiveTransactionDto>> ListTransactionsAsync(
            SePayListTransactionsRequest request, CancellationToken ct = default)
            => Throw<List<SePayLiveTransactionDto>>();
    }

    private static Task Throw() => Task.FromException(new NotImplementedException());
    private static Task<T> Throw<T>() => Task.FromException<T>(new NotImplementedException());
}
