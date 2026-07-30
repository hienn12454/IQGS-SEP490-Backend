using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Jobs;

/// <summary>
/// Quét Pending Upgrade quá ExpiresAt → Expired.
/// Bọc case user đóng app / không còn poll GET status nên lazy-expire không chạy.
/// </summary>
public class ExpirePendingUpgradeOrdersJob : IExpirePendingUpgradeOrdersJob
{
    private readonly ISubscriptionTransactionRepository _txRepo;
    private readonly ILogger<ExpirePendingUpgradeOrdersJob> _logger;

    public ExpirePendingUpgradeOrdersJob(
        ISubscriptionTransactionRepository txRepo,
        ILogger<ExpirePendingUpgradeOrdersJob> logger)
    {
        _txRepo = txRepo;
        _logger = logger;
    }

    [Queue("default")]
    public async Task ExecuteAsync()
    {
        var now = DateTime.UtcNow;
        var expired = await _txRepo.ListExpiredPendingUpgradesAsync(now, take: 200);
        if (expired.Count == 0) return;

        foreach (var tx in expired)
        {
            try
            {
                // Guard: chỉ đổi nếu vẫn Pending (race với create cancel / webhook)
                if (tx.Status != SubscriptionTransactionStatus.Pending)
                    continue;

                tx.Status = SubscriptionTransactionStatus.Expired;
                var note = string.IsNullOrWhiteSpace(tx.Note)
                    ? "Expired by Hangfire TTL job"
                    : $"{tx.Note} | Expired by Hangfire TTL job";
                tx.Note = note.Length > 500 ? note[..500] : note;
                await _txRepo.UpdateAsync(tx);

                _logger.LogInformation(
                    "Expire upgrade order {OrderCode} (tx {TxId}) — past ExpiresAt {ExpiresAt:O}.",
                    tx.OrderCode, tx.Id, tx.ExpiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expire job thất bại cho transaction {TxId}.", tx.Id);
            }
        }
    }
}
