using ApplicationLayer.DTOs.Gamification;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Gamification;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using InfrastructureLayer.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace InfrastructureLayer.Services.Gamification;

/// <summary>
/// Orchestrator duy nhất ghi UserProgress/XpTransaction/DailyProgress/UserAchievement — theo pattern
/// đã có sẵn cho các thao tác atomic đa-entity trong repo này (xem InterviewProjectService/StudioCoreServices):
/// inject thẳng AppDbContext, dùng transaction tường minh thay vì tách repository pattern.
///
/// CONCURRENCY: mỗi lần award XP lock đúng 1 row tbl_user_progress của user đó bằng
/// "SELECT ... FOR UPDATE" trong transaction — mọi request award XP đồng thời của CÙNG user bị serialize
/// tại điểm này (không phải table lock, các user khác không bị ảnh hưởng). Nhờ vậy toàn bộ update
/// TotalXp/Level/Streak/DailyProgress/Achievement trong 1 lần award là read-modify-write nhất quán,
/// không cần optimistic concurrency + retry loop. AppDbContext không bật EnableRetryOnFailure nên
/// transaction tường minh ở đây an toàn (không bị execution-strategy nuốt).
///
/// IDEMPOTENCY: XpTransaction.IdempotencyKey có UNIQUE index ở DB — đây là chốt chặn cuối cùng.
/// Vì check-tồn-tại luôn chạy SAU khi đã lock row user, race giữa 2 request trùng idempotency key của
/// CÙNG user (vd double-submit) không thể xảy ra trong thực tế; UNIQUE index chỉ còn là an toàn dự phòng.
/// </summary>
public sealed class GamificationService(
    AppDbContext dbContext,
    ILevelCalculator levelCalculator,
    IXpRewardPolicy rewardPolicy,
    IStreakCalculator streakCalculator,
    IUserLocalDateProvider localDateProvider,
    IEnumerable<IAchievementRule> achievementRules,
    IOptions<GamificationOptions> options,
    ILogger<GamificationService> logger) : IGamificationService
{
    private readonly GamificationOptions _options = options.Value;

    // ── Award entry points (chỉ gọi từ CandidatePracticeSessionService — không public XP API) ──

    public async Task<XpRewardDto?> AwardQuestionCompletionAsync(QuestionCompletionXpContext context)
    {
        var items = new List<PendingXpItem>
        {
            new(XpTransactionType.QuestionCompleted, rewardPolicy.QuestionCompletedXp,
                $"question-completed:{context.CandidateAnswerId}")
        };

        var scoreBonus = rewardPolicy.GetScoreBonus(context.Score);
        if (scoreBonus > 0)
            items.Add(new PendingXpItem(XpTransactionType.ScoreBonus, scoreBonus,
                $"score-bonus:{context.CandidateAnswerId}"));

        if (context.PreviousBestScore.HasValue
            && rewardPolicy.IsImprovementEligible(context.PreviousBestScore.Value, context.Score))
        {
            items.Add(new PendingXpItem(XpTransactionType.ImprovementBonus, rewardPolicy.ImprovementBonusXp,
                $"improvement-bonus:{context.CandidateAnswerId}"));
        }

        return await AwardCoreAsync(new AwardCoreRequest
        {
            UserId = context.UserId,
            Items = items,
            OccurredAtUtc = context.OccurredAtUtc,
            QuestionId = context.QuestionSetQuestionId,
            QuestionAttemptId = context.CandidateAnswerId,
            QuestionSetId = context.QuestionSetId,
            PracticeSessionId = context.PracticeSessionId,
            IsQuestionCompletionEvent = true,
            QuestionScore = context.Score,
            QuestionType = context.QuestionType
        });
    }

    public async Task<XpRewardDto?> AwardQuestionSetCompletionAsync(QuestionSetCompletionXpContext context)
    {
        var items = new List<PendingXpItem>
        {
            new(XpTransactionType.QuestionSetCompleted, rewardPolicy.QuestionSetCompletedXp,
                $"question-set-completed:{context.PracticeSessionId}")
        };

        return await AwardCoreAsync(new AwardCoreRequest
        {
            UserId = context.UserId,
            Items = items,
            OccurredAtUtc = context.OccurredAtUtc,
            QuestionSetId = context.QuestionSetId,
            PracticeSessionId = context.PracticeSessionId,
            IsQuestionCompletionEvent = false
        });
    }

    // ── Core atomic award flow ───────────────────────────────────────

    private async Task<XpRewardDto?> AwardCoreAsync(AwardCoreRequest request)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var progress = await GetOrCreateLockedProgressAsync(request.UserId, tx);

            // Lọc bỏ item đã award trước đó — an toàn vì đã lock row user ở bước trên.
            var keys = request.Items.Select(i => i.IdempotencyKey).ToList();
            var existingKeys = (await dbContext.XpTransactions
                .Where(t => t.UserId == request.UserId && keys.Contains(t.IdempotencyKey))
                .Select(t => t.IdempotencyKey)
                .ToListAsync())
                .ToHashSet();

            var pendingItems = request.Items.Where(i => !existingKeys.Contains(i.IdempotencyKey)).ToList();

            // ── Streak — chỉ tính cho sự kiện QuestionCompleted (câu hỏi hoàn thành hợp lệ) ──
            DateOnly? localDate = null;
            StreakResult? streak = null;
            if (request.IsQuestionCompletionEvent)
            {
                localDate = await localDateProvider.GetLocalDateAsync(request.UserId, request.OccurredAtUtc);
                streak = streakCalculator.Compute(
                    progress.LastActiveLocalDate, progress.CurrentStreak, progress.LongestStreak, localDate.Value);

                if (streak.Value.IsNewActiveDay
                    && rewardPolicy.TryGetStreakMilestoneXp(streak.Value.CurrentStreak, out var milestoneXp))
                {
                    var milestoneKey =
                        $"streak-milestone:{request.UserId}:{localDate:yyyy-MM-dd}:{streak.Value.CurrentStreak}";
                    if (!existingKeys.Contains(milestoneKey))
                        pendingItems.Add(new PendingXpItem(XpTransactionType.StreakMilestone, milestoneXp, milestoneKey));
                }
            }

            if (pendingItems.Count == 0)
            {
                // Toàn bộ sự kiện đã được award trước đó (retry/duplicate) — không có gì mới.
                await tx.CommitAsync();
                return null;
            }

            var previousTotalXp = progress.TotalXp;
            var previousLevel = progress.Level;

            foreach (var item in pendingItems)
            {
                dbContext.XpTransactions.Add(new XpTransaction
                {
                    UserId = request.UserId,
                    Amount = item.Amount,
                    Type = item.Type,
                    QuestionId = request.QuestionId,
                    QuestionAttemptId = request.QuestionAttemptId,
                    QuestionSetId = request.QuestionSetId,
                    PracticeSessionId = request.PracticeSessionId,
                    Description = GamificationLabelKeys.ForType(item.Type),
                    IdempotencyKey = item.IdempotencyKey,
                    CreatedAtUtc = request.OccurredAtUtc
                });
            }

            var delta = pendingItems.Sum(i => i.Amount);
            var newTotalXp = previousTotalXp + delta;
            var newLevel = levelCalculator.CalculateLevel(newTotalXp);

            progress.TotalXp = newTotalXp;
            progress.Level = newLevel;

            var questionCompletedAwarded = pendingItems.Any(i => i.Type == XpTransactionType.QuestionCompleted);
            var setCompletedAwarded = pendingItems.Any(i => i.Type == XpTransactionType.QuestionSetCompleted);

            if (questionCompletedAwarded)
            {
                progress.TotalQuestionsCompleted += 1;
                var normalizedType = QuestionTypeNormalizer.NormalizeOne(request.QuestionType);
                if (normalizedType == "technical") progress.TotalTechnicalQuestionsCompleted += 1;
                else if (normalizedType == "system-design") progress.TotalSystemDesignQuestionsCompleted += 1;
            }

            if (setCompletedAwarded)
                progress.TotalSessionsCompleted += 1;

            if (streak is { IsNewActiveDay: true })
            {
                progress.CurrentStreak = streak.Value.CurrentStreak;
                progress.LongestStreak = streak.Value.LongestStreak;
                progress.LastActiveLocalDate = localDate;
            }

            progress.UpdatedAt = DateTime.UtcNow;

            // ── DailyProgress upsert — an toàn không cần lock riêng vì đã serialize theo user ở trên ──
            var progressLocalDate = localDate ?? await localDateProvider.GetLocalDateAsync(request.UserId, request.OccurredAtUtc);
            var daily = await dbContext.DailyProgresses
                .FirstOrDefaultAsync(d => d.UserId == request.UserId && d.LocalDate == progressLocalDate);
            if (daily is null)
            {
                daily = new DailyProgress { UserId = request.UserId, LocalDate = progressLocalDate };
                dbContext.DailyProgresses.Add(daily);
            }

            daily.XpEarned += delta;
            if (questionCompletedAwarded) daily.QuestionsCompleted += 1;
            if (setCompletedAwarded) daily.SessionsCompleted += 1;
            daily.UpdatedAt = DateTime.UtcNow;

            if (!daily.DailyGoalCompleted && daily.XpEarned >= progress.DailyGoalXp)
            {
                daily.DailyGoalCompleted = true;
                progress.DailyGoalCompletedDaysCount += 1;
            }

            await dbContext.SaveChangesAsync();

            // ── Achievements — strategy pattern, mỗi rule tự quyết dựa trên snapshot vừa update ──
            var alreadyUnlocked = (await dbContext.UserAchievements
                .Where(a => a.UserId == request.UserId)
                .Select(a => a.AchievementCode)
                .ToListAsync())
                .ToHashSet();

            var achievementContext = new AchievementEvaluationContext
            {
                UserId = request.UserId,
                CurrentStreak = progress.CurrentStreak,
                TotalQuestionsCompleted = progress.TotalQuestionsCompleted,
                TotalSessionsCompleted = progress.TotalSessionsCompleted,
                TotalTechnicalQuestionsCompleted = progress.TotalTechnicalQuestionsCompleted,
                TotalSystemDesignQuestionsCompleted = progress.TotalSystemDesignQuestionsCompleted,
                DailyGoalCompletedDaysCount = progress.DailyGoalCompletedDaysCount,
                LastQuestionScore = request.IsQuestionCompletionEvent ? request.QuestionScore : null
            };

            var unlockedCodes = new List<string>();
            foreach (var rule in achievementRules)
            {
                if (alreadyUnlocked.Contains(rule.Code)) continue;
                if (!rule.IsUnlocked(achievementContext)) continue;

                dbContext.UserAchievements.Add(new UserAchievement
                {
                    UserId = request.UserId,
                    AchievementCode = rule.Code,
                    UnlockedAtUtc = DateTime.UtcNow
                });
                unlockedCodes.Add(rule.Code);
            }

            if (unlockedCodes.Count > 0)
                await dbContext.SaveChangesAsync();

            await tx.CommitAsync();

            logger.LogInformation(
                "Gamification XP awarded: UserId={UserId} Types={Types} Amount={Amount} PreviousXp={PreviousXp} CurrentXp={CurrentXp} LevelUp={LevelUp} IdempotencyKeys={IdempotencyKeys}",
                request.UserId,
                string.Join(",", pendingItems.Select(i => i.Type)),
                delta, previousTotalXp, newTotalXp, newLevel > previousLevel,
                string.Join(",", pendingItems.Select(i => i.IdempotencyKey)));

            return new XpRewardDto
            {
                TotalEarned = delta,
                Rewards = pendingItems.Select(i => new XpRewardItemDto
                {
                    Type = i.Type,
                    Xp = i.Amount,
                    LabelKey = GamificationLabelKeys.ForType(i.Type)
                }).ToList(),
                PreviousLevel = previousLevel,
                CurrentLevel = newLevel,
                PreviousTotalXp = previousTotalXp,
                CurrentTotalXp = newTotalXp,
                LevelUp = newLevel > previousLevel,
                UnlockedAchievementCodes = unlockedCodes,
                Progress = MapToProgressDto(progress, daily.XpEarned, daily.DailyGoalCompleted)
            };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// SELECT ... FOR UPDATE để lock row của user (khoá theo user, không phải toàn bảng).
    /// Nếu chưa có row (candidate chưa từng award XP — bao gồm cả candidate có từ trước khi tính năng
    /// này ra mắt), tạo mới lazily. Race hiếm gặp giữa 2 request cùng tạo row lần đầu cho cùng user
    /// được xử lý bằng SAVEPOINT: request thua cuộc rollback về savepoint rồi SELECT FOR UPDATE lại
    /// (lúc này row đã tồn tại, sẽ chờ lock rồi đọc được).
    /// </summary>
    private async Task<UserProgress> GetOrCreateLockedProgressAsync(Guid userId, IDbContextTransaction tx)
    {
        var existing = await LockProgressRowAsync(userId);
        if (existing is not null) return existing;

        const string savepoint = "gamification_create_progress";
        await tx.CreateSavepointAsync(savepoint);
        var candidate = new UserProgress { UserId = userId, DailyGoalXp = _options.DefaultDailyGoalXp };
        dbContext.UserProgresses.Add(candidate);
        try
        {
            await dbContext.SaveChangesAsync();
            return candidate;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            dbContext.Entry(candidate).State = EntityState.Detached;
            await tx.RollbackToSavepointAsync(savepoint);

            // Request khác đã tạo row trước — chờ lock rồi đọc lại, chắc chắn tồn tại lần này.
            return await LockProgressRowAsync(userId)
                ?? throw new InvalidOperationException(
                    $"UserProgress cho user {userId} phải tồn tại sau khi rollback savepoint do unique violation.");
        }
    }

    private async Task<UserProgress?> LockProgressRowAsync(Guid userId)
    {
        var rows = await dbContext.UserProgresses
            .FromSqlInterpolated(
                $"SELECT * FROM tbl_user_progress WHERE \"UserId\" = {userId} AND \"IsActive\" = TRUE FOR UPDATE")
            .ToListAsync();
        return rows.FirstOrDefault();
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    // ── Read queries (GET endpoints) ─────────────────────────────────

    public async Task<UserProgressDto> GetProgressAsync(Guid userId)
    {
        var progress = await dbContext.UserProgresses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        var today = await localDateProvider.GetLocalDateAsync(userId, DateTime.UtcNow);
        var daily = await dbContext.DailyProgresses.AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == userId && d.LocalDate == today);

        return progress is null
            ? BuildDefaultProgressDto(daily?.XpEarned ?? 0, daily?.DailyGoalCompleted ?? false)
            : MapToProgressDto(progress, daily?.XpEarned ?? 0, daily?.DailyGoalCompleted ?? false);
    }

    public async Task<IReadOnlyList<DailyActivityDto>> GetActivityAsync(Guid userId, DateOnly from, DateOnly to)
    {
        if (from > to)
            throw new BadRequestException("from không được lớn hơn to.");

        var totalDays = to.DayNumber - from.DayNumber + 1;
        if (totalDays > _options.MaxActivityRangeDays)
            throw new BadRequestException($"Khoảng ngày yêu cầu tối đa {_options.MaxActivityRangeDays} ngày.");

        var rows = await dbContext.DailyProgresses.AsNoTracking()
            .Where(d => d.UserId == userId && d.LocalDate >= from && d.LocalDate <= to)
            .ToListAsync();
        var byDate = rows.ToDictionary(r => r.LocalDate);

        var result = new List<DailyActivityDto>(totalDays);
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (byDate.TryGetValue(date, out var row))
            {
                result.Add(new DailyActivityDto
                {
                    Date = date,
                    Xp = row.XpEarned,
                    QuestionsCompleted = row.QuestionsCompleted,
                    SessionsCompleted = row.SessionsCompleted,
                    Active = row.QuestionsCompleted > 0
                });
            }
            else
            {
                result.Add(new DailyActivityDto { Date = date, Xp = 0, QuestionsCompleted = 0, SessionsCompleted = 0, Active = false });
            }
        }

        return result;
    }

    public async Task<PagedResultDto<XpHistoryItemDto>> GetXpHistoryAsync(Guid userId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.XpTransactions.AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAtUtc);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new XpHistoryItemDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Type = t.Type,
                DescriptionKey = t.Description,
                CreatedAtUtc = t.CreatedAtUtc
            })
            .ToListAsync();

        return new PagedResultDto<XpHistoryItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<AchievementDto>> GetAchievementsAsync(Guid userId)
    {
        var unlocked = await dbContext.UserAchievements.AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToDictionaryAsync(a => a.AchievementCode, a => a.UnlockedAtUtc);

        return achievementRules
            .Select(rule => new AchievementDto
            {
                Code = rule.Code,
                Unlocked = unlocked.ContainsKey(rule.Code),
                UnlockedAtUtc = unlocked.TryGetValue(rule.Code, out var at) ? at : null
            })
            .ToList();
    }

    public async Task<UserProgressDto> UpdateDailyGoalAsync(Guid userId, int dailyGoalXp)
    {
        if (!_options.AllowedDailyGoalXpValues.Contains(dailyGoalXp))
        {
            throw new BadRequestException(
                $"dailyGoalXp phải là một trong các giá trị: {string.Join(", ", _options.AllowedDailyGoalXpValues)}.");
        }

        var progress = await dbContext.UserProgresses.FirstOrDefaultAsync(p => p.UserId == userId);
        if (progress is null)
        {
            progress = new UserProgress { UserId = userId, DailyGoalXp = dailyGoalXp };
            dbContext.UserProgresses.Add(progress);
        }
        else
        {
            progress.DailyGoalXp = dailyGoalXp;
            progress.UpdatedAt = DateTime.UtcNow;
        }

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Hiếm: request khác vừa tạo row đầu tiên cho user này trước — đọc lại rồi set field.
            dbContext.Entry(progress).State = EntityState.Detached;
            progress = await dbContext.UserProgresses.FirstAsync(p => p.UserId == userId);
            progress.DailyGoalXp = dailyGoalXp;
            progress.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        var today = await localDateProvider.GetLocalDateAsync(userId, DateTime.UtcNow);
        var daily = await dbContext.DailyProgresses.AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == userId && d.LocalDate == today);

        return MapToProgressDto(progress, daily?.XpEarned ?? 0, daily?.DailyGoalCompleted ?? false);
    }

    // ── Mapping ───────────────────────────────────────────────────────

    private UserProgressDto MapToProgressDto(UserProgress progress, int todayXp, bool dailyGoalCompleted) => new()
    {
        TotalXp = progress.TotalXp,
        Level = progress.Level,
        CurrentLevelXp = levelCalculator.GetCurrentLevelXp(progress.TotalXp),
        XpRequiredForNextLevel = levelCalculator.GetXpRequiredForNextLevel(progress.Level),
        ProgressPercentage = levelCalculator.GetProgressPercentage(progress.TotalXp),
        CurrentStreak = progress.CurrentStreak,
        LongestStreak = progress.LongestStreak,
        DailyGoalXp = progress.DailyGoalXp,
        TodayXp = todayXp,
        DailyGoalCompleted = dailyGoalCompleted,
        TotalPracticeSessions = progress.TotalSessionsCompleted,
        AllowedDailyGoalXpValues = _options.AllowedDailyGoalXpValues
    };

    private UserProgressDto BuildDefaultProgressDto(int todayXp, bool dailyGoalCompleted) => new()
    {
        TotalXp = 0,
        Level = 1,
        CurrentLevelXp = 0,
        XpRequiredForNextLevel = levelCalculator.GetXpRequiredForNextLevel(1),
        ProgressPercentage = 0,
        CurrentStreak = 0,
        LongestStreak = 0,
        DailyGoalXp = _options.DefaultDailyGoalXp,
        TodayXp = todayXp,
        DailyGoalCompleted = dailyGoalCompleted,
        TotalPracticeSessions = 0,
        AllowedDailyGoalXpValues = _options.AllowedDailyGoalXpValues
    };

    private readonly record struct PendingXpItem(string Type, int Amount, string IdempotencyKey);

    private sealed class AwardCoreRequest
    {
        public required Guid UserId { get; init; }
        public required List<PendingXpItem> Items { get; init; }
        public required DateTime OccurredAtUtc { get; init; }
        public Guid? QuestionId { get; init; }
        public Guid? QuestionAttemptId { get; init; }
        public Guid? QuestionSetId { get; init; }
        public Guid? PracticeSessionId { get; init; }
        public required bool IsQuestionCompletionEvent { get; init; }
        public double? QuestionScore { get; init; }
        public string? QuestionType { get; init; }
    }
}
