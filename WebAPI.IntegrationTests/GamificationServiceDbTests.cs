using ApplicationLayer.DTOs.Gamification;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Gamification;
using ApplicationLayer.Services.Gamification.AchievementRules;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using DomainLayer.Entities;
using InfrastructureLayer.Database;
using InfrastructureLayer.Repository;
using InfrastructureLayer.Services.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebAPI.IntegrationTests;

/// <summary>
/// Test GamificationService trực tiếp trên Postgres thật (không mock DbContext) — mục tiêu là xác nhận
/// idempotency (UNIQUE index + row lock) và concurrency (SELECT ... FOR UPDATE) hoạt động đúng ở tầng SQL
/// thật, không chỉ đúng về logic C#. Cần 1 Postgres khả dụng qua biến môi trường
/// ConnectionStrings__DefaultConnection (giống StudioSmokeTests) — nếu không có, các test tự bỏ qua
/// (return sớm) thay vì fail CI, theo đúng convention "return sớm khi môi trường thiếu setup" đã dùng ở
/// StudioSmokeTests.Studio_BasicAuthenticatedFlow_ShouldWork.
/// </summary>
public sealed class GamificationServiceDbTests : IClassFixture<GamificationDbFixture>
{
    private readonly GamificationDbFixture _fixture;

    public GamificationServiceDbTests(GamificationDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AwardQuestionCompletion_CalledTwiceWithSameAnswerId_OnlyAwardsOnce()
    {
        if (!_fixture.IsAvailable) return;

        await using var scope = _fixture.CreateScope();
        var gamification = scope.ServiceProvider.GetRequiredService<IGamificationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await CreateTestUserAsync(db);

        var context = new QuestionCompletionXpContext
        {
            UserId = userId,
            QuestionSetQuestionId = Guid.NewGuid(),
            CandidateAnswerId = Guid.NewGuid(),
            QuestionSetId = Guid.NewGuid(),
            PracticeSessionId = Guid.NewGuid(),
            Score = 80, // QuestionCompleted(10) + ScoreBonus(4) = 14
            QuestionType = "Technical",
            PreviousBestScore = null,
            OccurredAtUtc = DateTime.UtcNow
        };

        var first = await gamification.AwardQuestionCompletionAsync(context);
        var second = await gamification.AwardQuestionCompletionAsync(context); // retry/duplicate — cùng CandidateAnswerId

        Assert.NotNull(first);
        Assert.Equal(14, first!.TotalEarned);
        Assert.Null(second); // toàn bộ item đã được award ở lần gọi đầu — không có gì mới

        var progress = await gamification.GetProgressAsync(userId);
        Assert.Equal(14, progress.TotalXp); // KHÔNG bị cộng 2 lần
    }

    [Fact]
    public async Task ConcurrentAwards_ForSameUser_DoNotLoseUpdates()
    {
        if (!_fixture.IsAvailable) return;

        await using var seedScope = _fixture.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await CreateTestUserAsync(seedDb);

        var questionSetId = Guid.NewGuid();
        var contextA = new QuestionCompletionXpContext
        {
            UserId = userId,
            QuestionSetQuestionId = Guid.NewGuid(),
            CandidateAnswerId = Guid.NewGuid(),
            QuestionSetId = questionSetId,
            PracticeSessionId = Guid.NewGuid(),
            Score = 60, // 10 + 2 = 12
            QuestionType = "Technical",
            OccurredAtUtc = DateTime.UtcNow
        };
        var contextB = new QuestionCompletionXpContext
        {
            UserId = userId,
            QuestionSetQuestionId = Guid.NewGuid(),
            CandidateAnswerId = Guid.NewGuid(),
            QuestionSetId = questionSetId,
            PracticeSessionId = Guid.NewGuid(),
            Score = 95, // 10 + 6 = 16
            QuestionType = "Behavioral",
            OccurredAtUtc = DateTime.UtcNow
        };

        // Mỗi task dùng scope/DbContext RIÊNG — mô phỏng 2 request HTTP đồng thời của cùng 1 candidate.
        var taskA = Task.Run(async () =>
        {
            await using var scopeA = _fixture.CreateScope();
            var svc = scopeA.ServiceProvider.GetRequiredService<IGamificationService>();
            return await svc.AwardQuestionCompletionAsync(contextA);
        });
        var taskB = Task.Run(async () =>
        {
            await using var scopeB = _fixture.CreateScope();
            var svc = scopeB.ServiceProvider.GetRequiredService<IGamificationService>();
            return await svc.AwardQuestionCompletionAsync(contextB);
        });

        await Task.WhenAll(taskA, taskB);

        await using var verifyScope = _fixture.CreateScope();
        var gamification = verifyScope.ServiceProvider.GetRequiredService<IGamificationService>();
        var progress = await gamification.GetProgressAsync(userId);

        // Row lock (FOR UPDATE) đảm bảo cả 2 update không ghi đè nhau — tổng phải là 12 + 16 = 28.
        Assert.Equal(28, progress.TotalXp);
    }

    [Fact]
    public async Task AwardQuestionSetCompletion_CalledTwiceWithSameSessionId_OnlyAwardsOnceAndAchievementUnlocksOnce()
    {
        if (!_fixture.IsAvailable) return;

        await using var scope = _fixture.CreateScope();
        var gamification = scope.ServiceProvider.GetRequiredService<IGamificationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await CreateTestUserAsync(db);

        var context = new QuestionSetCompletionXpContext
        {
            UserId = userId,
            PracticeSessionId = Guid.NewGuid(),
            QuestionSetId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow
        };

        var first = await gamification.AwardQuestionSetCompletionAsync(context);
        var second = await gamification.AwardQuestionSetCompletionAsync(context);

        Assert.NotNull(first);
        Assert.Equal(20, first!.TotalEarned);
        Assert.Contains(AchievementCode.FirstStep, first.UnlockedAchievementCodes);
        Assert.Null(second);

        var achievements = await gamification.GetAchievementsAsync(userId);
        var firstStep = achievements.Single(a => a.Code == AchievementCode.FirstStep);
        Assert.True(firstStep.Unlocked);

        // Không có duplicate UserAchievement row — unique index (UserId, AchievementCode) sẽ throw nếu có.
        var count = await db.UserAchievements.CountAsync(a => a.UserId == userId && a.AchievementCode == AchievementCode.FirstStep);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ImprovementBonus_OnlyAwardedWhenDeltaAtLeast10Points()
    {
        if (!_fixture.IsAvailable) return;

        await using var scope = _fixture.CreateScope();
        var gamification = scope.ServiceProvider.GetRequiredService<IGamificationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await CreateTestUserAsync(db);
        var questionSetQuestionId = Guid.NewGuid();

        // +9 điểm — chưa đủ ngưỡng improvement bonus.
        var notEnough = await gamification.AwardQuestionCompletionAsync(new QuestionCompletionXpContext
        {
            UserId = userId,
            QuestionSetQuestionId = questionSetQuestionId,
            CandidateAnswerId = Guid.NewGuid(),
            QuestionSetId = Guid.NewGuid(),
            PracticeSessionId = Guid.NewGuid(),
            Score = 70,
            QuestionType = "Technical",
            PreviousBestScore = 61,
            OccurredAtUtc = DateTime.UtcNow
        });
        Assert.DoesNotContain(notEnough!.Rewards, r => r.Type == XpTransactionType.ImprovementBonus);

        // +10 điểm — đủ ngưỡng.
        var enough = await gamification.AwardQuestionCompletionAsync(new QuestionCompletionXpContext
        {
            UserId = userId,
            QuestionSetQuestionId = questionSetQuestionId,
            CandidateAnswerId = Guid.NewGuid(),
            QuestionSetId = Guid.NewGuid(),
            PracticeSessionId = Guid.NewGuid(),
            Score = 73,
            QuestionType = "Technical",
            PreviousBestScore = 63,
            OccurredAtUtc = DateTime.UtcNow
        });
        Assert.Contains(enough!.Rewards, r => r.Type == XpTransactionType.ImprovementBonus && r.Xp == 5);
    }

    [Fact]
    public async Task GetProgress_ReturnsAllowedDailyGoalXpValuesFromOptions()
    {
        if (!_fixture.IsAvailable) return;

        await using var scope = _fixture.CreateScope();
        var gamification = scope.ServiceProvider.GetRequiredService<IGamificationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await CreateTestUserAsync(db);

        // Không có UserProgress row nào cho user này — kiểm tra cả nhánh default lẫn nhánh đã có progress.
        var beforeAnyXp = await gamification.GetProgressAsync(userId);
        Assert.Equal(new[] { 20, 50, 80, 120 }, beforeAnyXp.AllowedDailyGoalXpValues);

        await gamification.AwardQuestionSetCompletionAsync(new QuestionSetCompletionXpContext
        {
            UserId = userId,
            PracticeSessionId = Guid.NewGuid(),
            QuestionSetId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow
        });

        var afterXp = await gamification.GetProgressAsync(userId);
        Assert.Equal(new[] { 20, 50, 80, 120 }, afterXp.AllowedDailyGoalXpValues);
    }

    [Fact]
    public async Task AwardQuestionCompletion_UsesCandidateTimeZone_ForStreakLocalDate()
    {
        if (!_fixture.IsAvailable) return;

        await using var scope = _fixture.CreateScope();
        var gamification = scope.ServiceProvider.GetRequiredService<IGamificationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await CreateTestUserAsync(db);
        db.CandidateProfiles.Add(new CandidateProfile { UserId = userId, TimeZoneId = "Asia/Ho_Chi_Minh" });
        await db.SaveChangesAsync();

        // 2026-08-09 20:00 UTC = 2026-08-10 03:00 giờ VN (UTC+7) — cùng 1 UTC-instant nhưng khác local date.
        var occurredAtUtc = new DateTime(2026, 8, 9, 20, 0, 0, DateTimeKind.Utc);

        var reward = await gamification.AwardQuestionCompletionAsync(new QuestionCompletionXpContext
        {
            UserId = userId,
            QuestionSetQuestionId = Guid.NewGuid(),
            CandidateAnswerId = Guid.NewGuid(),
            QuestionSetId = Guid.NewGuid(),
            PracticeSessionId = Guid.NewGuid(),
            Score = 100,
            QuestionType = "Technical",
            OccurredAtUtc = occurredAtUtc
        });

        Assert.NotNull(reward);
        Assert.Equal(1, reward!.Progress.CurrentStreak);

        var localDate = new DateOnly(2026, 8, 10); // ngày theo giờ VN, KHÔNG phải 2026-08-09 (ngày UTC)
        var daily = await db.DailyProgresses.SingleAsync(d => d.UserId == userId && d.LocalDate == localDate);
        Assert.Equal(1, daily.QuestionsCompleted);
    }

    private static async Task<Guid> CreateTestUserAsync(AppDbContext db)
    {
        var user = new User
        {
            FullName = "Gamification Test Candidate",
            Email = $"gamification-test-{Guid.NewGuid():N}@example.com",
            RoleId = DomainLayer.Constants.UserRole.CandidateId,
            Provider = "local",
            IsEmailVerified = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }
}

/// <summary>
/// Khởi tạo schema (EnsureCreated — bỏ qua migration history cũ để tránh phụ thuộc migration hiện có)
/// trên Postgres thật trỏ bởi ConnectionStrings__DefaultConnection, và wire riêng các service Gamification
/// (không cần boot toàn bộ WebAPI host — nhanh hơn, không phụ thuộc JWT/seed/DatabaseSeeder).
/// </summary>
public sealed class GamificationDbFixture : IAsyncLifetime
{
    private ServiceProvider? _provider;
    public bool IsAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = "Host=localhost;Port=5432;Database=iqgs_gamification_test;Username=postgres;Password=postgres";

        try
        {
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));
            services.Configure<GamificationOptions>(_ => { });
            services.AddScoped<ILevelCalculator, LevelCalculator>();
            services.AddScoped<IXpRewardPolicy, XpRewardPolicy>();
            services.AddScoped<IStreakCalculator, StreakCalculator>();
            services.AddScoped<ICandidateProfileRepository, CandidateProfileRepository>();
            services.AddScoped<IUserLocalDateProvider, UserLocalDateProvider>();
            services.AddScoped<IAchievementRule, FirstStepAchievementRule>();
            services.AddScoped<IAchievementRule, OnFireAchievementRule>();
            services.AddScoped<IAchievementRule, ExcellentAnswerAchievementRule>();
            services.AddScoped<IAchievementRule, DedicatedAchievementRule>();
            services.AddScoped<IAchievementRule, TechnicalMindAchievementRule>();
            services.AddScoped<IAchievementRule, SystemThinkerAchievementRule>();
            services.AddScoped<IAchievementRule, ConsistencyAchievementRule>();
            services.AddScoped<IAchievementRule, InterviewVeteranAchievementRule>();
            services.AddScoped<IGamificationService, GamificationService>();
            services.AddLogging();

            _provider = services.BuildServiceProvider();

            await using var scope = _provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            IsAvailable = true;
        }
        catch
        {
            // Môi trường không có Postgres khả dụng — các test sẽ tự bỏ qua (return sớm), không fail CI.
            IsAvailable = false;
        }
    }

    public AsyncServiceScope CreateScope()
        => (_provider ?? throw new InvalidOperationException("Fixture chưa init.")).CreateAsyncScope();

    public Task DisposeAsync()
    {
        _provider?.Dispose();
        return Task.CompletedTask;
    }
}
