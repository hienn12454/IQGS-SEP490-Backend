using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ApplicationLayer.DTOs.Subscription;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services;

public interface IAdminSubscriptionPlanService
{
    Task<List<SubscriptionPlanDto>> ListAsync();
    Task<SubscriptionPlanDto> CreateAsync(CreateSubscriptionPlanDto dto);
    Task<SubscriptionPlanDto> UpdateAsync(Guid id, UpdateSubscriptionPlanDto dto);
    Task<AdminUsageSummaryDto> GetUserUsageAsync(Guid userId);
    Task<List<AdminUsageAggregateItemDto>> GetUsageSummaryAsync();
    Task<AdminSubscriptionStatsDto> GetPaymentStatsAsync();
    Task<List<AdminSubscriptionTransactionRowDto>> ListRecentTransactionsAsync(int take = 20);
    Task<AdminSePayOverviewDto> GetSePayOverviewAsync(bool forceRefresh = false);

    // SCRUM-386: Admin gán / gia hạn / thu hồi Premium theo user
    Task<AdminUserSubscriptionDetailDto> GetUserSubscriptionAsync(Guid userId);
    Task<AdminUserSubscriptionDetailDto> GrantPremiumAsync(Guid userId, AdminGrantPremiumDto dto, Guid adminUserId);
    Task<AdminUserSubscriptionDetailDto> UpdateUserSubscriptionPeriodAsync(Guid userId, AdminUpdateUserSubscriptionDto dto, Guid adminUserId);
    Task<AdminUserSubscriptionDetailDto> RevokePremiumAsync(Guid userId, AdminRevokePremiumDto dto, Guid adminUserId);
}

public class AdminSubscriptionPlanService : IAdminSubscriptionPlanService
{
    private static readonly ConcurrentDictionary<string, (DateTime ExpiresAt, AdminSePayOverviewDto Value)> SePayCache = new();
    private static readonly TimeSpan SePayCacheTtl = TimeSpan.FromSeconds(50);
    private static readonly Regex OrderCodeRegex = new(
        @"UPG(?:HR|CA)[A-Z0-9]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ISubscriptionPlanRepository _planRepo;
    private readonly ISubscriptionRepository _subscriptionRepo;
    private readonly IUsageCounterRepository _usageRepo;
    private readonly ISubscriptionTransactionRepository _txRepo;
    private readonly IUsageMeteringService _metering;
    private readonly IUserRepository _userRepo;
    private readonly ISePayGateway _sePay;
    private readonly SePaySettings _sePaySettings;

    private static readonly int[] AllowedPeriodMonths = [1, 3, 6, 12];

    public AdminSubscriptionPlanService(
        ISubscriptionPlanRepository planRepo,
        ISubscriptionRepository subscriptionRepo,
        IUsageCounterRepository usageRepo,
        ISubscriptionTransactionRepository txRepo,
        IUsageMeteringService metering,
        IUserRepository userRepo,
        ISePayGateway sePay,
        IOptions<SePaySettings> sePaySettings)
    {
        _planRepo = planRepo;
        _subscriptionRepo = subscriptionRepo;
        _usageRepo = usageRepo;
        _txRepo = txRepo;
        _metering = metering;
        _userRepo = userRepo;
        _sePay = sePay;
        _sePaySettings = sePaySettings.Value;
    }

    public async Task<List<SubscriptionPlanDto>> ListAsync()
    {
        var plans = await _planRepo.ListAllAsync();
        return plans.Select(Map).ToList();
    }

    public async Task<SubscriptionPlanDto> CreateAsync(CreateSubscriptionPlanDto dto)
    {
        if (await _planRepo.GetByCodeAsync(dto.Code) != null)
            throw new ConflictException($"Plan code {dto.Code} đã tồn tại.");

        var audience = NormalizeAudience(dto.Audience);
        var code = dto.Code.Trim().ToUpperInvariant();
        ValidatePlanPrice(code, dto.PriceMonthly);
        var plan = new SubscriptionPlan
        {
            Code = code,
            Audience = audience,
            Name = dto.Name.Trim(),
            PriceMonthly = dto.PriceMonthly,
            Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "VND" : dto.Currency.Trim().ToUpperInvariant(),
            LimitsJson = SubscriptionLimitsHelper.Serialize(dto.Limits),
            IsActive = true
        };
        await _planRepo.AddAsync(plan);
        return Map(plan);
    }

    public async Task<SubscriptionPlanDto> UpdateAsync(Guid id, UpdateSubscriptionPlanDto dto)
    {
        var plan = await _planRepo.GetByIdAsync(id)
            ?? throw new NotFoundException("Không tìm thấy gói.");

        if (dto.Name != null) plan.Name = dto.Name.Trim();
        if (dto.PriceMonthly.HasValue)
        {
            ValidatePlanPrice(plan.Code, dto.PriceMonthly.Value);
            plan.PriceMonthly = dto.PriceMonthly.Value;
        }
        if (dto.Currency != null) plan.Currency = dto.Currency.Trim().ToUpperInvariant();
        if (dto.IsActive.HasValue) plan.IsActive = dto.IsActive.Value;
        // Chỉ cập nhật template — subscriber giữ LimitsSnapshot đến PeriodEnd
        if (dto.Limits != null)
            plan.LimitsJson = SubscriptionLimitsHelper.Serialize(dto.Limits);

        await _planRepo.UpdateAsync(plan);
        var result = Map(plan);
        result.AppliesToExistingSubscribersFromNextPeriod = true;
        return result;
    }

    public async Task<AdminUsageSummaryDto> GetUserUsageAsync(Guid userId)
    {
        var sub = await _metering.GetOrThrowSubscriptionAsync(userId);
        var plan = sub.Plan ?? await _planRepo.GetByIdAsync(sub.PlanId);
        var usage = await _usageRepo.ListBySubscriptionPeriodAsync(sub.Id, sub.CurrentPeriodStart);

        return new AdminUsageSummaryDto
        {
            UserId = userId,
            PlanCode = plan?.Code ?? "",
            PeriodStart = sub.CurrentPeriodStart,
            PeriodEnd = sub.CurrentPeriodEnd,
            Usage = usage.Select(u => new UsageCounterDto
            {
                UsageType = u.UsageType,
                ScopeKey = u.ScopeKey,
                UsedCount = u.UsedCount,
                ExtraFromPack = u.ExtraFromPack,
                PeriodStart = u.PeriodStart
            }).ToList()
        };
    }

    public async Task<List<AdminUsageAggregateItemDto>> GetUsageSummaryAsync()
    {
        var rows = await _usageRepo.ListAllAsync();
        return rows
            .GroupBy(r => r.UsageType)
            .Select(g => new AdminUsageAggregateItemDto
            {
                UsageType = g.Key,
                TotalUsed = g.Sum(x => x.UsedCount),
                UserCount = g.Select(x => x.SubscriptionId).Distinct().Count()
            })
            .OrderByDescending(x => x.TotalUsed)
            .ToList();
    }

    public async Task<AdminSubscriptionStatsDto> GetPaymentStatsAsync()
    {
        var vnNow = DateTime.UtcNow.AddHours(7);
        var monthStartVn = new DateTime(vnNow.Year, vnNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var monthStartUtc = monthStartVn.AddHours(-7);
        var since30Utc = DateTime.UtcNow.AddDays(-30);
        var nowUtc = DateTime.UtcNow;

        var sePayRows = await _txRepo.ListSePayUpgradesSinceAsync(since30Utc);
        var revenueThisMonth = sePayRows
            .Where(t =>
                t.Status == SubscriptionTransactionStatus.Paid
                && (t.ConfirmedAt ?? t.CreatedAt) >= monthStartUtc)
            .Sum(t => t.Amount);

        var pending = sePayRows.Count(t =>
            t.Status == SubscriptionTransactionStatus.Pending
            && (!t.ExpiresAt.HasValue || t.ExpiresAt > nowUtc));

        var closed30 = sePayRows.Where(t =>
            t.Status is SubscriptionTransactionStatus.Paid
                or SubscriptionTransactionStatus.Expired
                or SubscriptionTransactionStatus.Failed).ToList();
        var paid30 = closed30.Count(t => t.Status == SubscriptionTransactionStatus.Paid);
        var paidRate = closed30.Count == 0 ? 0 : (double)paid30 / closed30.Count;

        var premiumHr = await _subscriptionRepo.CountActiveByPlanCodeAsync(SubscriptionPlanCodes.HrPremium);
        var premiumCa = await _subscriptionRepo.CountActiveByPlanCodeAsync(SubscriptionPlanCodes.CandidatePremium);

        var hrPlan = await _planRepo.GetByCodeAsync(SubscriptionPlanCodes.HrPremium);
        var caPlan = await _planRepo.GetByCodeAsync(SubscriptionPlanCodes.CandidatePremium);
        var mrr = premiumHr * (hrPlan?.PriceMonthly ?? 0) + premiumCa * (caPlan?.PriceMonthly ?? 0);
        var currency = hrPlan?.Currency ?? caPlan?.Currency ?? "VND";

        return new AdminSubscriptionStatsDto
        {
            RevenueThisMonth = revenueThisMonth,
            Currency = currency,
            PendingOrders = pending,
            PremiumHrActive = premiumHr,
            PremiumCandidateActive = premiumCa,
            EstimatedMrr = mrr,
            PaidRateLast30Days = Math.Round(paidRate, 4),
            PaidLast30Days = paid30,
            ClosedLast30Days = closed30.Count
        };
    }

    public async Task<List<AdminSubscriptionTransactionRowDto>> ListRecentTransactionsAsync(int take = 20)
    {
        var rows = await _txRepo.ListRecentWithDetailsAsync(take);
        return rows.Select(t => new AdminSubscriptionTransactionRowDto
        {
            Id = t.Id,
            OrderCode = t.OrderCode,
            Type = t.Type,
            Status = t.Status,
            Provider = t.Provider,
            Amount = t.Amount,
            Currency = t.Currency,
            Audience = t.Subscription?.Plan?.Audience,
            PlanCode = t.Subscription?.Plan?.Code,
            UserEmail = t.Subscription?.User?.Email,
            CreatedAt = t.CreatedAt,
            ConfirmedAt = t.ConfirmedAt,
            ExpiresAt = t.ExpiresAt
        }).ToList();
    }

    public async Task<AdminSePayOverviewDto> GetSePayOverviewAsync(bool forceRefresh = false)
    {
        const string cacheKey = "sepay-overview";
        if (!forceRefresh
            && SePayCache.TryGetValue(cacheKey, out var cached)
            && cached.ExpiresAt > DateTime.UtcNow)
        {
            return cached.Value;
        }

        if (!_sePaySettings.Enabled || string.IsNullOrWhiteSpace(_sePaySettings.ApiKey))
        {
            return new AdminSePayOverviewDto
            {
                Available = false,
                Message = "SePay User API chưa cấu hình (ApiKey / Enabled).",
                Bank = new AdminSePayBankCardDto
                {
                    Available = false,
                    Message = "Chưa cấu hình SePay ApiKey."
                }
            };
        }

        var from = DateTime.UtcNow.AddHours(7).Date.AddDays(-13);
        var to = DateTime.UtcNow.AddHours(7).Date.AddDays(1).AddSeconds(-1);

        var accountsTask = _sePay.ListBankAccountsAsync();
        var txsTask = _sePay.ListTransactionsAsync(new SePayListTransactionsRequest
        {
            From = from,
            To = to,
            TransferType = "in",
            PerPage = 50
        });
        await Task.WhenAll(accountsTask, txsTask);

        var accounts = accountsTask.Result;
        var liveTxs = txsTask.Result;

        var bank = PickBankAccount(accounts);
        var localRecent = await _txRepo.ListSePayUpgradesSinceAsync(DateTime.UtcNow.AddDays(-14));
        var localByOrder = localRecent
            .Where(t => !string.IsNullOrWhiteSpace(t.OrderCode))
            .GroupBy(t => NormalizeOrderKey(t.OrderCode!))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First());

        var rows = liveTxs.Select(tx =>
        {
            var orderCode = ExtractOrderCode(tx.TransactionContent) ?? ExtractOrderCode(tx.Code);
            string matchStatus;
            string? matched = null;
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                matchStatus = "Unmatched";
            }
            else
            {
                matched = orderCode;
                var key = NormalizeOrderKey(orderCode);
                if (localByOrder.TryGetValue(key, out var local))
                {
                    matchStatus = local.Status == SubscriptionTransactionStatus.Pending
                        ? "PendingLocal"
                        : local.Status == SubscriptionTransactionStatus.Paid
                            ? "Matched"
                            : local.Status;
                }
                else
                {
                    matchStatus = "Unmatched";
                }
            }

            return new AdminSePayLiveTxRowDto
            {
                Id = tx.Id,
                TransactionDate = tx.TransactionDate,
                AmountIn = tx.AmountIn,
                TransactionContent = tx.TransactionContent,
                ReferenceNumber = tx.ReferenceNumber,
                BankBrandName = tx.BankBrandName,
                WebhookSuccess = tx.WebhookSuccess,
                MatchStatus = matchStatus,
                MatchedOrderCode = matched
            };
        }).ToList();

        var daily = BuildDailyInLast7Days(liveTxs);
        var available = accounts.Count > 0 || liveTxs.Count > 0;
        var overview = new AdminSePayOverviewDto
        {
            Available = available,
            Message = available ? null : "Không lấy được dữ liệu SePay (API lỗi hoặc chưa có giao dịch).",
            Bank = bank,
            RecentInflows = rows.Take(30).ToList(),
            DailyInLast7Days = daily,
            CachedAt = DateTime.UtcNow
        };

        SePayCache[cacheKey] = (DateTime.UtcNow.Add(SePayCacheTtl), overview);
        return overview;
    }

    private AdminSePayBankCardDto PickBankAccount(List<SePayBankAccountDto> accounts)
    {
        if (accounts.Count == 0)
        {
            return new AdminSePayBankCardDto
            {
                Available = false,
                Message = "SePay không trả về tài khoản ngân hàng.",
                AccountNumberMasked = MaskAccount(_sePaySettings.BankAccountNumber),
                BankName = _sePaySettings.BankName,
                AccountHolderName = _sePaySettings.BankAccountName
            };
        }

        var configured = _sePaySettings.BankAccountNumber?.Trim();
        var pick = accounts.FirstOrDefault(a =>
                       !string.IsNullOrWhiteSpace(configured)
                       && string.Equals(a.AccountNumber?.Trim(), configured, StringComparison.OrdinalIgnoreCase))
                   ?? accounts.FirstOrDefault(a => a.Active)
                   ?? accounts[0];

        return new AdminSePayBankCardDto
        {
            Available = true,
            BankName = string.IsNullOrWhiteSpace(pick.BankFullName) ? pick.BankShortName : pick.BankFullName,
            AccountHolderName = pick.AccountHolderName,
            AccountNumberMasked = MaskAccount(pick.AccountNumber),
            Accumulated = pick.Accumulated,
            LastTransaction = pick.LastTransaction,
            Active = pick.Active
        };
    }

    private static List<AdminSePayDailyInDto> BuildDailyInLast7Days(List<SePayLiveTransactionDto> txs)
    {
        var todayVn = DateTime.UtcNow.AddHours(7).Date;
        var map = new Dictionary<string, decimal>();
        for (var i = 6; i >= 0; i--)
        {
            var d = todayVn.AddDays(-i);
            map[d.ToString("yyyy-MM-dd")] = 0;
        }

        foreach (var tx in txs)
        {
            if (!TryParseSePayDate(tx.TransactionDate, out var dt)) continue;
            var key = dt.Date.ToString("yyyy-MM-dd");
            if (!map.ContainsKey(key)) continue;
            map[key] += tx.AmountIn;
        }

        return map.Select(kv => new AdminSePayDailyInDto { Date = kv.Key, AmountIn = kv.Value }).ToList();
    }

    private static bool TryParseSePayDate(string? raw, out DateTime dt)
    {
        dt = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return DateTime.TryParse(raw, out dt);
    }

    private static string? ExtractOrderCode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = OrderCodeRegex.Match(text.Replace("-", "").Replace(" ", ""));
        if (m.Success) return m.Value.ToUpperInvariant();
        // Thử trên chuỗi gốc (có dấu '-')
        m = OrderCodeRegex.Match(text);
        return m.Success ? m.Value.ToUpperInvariant() : null;
    }

    private static string NormalizeOrderKey(string orderCode)
        => orderCode.Replace("-", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();

    private static string MaskAccount(string? account)
    {
        if (string.IsNullOrWhiteSpace(account)) return "—";
        var a = account.Trim();
        if (a.Length <= 4) return "****";
        return new string('*', Math.Max(a.Length - 4, 0)) + a[^4..];
    }

    private static SubscriptionPlanDto Map(SubscriptionPlan p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        Audience = p.Audience,
        Name = p.Name,
        PriceMonthly = p.PriceMonthly,
        Currency = p.Currency,
        IsActive = p.IsActive,
        Limits = SubscriptionLimitsHelper.Deserialize(p.LimitsJson),
        AppliesToExistingSubscribersFromNextPeriod = true
    };

    private static string NormalizeAudience(string audience)
    {
        if (string.Equals(audience, SubscriptionAudience.HR, StringComparison.OrdinalIgnoreCase))
            return SubscriptionAudience.HR;
        if (string.Equals(audience, SubscriptionAudience.Candidate, StringComparison.OrdinalIgnoreCase))
            return SubscriptionAudience.Candidate;
        throw new BadRequestException("audience phải là HR hoặc Candidate.");
    }

    /// <summary>SCRUM-412: Free = 0đ; Premium ≥ 10.000đ.</summary>
    private static void ValidatePlanPrice(string code, decimal price)
    {
        var upper = (code ?? "").ToUpperInvariant();
        if (upper.Contains("FREE"))
        {
            if (price != 0)
                throw new BadRequestException("Gói Free phải có giá 0 VNĐ.");
            return;
        }

        if (upper.Contains("PREMIUM") && price < 10000)
            throw new BadRequestException("Gói Premium phải có giá tối thiểu 10.000 VNĐ.");
    }

    // ── SCRUM-386: Admin gán Premium user ─────────────────────────────────

    public async Task<AdminUserSubscriptionDetailDto> GetUserSubscriptionAsync(Guid userId)
    {
        await EnsureGrantableUserAsync(userId);
        var sub = await _metering.GetOrThrowSubscriptionAsync(userId);
        return await BuildUserSubscriptionDetailAsync(sub);
    }

    public async Task<AdminUserSubscriptionDetailDto> GrantPremiumAsync(Guid userId, AdminGrantPremiumDto dto, Guid adminUserId)
    {
        var months = NormalizePeriodMonths(dto.PeriodMonths);
        await EnsureGrantableUserAsync(userId);

        var sub = await _metering.GetOrThrowSubscriptionAsync(userId);
        var audience = await ResolveAudienceAsync(userId, sub);
        var premium = await GetPremiumPlanAsync(audience);

        await ApplyPremiumPeriodAsync(sub, premium, months);

        await _txRepo.AddAsync(new SubscriptionTransaction
        {
            SubscriptionId = sub.Id,
            Type = SubscriptionTransactionTypes.Upgrade,
            Amount = 0,
            Currency = premium.Currency,
            Status = SubscriptionTransactionStatus.Paid,
            Provider = "Admin",
            ConfirmedAt = DateTime.UtcNow,
            Note = BuildAdminNote("Grant Premium", adminUserId, months, dto.Note)
        });

        return await BuildUserSubscriptionDetailAsync(sub);
    }

    public async Task<AdminUserSubscriptionDetailDto> UpdateUserSubscriptionPeriodAsync(
        Guid userId,
        AdminUpdateUserSubscriptionDto dto,
        Guid adminUserId)
    {
        var months = NormalizePeriodMonths(dto.PeriodMonths);
        await EnsureGrantableUserAsync(userId);

        var sub = await _metering.GetOrThrowSubscriptionAsync(userId);
        var plan = sub.Plan ?? await _planRepo.GetByIdAsync(sub.PlanId)
            ?? throw new NotFoundException("Không tìm thấy gói hiện tại.");

        if (plan.Code is not (SubscriptionPlanCodes.HrPremium or SubscriptionPlanCodes.CandidatePremium))
            throw new BadRequestException("User chưa Premium. Dùng activate-premium để gán gói.");

        var premium = plan;
        await ApplyPremiumPeriodAsync(sub, premium, months);

        await _txRepo.AddAsync(new SubscriptionTransaction
        {
            SubscriptionId = sub.Id,
            Type = SubscriptionTransactionTypes.Upgrade,
            Amount = 0,
            Currency = premium.Currency,
            Status = SubscriptionTransactionStatus.Paid,
            Provider = "Admin",
            ConfirmedAt = DateTime.UtcNow,
            Note = BuildAdminNote("Extend Premium period", adminUserId, months, dto.Note)
        });

        return await BuildUserSubscriptionDetailAsync(sub);
    }

    public async Task<AdminUserSubscriptionDetailDto> RevokePremiumAsync(Guid userId, AdminRevokePremiumDto dto, Guid adminUserId)
    {
        await EnsureGrantableUserAsync(userId);

        var sub = await _metering.GetOrThrowSubscriptionAsync(userId);
        var audience = await ResolveAudienceAsync(userId, sub);
        var freeCode = audience == SubscriptionAudience.HR
            ? SubscriptionPlanCodes.HrFree
            : SubscriptionPlanCodes.CandidateFree;
        var free = await _planRepo.GetByCodeAsync(freeCode)
            ?? throw new ServerFailureException($"Chưa seed plan {freeCode}.");

        var plan = sub.Plan ?? await _planRepo.GetByIdAsync(sub.PlanId);
        var alreadyFree = plan?.Code is SubscriptionPlanCodes.HrFree or SubscriptionPlanCodes.CandidateFree;
        if (alreadyFree)
            return await BuildUserSubscriptionDetailAsync(sub);

        var now = DateTime.UtcNow;
        await _usageRepo.DeleteBySubscriptionPeriodAsync(sub.Id, sub.CurrentPeriodStart);

        sub.PlanId = free.Id;
        sub.Plan = free;
        sub.Status = SubscriptionStatus.Active;
        sub.CancelledAt = now;
        sub.CancelAtPeriodEnd = false;
        sub.LimitsSnapshotJson = free.LimitsJson;
        // Hạ Free: bắt đầu kỳ Free mới 1 tháng
        sub.CurrentPeriodStart = now;
        sub.CurrentPeriodEnd = now.AddMonths(1);
        sub.UpdatedAt = now;
        await _subscriptionRepo.UpdateAsync(sub);

        await _txRepo.AddAsync(new SubscriptionTransaction
        {
            SubscriptionId = sub.Id,
            Type = SubscriptionTransactionTypes.Cancel,
            Amount = 0,
            Currency = free.Currency,
            Status = SubscriptionTransactionStatus.Paid,
            Provider = "Admin",
            ConfirmedAt = now,
            Note = BuildAdminNote("Revoke → Free", adminUserId, periodMonths: null, dto.Note)
        });

        return await BuildUserSubscriptionDetailAsync(sub);
    }

    private async Task<AdminUserSubscriptionDetailDto> BuildUserSubscriptionDetailAsync(Subscription sub)
    {
        var subscriptionDto = await BuildMyDtoAsync(sub);
        var txs = await _txRepo.ListBySubscriptionAsync(sub.Id, take: 40);
        return new AdminUserSubscriptionDetailDto
        {
            Subscription = subscriptionDto,
            History = txs.Select(t => new AdminUserSubscriptionHistoryItemDto
            {
                Id = t.Id,
                Type = t.Type,
                Status = t.Status,
                Provider = t.Provider,
                Amount = t.Amount,
                Currency = t.Currency,
                OrderCode = t.OrderCode,
                Note = t.Note,
                CreatedAt = t.CreatedAt,
                ConfirmedAt = t.ConfirmedAt
            }).ToList()
        };
    }

    private async Task EnsureGrantableUserAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAnyStatusAsync(userId)
            ?? throw new NotFoundException("Không tìm thấy user.");
        if (user.RoleId == UserRole.AdminId)
            throw new BadRequestException("Không gán subscription cho tài khoản Admin.");
    }

    private static int NormalizePeriodMonths(int periodMonths)
    {
        if (Array.IndexOf(AllowedPeriodMonths, periodMonths) < 0)
            throw new BadRequestException("periodMonths phải là 1, 3, 6 hoặc 12.");
        return periodMonths;
    }

    private async Task ApplyPremiumPeriodAsync(Subscription sub, SubscriptionPlan premium, int months)
    {
        var now = DateTime.UtcNow;
        // Xóa usage kỳ cũ để counter khớp period mới
        await _usageRepo.DeleteBySubscriptionPeriodAsync(sub.Id, sub.CurrentPeriodStart);

        sub.PlanId = premium.Id;
        sub.Plan = premium;
        sub.Status = SubscriptionStatus.Active;
        sub.CancelledAt = null;
        sub.CancelAtPeriodEnd = false;
        sub.LimitsSnapshotJson = premium.LimitsJson;
        sub.CurrentPeriodStart = now;
        sub.CurrentPeriodEnd = now.AddMonths(months);
        sub.UpdatedAt = now;
        await _subscriptionRepo.UpdateAsync(sub);
    }

    private async Task<SubscriptionPlan> GetPremiumPlanAsync(string audience)
    {
        var premiumCode = audience == SubscriptionAudience.HR
            ? SubscriptionPlanCodes.HrPremium
            : SubscriptionPlanCodes.CandidatePremium;
        return await _planRepo.GetByCodeAsync(premiumCode)
            ?? throw new ServerFailureException($"Chưa seed plan {premiumCode}.");
    }

    private async Task<string> ResolveAudienceAsync(Guid userId, Subscription sub)
    {
        var user = await _userRepo.GetByIdAnyStatusAsync(userId);
        if (user?.RoleId == UserRole.HRId)
            return SubscriptionAudience.HR;
        if (user?.RoleId == UserRole.CandidateId)
            return SubscriptionAudience.Candidate;

        var plan = sub.Plan ?? await _planRepo.GetByIdAsync(sub.PlanId);
        if (plan != null && !string.IsNullOrWhiteSpace(plan.Audience))
            return plan.Audience;

        return SubscriptionAudience.Candidate;
    }

    private static string BuildAdminNote(string action, Guid adminUserId, int? periodMonths, string? note)
    {
        var parts = new List<string> { action, $"admin={adminUserId:N}" };
        if (periodMonths.HasValue)
            parts.Add($"{periodMonths}m");
        if (!string.IsNullOrWhiteSpace(note))
        {
            var cleaned = note.Trim();
            if (cleaned.Length > 400)
                cleaned = cleaned[..400];
            parts.Add(cleaned);
        }
        return string.Join(" | ", parts);
    }

    private async Task<MySubscriptionDto> BuildMyDtoAsync(Subscription sub)
    {
        var plan = sub.Plan ?? await _planRepo.GetByIdAsync(sub.PlanId)
            ?? throw new NotFoundException("Không tìm thấy gói.");
        var limits = SubscriptionLimitsHelper.Deserialize(sub.LimitsSnapshotJson);
        var askAi = await _metering.GetUsageAsync(sub.UserId, UsageType.HrAskAi);
        var generate = await _metering.GetUsageAsync(sub.UserId, UsageType.HrGenerateSet);

        return new MySubscriptionDto
        {
            PlanCode = plan.Code,
            PlanName = plan.Name,
            Audience = plan.Audience,
            Status = sub.Status,
            PriceMonthly = plan.PriceMonthly,
            Currency = plan.Currency,
            PeriodStart = sub.CurrentPeriodStart,
            PeriodEnd = sub.CurrentPeriodEnd,
            CancelAtPeriodEnd = sub.CancelAtPeriodEnd,
            LastSuccessfulGenerateAt = sub.LastSuccessfulGenerateAt,
            Limits = limits,
            AskAiUsed = askAi.UsedCount,
            AskAiLimit = askAi.EffectiveLimit,
            GenerateSetUsed = generate.UsedCount,
            Entitlements = new SubscriptionEntitlementsDto
            {
                CanExport = limits.CanExport,
                CanAskAi = limits.AskAiPerMonth > 0,
                GenerateUnlimited = limits.GenerateUnlimited,
                FreeVisiblePercent = limits.FreeVisiblePercent,
                CanPersistHrRecommendation = limits.CanPersistHrRecommendation
            }
        };
    }
}
