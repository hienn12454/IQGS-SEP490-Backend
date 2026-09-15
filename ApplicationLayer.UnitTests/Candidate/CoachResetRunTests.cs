using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services;
using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Moq;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

/// <summary>SCRUM-459: Chạy Coach mới — soft-reset về Confirm Goal.</summary>
public sealed class CoachResetRunTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task ResetCoachRun_UnconfirmsContext_SupersedesScored_ArchivesRoadmap_FailsPendingJob()
    {
        var profile = new CandidateProfile
        {
            UserId = UserId,
            TargetRole = ".NET Backend Developer",
            TargetLevel = "Junior",
            SelfAssessedLevel = "Junior",
            TechStack = new[] { "C#", "EF Core" },
            CvEvaluationJson = """{"skills":["C#","EF Core"],"summary":"Backend"}""",
            CvBlobPath = "cv/test.pdf",
            CoachContextConfirmed = true,
            CoachContextConfirmedAt = DateTime.UtcNow.AddDays(-1)
        };

        var scored = new CandidateAssessment
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            FrameworkId = Guid.NewGuid(),
            Status = CandidateAssessmentStatus.Scored,
            OverallReadiness = 62,
            Kind = CandidateAssessmentKind.Diagnostic,
            IsActive = true
        };
        var incomplete = new CandidateAssessment
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            FrameworkId = Guid.NewGuid(),
            Status = CandidateAssessmentStatus.ReadyToPractice,
            Kind = CandidateAssessmentKind.Diagnostic,
            IsActive = true
        };
        var pendingJob = new CandidatePersonalSetJob
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            Status = CandidatePersonalSetJobStatus.Generating,
            Purpose = CandidatePersonalSetPurpose.CvDrill,
            JobDescription = "drill",
            AssessmentId = incomplete.Id,
            IsActive = true
        };
        var roadmap = new CandidateRoadmap
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            FrameworkId = Guid.NewGuid(),
            Skill = "C#",
            Status = CandidateRoadmapStatus.Active,
            IsActive = true,
            Items = new List<CandidateRoadmapItem>()
        };

        var profiles = new Mock<ICandidateProfileRepository>();
        profiles.Setup(p => p.GetByUserIdAsync(UserId)).ReturnsAsync(profile);
        profiles.Setup(p => p.UpdateAsync(It.IsAny<CandidateProfile>())).Returns(Task.CompletedTask);

        var competencyResolver = new Mock<ICompetencyResolver>();
        competencyResolver
            .Setup(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(new CompetencyResolution(
                CompetencyResolutionMode.Adaptive,
                "backend",
                "backend",
                null,
                "Backend Developer",
                "Junior",
                "backend",
                "Backend Developer",
                new List<string> { "C#", "EF Core" },
                0.8,
                "Adaptive",
                null,
                false,
                new List<string> { "Backend Developer" }));

        var frameworkResolver = new Mock<ICompetencyFrameworkResolver>();
        frameworkResolver.Setup(r => r.GetCatalogAsync())
            .ReturnsAsync(new List<FrameworkCatalogEntry>());

        var frameworks = new Mock<ICompetencyFrameworkRepository>();
        frameworks.Setup(f => f.GetPolicyAsync()).ReturnsAsync(new CompetencyScoringPolicy
        {
            OverallReadyThreshold = 70
        });

        var assessments = new FakeAssessmentRepo(scored, incomplete);
        var jobs = new FakeJobRepo(pendingJob);
        var roadmaps = new FakeRoadmapRepo(roadmap);

        var svc = new CoachCompetencyService(
            profiles.Object,
            frameworks.Object,
            frameworkResolver.Object,
            competencyResolver.Object,
            Mock.Of<IAdaptiveBlueprintBuilder>(),
            assessments,
            roadmaps,
            jobs,
            Mock.Of<IAiFeedbackRepository>(),
            Mock.Of<ICandidateAnswerRepository>(),
            Mock.Of<ICandidateMarketplaceRepository>(),
            Mock.Of<ISubscriptionGateService>(),
            Mock.Of<IJobScheduler>(),
            Mock.Of<ICompetencyProfileService>(),
            Mock.Of<IRoadmapRecommendationService>());

        var ctx = await svc.ResetCoachRunAsync(UserId);

        Assert.False(profile.CoachContextConfirmed);
        Assert.Null(profile.CoachContextConfirmedAt);
        Assert.False(ctx.ContextConfirmed);
        Assert.Equal(".NET Backend Developer", profile.TargetRole);
        Assert.Equal("Junior", profile.TargetLevel);

        Assert.Equal(CandidateAssessmentStatus.Superseded, scored.Status);
        Assert.Equal(CandidateAssessmentStatus.Abandoned, incomplete.Status);
        Assert.Equal(CandidatePersonalSetJobStatus.Failed, pendingJob.Status);
        Assert.False(roadmap.IsActive);

        var report = await svc.GetLatestReportAsync(UserId);
        Assert.Null(report);

        var history = await svc.GetHistoryAsync(UserId);
        Assert.Contains(history, h => h.Id == scored.Id && h.Status == CandidateAssessmentStatus.Superseded);
    }

    private sealed class FakeJobRepo : ICandidatePersonalSetJobRepository
    {
        public List<CandidatePersonalSetJob> Items { get; } = new();
        public FakeJobRepo(params CandidatePersonalSetJob[] jobs) => Items.AddRange(jobs);
        public Task AddAsync(CandidatePersonalSetJob job) { Items.Add(job); return Task.CompletedTask; }
        public Task UpdateAsync(CandidatePersonalSetJob job) => Task.CompletedTask;
        public Task<CandidatePersonalSetJob?> GetByIdAsync(Guid id)
            => Task.FromResult(Items.FirstOrDefault(j => j.Id == id && j.IsActive));
        public Task<CandidatePersonalSetJob?> GetByQuestionSetIdAsync(Guid questionSetId)
            => Task.FromResult(Items.FirstOrDefault(j => j.QuestionSetId == questionSetId && j.IsActive));
        public Task<CandidatePersonalSetJob?> GetByQuestionSetIdIncludingInactiveAsync(Guid questionSetId)
            => Task.FromResult(Items
                .Where(j => j.QuestionSetId == questionSetId)
                .OrderByDescending(j => j.IsActive)
                .ThenByDescending(j => j.CreatedAt)
                .FirstOrDefault());
        public Task<IReadOnlyList<CandidatePersonalSetJob>> ListByCandidateAsync(Guid candidateUserId)
            => Task.FromResult<IReadOnlyList<CandidatePersonalSetJob>>(
                Items.Where(j => j.CandidateUserId == candidateUserId && j.IsActive).ToList());
    }

    private sealed class FakeAssessmentRepo : ICandidateAssessmentRepository
    {
        public List<CandidateAssessment> Items { get; } = new();
        public FakeAssessmentRepo(params CandidateAssessment[] items) => Items.AddRange(items);
        public Task AddAsync(CandidateAssessment assessment) { Items.Add(assessment); return Task.CompletedTask; }
        public Task UpdateAsync(CandidateAssessment assessment) => Task.CompletedTask;
        public Task SaveScoredAssessmentAsync(
            CandidateAssessment assessment,
            IReadOnlyList<CandidateAssessmentSkillResult> newSkillResults)
        {
            assessment.SkillResults.Clear();
            foreach (var r in newSkillResults) assessment.SkillResults.Add(r);
            return Task.CompletedTask;
        }
        public Task UpdateReadinessAsync(
            Guid assessmentId, double? overallReadiness, string? readinessStatus, string? explanationJson)
            => Task.CompletedTask;
        public Task<CandidateAssessment?> GetByIdAsync(Guid id)
            => Task.FromResult(Items.FirstOrDefault(a => a.Id == id));
        public Task<CandidateAssessment?> GetLatestScoredAsync(Guid candidateUserId)
            => Task.FromResult(Items.FirstOrDefault(a =>
                a.CandidateUserId == candidateUserId && a.Status == CandidateAssessmentStatus.Scored));
        public Task<CandidateAssessment?> GetByQuestionSetIdAsync(Guid questionSetId)
            => Task.FromResult(Items.FirstOrDefault(a => a.QuestionSetId == questionSetId));
        public Task<CandidateAssessment?> GetByJobIdAsync(Guid jobId)
            => Task.FromResult(Items.FirstOrDefault(a => a.PersonalSetJobId == jobId));
        public Task<List<CandidateAssessment>> ListByCandidateAsync(Guid candidateUserId)
            => Task.FromResult(Items.Where(a => a.CandidateUserId == candidateUserId && a.IsActive).ToList());
    }

    private sealed class FakeRoadmapRepo : ICandidateRoadmapRepository
    {
        private readonly List<CandidateRoadmap> _all;
        public FakeRoadmapRepo(params CandidateRoadmap[] items) => _all = items.ToList();
        public Task AddRangeAsync(IEnumerable<CandidateRoadmap> roadmaps)
        {
            _all.AddRange(roadmaps);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(CandidateRoadmap roadmap) => Task.CompletedTask;
        public Task ArchiveActiveByCandidateAsync(Guid candidateUserId)
        {
            foreach (var r in _all.Where(x => x.CandidateUserId == candidateUserId && x.IsActive))
                r.IsActive = false;
            return Task.CompletedTask;
        }
        public Task RestoreActiveAsync(IEnumerable<Guid> roadmapIds)
        {
            var set = roadmapIds.ToHashSet();
            foreach (var r in _all.Where(x => set.Contains(x.Id)))
                r.IsActive = true;
            return Task.CompletedTask;
        }
        public Task<List<CandidateRoadmap>> ListByCandidateAsync(Guid candidateUserId)
            => Task.FromResult(_all.Where(r => r.CandidateUserId == candidateUserId && r.IsActive).ToList());
        public Task<List<CandidateRoadmap>> ListAllByCandidateAsync(Guid candidateUserId)
            => Task.FromResult(_all.Where(r => r.CandidateUserId == candidateUserId).ToList());
        public Task<CandidateRoadmap?> GetByIdAsync(Guid id)
            => Task.FromResult(_all.FirstOrDefault(r => r.Id == id && r.IsActive));
    }
}
