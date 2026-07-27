using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface ISubscriptionPlanRepository
{
    Task<List<SubscriptionPlan>> ListActiveByAudienceAsync(string audience);
    Task<List<SubscriptionPlan>> ListAllAsync();
    Task<SubscriptionPlan?> GetByIdAsync(Guid id);
    Task<SubscriptionPlan?> GetByCodeAsync(string code);
    Task AddAsync(SubscriptionPlan plan);
    Task UpdateAsync(SubscriptionPlan plan);
}

public interface ISubscriptionRepository
{
    Task<Subscription?> GetByIdWithPlanAsync(Guid subscriptionId);
    Task<Subscription?> GetByUserIdAsync(Guid userId);
    Task<Subscription?> GetByUserIdWithPlanAsync(Guid userId);
    Task AddAsync(Subscription subscription);
    Task UpdateAsync(Subscription subscription);
    /// <summary>Đếm subscription Active theo Plan.Code (admin stats).</summary>
    Task<int> CountActiveByPlanCodeAsync(string planCode);
    /// <summary>Map UserId → (PlanCode, CurrentPeriodEnd) cho batch admin list.</summary>
    Task<Dictionary<Guid, (string PlanCode, DateTime CurrentPeriodEnd)>> GetPlanSummariesByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds);
}

public interface IUsageCounterRepository
{
    Task<UsageCounter?> GetAsync(Guid subscriptionId, DateTime periodStart, string usageType, string? scopeKey);
    Task<List<UsageCounter>> ListBySubscriptionPeriodAsync(Guid subscriptionId, DateTime periodStart);
    Task AddAsync(UsageCounter counter);
    Task UpdateAsync(UsageCounter counter);
    Task DeleteBySubscriptionPeriodAsync(Guid subscriptionId, DateTime periodStart);
    Task<List<UsageCounter>> ListAllAsync();
}

public interface ISubscriptionTransactionRepository
{
    Task AddAsync(SubscriptionTransaction transaction);
    Task UpdateAsync(SubscriptionTransaction transaction);
    Task<SubscriptionTransaction?> GetByOrderCodeAsync(string orderCode);
    Task<SubscriptionTransaction?> GetByExternalTransactionIdAsync(string externalTransactionId);
    Task<List<SubscriptionTransaction>> ListBySubscriptionAsync(Guid subscriptionId, int take = 50);
    /// <summary>Giao dịch gần đây kèm Subscription + Plan + User (admin).</summary>
    Task<List<SubscriptionTransaction>> ListRecentWithDetailsAsync(int take = 20);
    /// <summary>Tất cả giao dịch SePay Upgrade trong khoảng (để aggregate KPI).</summary>
    Task<List<SubscriptionTransaction>> ListSePayUpgradesSinceAsync(DateTime sinceUtc);
}
