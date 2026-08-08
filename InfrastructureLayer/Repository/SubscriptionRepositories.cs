using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using InfrastructureLayer.Database;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class SubscriptionPlanRepository : ISubscriptionPlanRepository
{
    private readonly AppDbContext _db;

    public SubscriptionPlanRepository(AppDbContext db) => _db = db;

    public Task<List<SubscriptionPlan>> ListActiveByAudienceAsync(string audience)
        => _db.SubscriptionPlans
            .Where(p => p.IsActive && p.Audience == audience)
            .OrderBy(p => p.PriceMonthly)
            .ToListAsync();

    public Task<List<SubscriptionPlan>> ListAllAsync()
        => _db.SubscriptionPlans.OrderBy(p => p.Audience).ThenBy(p => p.PriceMonthly).ToListAsync();

    public Task<SubscriptionPlan?> GetByIdAsync(Guid id)
        => _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id);

    public Task<SubscriptionPlan?> GetByCodeAsync(string code)
        => _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == code);

    public async Task AddAsync(SubscriptionPlan plan)
    {
        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(SubscriptionPlan plan)
    {
        plan.UpdatedAt = DateTime.UtcNow;
        _db.SubscriptionPlans.Update(plan);
        await _db.SaveChangesAsync();
    }
}

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly AppDbContext _db;

    public SubscriptionRepository(AppDbContext db) => _db = db;

    public Task<Subscription?> GetByIdWithPlanAsync(Guid subscriptionId)
        => _db.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.IsActive);

    public Task<Subscription?> GetByUserIdAsync(Guid userId)
        => _db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

    public Task<Subscription?> GetByUserIdWithPlanAsync(Guid userId)
        => _db.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

    public async Task AddAsync(Subscription subscription)
    {
        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Subscription subscription)
    {
        subscription.UpdatedAt = DateTime.UtcNow;
        _db.Subscriptions.Update(subscription);
        await _db.SaveChangesAsync();
    }

    public Task<int> CountActiveByPlanCodeAsync(string planCode)
        => (
            from s in _db.Subscriptions.AsNoTracking()
            join p in _db.SubscriptionPlans.AsNoTracking() on s.PlanId equals p.Id
            where s.IsActive
                  && s.Status == DomainLayer.Constants.SubscriptionStatus.Active
                  && p.Code == planCode
            select s.Id
        ).CountAsync();

    public async Task<Dictionary<Guid, (string PlanCode, DateTime CurrentPeriodEnd)>> GetPlanSummariesByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0)
            return new Dictionary<Guid, (string, DateTime)>();

        var rows = await (
            from s in _db.Subscriptions.AsNoTracking()
            join p in _db.SubscriptionPlans.AsNoTracking() on s.PlanId equals p.Id
            where s.IsActive && userIds.Contains(s.UserId)
            select new { s.UserId, p.Code, s.CurrentPeriodEnd }
        ).ToListAsync();

        return rows
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g =>
            {
                var first = g.First();
                return (first.Code, first.CurrentPeriodEnd);
            });
    }
}

public class UsageCounterRepository : IUsageCounterRepository
{
    private readonly AppDbContext _db;

    public UsageCounterRepository(AppDbContext db) => _db = db;

    public Task<UsageCounter?> GetAsync(Guid subscriptionId, DateTime periodStart, string usageType, string? scopeKey)
    {
        var key = scopeKey ?? string.Empty;
        return _db.UsageCounters.FirstOrDefaultAsync(c =>
            c.SubscriptionId == subscriptionId
            && c.PeriodStart == periodStart
            && c.UsageType == usageType
            && c.ScopeKey == key);
    }

    public Task<List<UsageCounter>> ListBySubscriptionPeriodAsync(Guid subscriptionId, DateTime periodStart)
        => _db.UsageCounters
            .Where(c => c.SubscriptionId == subscriptionId && c.PeriodStart == periodStart)
            .ToListAsync();

    public async Task AddAsync(UsageCounter counter)
    {
        _db.UsageCounters.Add(counter);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(UsageCounter counter)
    {
        counter.UpdatedAt = DateTime.UtcNow;
        _db.UsageCounters.Update(counter);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteBySubscriptionPeriodAsync(Guid subscriptionId, DateTime periodStart)
    {
        var rows = await _db.UsageCounters
            .Where(c => c.SubscriptionId == subscriptionId && c.PeriodStart == periodStart)
            .ToListAsync();
        if (rows.Count == 0) return;
        _db.UsageCounters.RemoveRange(rows);
        await _db.SaveChangesAsync();
    }

    public Task<List<UsageCounter>> ListAllAsync()
        => _db.UsageCounters.AsNoTracking().ToListAsync();
}

public class SubscriptionTransactionRepository : ISubscriptionTransactionRepository
{
    private readonly AppDbContext _db;

    public SubscriptionTransactionRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(SubscriptionTransaction transaction)
    {
        _db.SubscriptionTransactions.Add(transaction);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(SubscriptionTransaction transaction)
    {
        transaction.UpdatedAt = DateTime.UtcNow;
        _db.SubscriptionTransactions.Update(transaction);
        await _db.SaveChangesAsync();
    }

    public Task<SubscriptionTransaction?> GetByOrderCodeAsync(string orderCode)
    {
        // Ngân hàng/SePay thường bỏ dấu '-' trong ND CK → so khớp dạng đã normalize.
        var key = NormalizeOrderKey(orderCode);
        return _db.SubscriptionTransactions
            .Include(t => t.Subscription)
            .ThenInclude(s => s.Plan)
            .FirstOrDefaultAsync(t =>
                t.IsActive
                && t.OrderCode != null
                && t.OrderCode.Replace("-", "").ToLower() == key);
    }

    private static string NormalizeOrderKey(string orderCode)
        => orderCode.Replace("-", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();

    public Task<SubscriptionTransaction?> GetByExternalTransactionIdAsync(string externalTransactionId)
        => _db.SubscriptionTransactions
            .Include(t => t.Subscription)
            .ThenInclude(s => s.Plan)
            .FirstOrDefaultAsync(t => t.ExternalTransactionId == externalTransactionId && t.IsActive);

    public Task<List<SubscriptionTransaction>> ListBySubscriptionAsync(Guid subscriptionId, int take = 50)
        => _db.SubscriptionTransactions
            .Where(t => t.SubscriptionId == subscriptionId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(take)
            .ToListAsync();

    public Task<List<SubscriptionTransaction>> ListRecentWithDetailsAsync(int take = 20)
        => _db.SubscriptionTransactions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(t => t.Subscription)
                .ThenInclude(s => s.Plan)
            .Include(t => t.Subscription)
                .ThenInclude(s => s.User)
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync();

    public Task<List<SubscriptionTransaction>> ListSePayUpgradesSinceAsync(DateTime sinceUtc)
        => _db.SubscriptionTransactions
            .AsNoTracking()
            .Where(t =>
                t.IsActive
                && t.Provider == "SePay"
                && t.Type == DomainLayer.Constants.SubscriptionTransactionTypes.Upgrade
                && t.CreatedAt >= sinceUtc)
            .ToListAsync();

    public Task<List<SubscriptionTransaction>> ListPendingUpgradesBySubscriptionAsync(Guid subscriptionId)
        => _db.SubscriptionTransactions
            .Where(t =>
                t.IsActive
                && t.SubscriptionId == subscriptionId
                && t.Type == DomainLayer.Constants.SubscriptionTransactionTypes.Upgrade
                && t.Status == DomainLayer.Constants.SubscriptionTransactionStatus.Pending)
            .ToListAsync();

    public Task<List<SubscriptionTransaction>> ListExpiredPendingUpgradesAsync(DateTime utcNow, int take = 200)
        => _db.SubscriptionTransactions
            .Where(t =>
                t.IsActive
                && t.Type == DomainLayer.Constants.SubscriptionTransactionTypes.Upgrade
                && t.Status == DomainLayer.Constants.SubscriptionTransactionStatus.Pending
                && t.ExpiresAt != null
                && t.ExpiresAt <= utcNow)
            .OrderBy(t => t.ExpiresAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync();
}
