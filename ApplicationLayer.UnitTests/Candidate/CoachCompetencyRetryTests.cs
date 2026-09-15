using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services;
using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Moq;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

/// <summary>SCRUM-447 retry: cancel job / mark failed / archive roadmap thế hệ cũ.</summary>
public sealed class CoachCompetencyRetryTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task CancelActiveJob_AbandonsAssessment_AndResetsRoadmapItemToPending()
    {
        var itemId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var job = new CandidatePersonalSetJob
        {
            Id = jobId,
            CandidateUserId = UserId,
            Status = CandidatePersonalSetJobStatus.Generating,
            Purpose = CandidatePersonalSetPurpose.CvDrill,
            AssessmentId = assessmentId,
            RoadmapItemId = itemId,
            JobDescription = "drill"
        };
        var assessment = new CandidateAssessment
        {
            Id = assessmentId,
            CandidateUserId = UserId,
            FrameworkId = Guid.NewGuid(),
            Status = CandidateAssessmentStatus.PendingGeneration,
            PersonalSetJobId = jobId
        };
        var item = new CandidateRoadmapItem
        {
            Id = itemId,
            Topic = "EF Core",
            Status = CandidateRoadmapItemStatus.InProgress,
            IsReassessmentGate = false
        };
        var roadmap = new CandidateRoadmap
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            FrameworkId = Guid.NewGuid(),
            Skill = "EF Core",
            Status = CandidateRoadmapStatus.Active,
            Items = new List<CandidateRoadmapItem> { item }
        };
        item.RoadmapId = roadmap.Id;

        var jobs = new FakeJobRepo(job);
        var assessments = new FakeAssessmentRepo(assessment);
        var roadmaps = new FakeRoadmapRepo(roadmap);
        var svc = CreateService(jobs, assessments, roadmaps);

        var dto = await svc.CancelActiveJobAsync(UserId, jobId);

        Assert.Equal(CandidatePersonalSetJobStatus.Failed, job.Status);
        Assert.Contains("huỷ", job.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CandidateAssessmentStatus.Abandoned, assessment.Status);
        Assert.Equal(CandidateRoadmapItemStatus.Pending, item.Status);
        Assert.Equal(CandidatePersonalSetJobStatus.Failed, dto.Status);
    }

    [Fact]
    public async Task CancelActiveJob_ReassessmentGate_RestoresReadyForReassessment_WhenSiblingsDone()
    {
        var gateId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var doneItem = new CandidateRoadmapItem
        {
            Id = Guid.NewGuid(),
            Topic = "Drill A",
            Status = CandidateRoadmapItemStatus.Completed,
            IsReassessmentGate = false
        };
        var gate = new CandidateRoadmapItem
        {
            Id = gateId,
            Topic = "Re-assessment",
            Status = CandidateRoadmapItemStatus.InProgress,
            IsReassessmentGate = true
        };
        var roadmap = new CandidateRoadmap
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            FrameworkId = Guid.NewGuid(),
            Skill = "C#",
            Status = CandidateRoadmapStatus.Active,
            Items = new List<CandidateRoadmapItem> { doneItem, gate }
        };
        var job = new CandidatePersonalSetJob
        {
            Id = jobId,
            CandidateUserId = UserId,
            Status = CandidatePersonalSetJobStatus.Queued,
            Purpose = CandidatePersonalSetPurpose.CvReassessment,
            RoadmapItemId = gateId,
            JobDescription = "reassess"
        };

        var svc = CreateService(new FakeJobRepo(job), new FakeAssessmentRepo(), new FakeRoadmapRepo(roadmap));
        await svc.CancelActiveJobAsync(UserId, jobId);

        Assert.Equal(CandidateRoadmapItemStatus.ReadyForReassessment, gate.Status);
    }

    [Fact]
    public async Task MarkGenerationFailed_SetsAssessmentFailed_NotAbandoned()
    {
        var assessmentId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var job = new CandidatePersonalSetJob
        {
            Id = jobId,
            CandidateUserId = UserId,
            Status = CandidatePersonalSetJobStatus.Failed,
            Purpose = CandidatePersonalSetPurpose.CvDiagnostic,
            AssessmentId = assessmentId,
            ErrorMessage = "RAG timeout",
            JobDescription = "diag"
        };
        var assessment = new CandidateAssessment
        {
            Id = assessmentId,
            CandidateUserId = UserId,
            FrameworkId = Guid.NewGuid(),
            Status = CandidateAssessmentStatus.PendingGeneration,
            PersonalSetJobId = jobId
        };

        var svc = CreateService(new FakeJobRepo(job), new FakeAssessmentRepo(assessment), new FakeRoadmapRepo());
        await svc.MarkGenerationFailedAsync(jobId);

        Assert.Equal(CandidateAssessmentStatus.Failed, assessment.Status);
    }

    [Fact]
    public async Task StartDiagnostic_ArchivesAllPriorActiveRoadmaps()
    {
        var oldActive = new CandidateRoadmap
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            FrameworkId = Guid.NewGuid(),
            Skill = "SQL",
            Status = CandidateRoadmapStatus.Active,
            IsActive = true,
            Items = new List<CandidateRoadmapItem>()
        };
        var oldSuggested = new CandidateRoadmap
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            FrameworkId = Guid.NewGuid(),
            Skill = "C#",
            Status = CandidateRoadmapStatus.Suggested,
            IsActive = true,
            Items = new List<CandidateRoadmapItem>()
        };

        var profile = new CandidateProfile
        {
            UserId = UserId,
            TargetRole = ".NET Backend",
            TargetLevel = "Junior",
            CoachContextConfirmed = true,
            TechStack = new[] { "C#", "SQL" },
            CvEvaluationJson = """{"skills":["C#","SQL"]}"""
        };
        var fw = new CompetencyFramework
        {
            Id = Guid.NewGuid(),
            RoleKey = "dotnet-backend",
            DisplayRole = ".NET Backend",
            TargetLevel = "Junior",
            Skills = new List<CompetencyFrameworkSkill>
            {
                new() { Skill = "C#", ImportanceWeight = 0.5, TargetScore = 70, RequiredDifficulty = "medium", SortOrder = 1 },
                new() { Skill = "SQL", ImportanceWeight = 0.5, TargetScore = 65, RequiredDifficulty = "medium", SortOrder = 2 }
            }
        };

        var jobs = new FakeJobRepo();
        var assessments = new FakeAssessmentRepo();
        var roadmaps = new FakeRoadmapRepo(oldActive, oldSuggested);

        var profiles = new Mock<ICandidateProfileRepository>();
        profiles.Setup(p => p.GetByUserIdAsync(UserId)).ReturnsAsync(profile);

        // SCRUM-453: framework được chọn qua resolver data-driven, không còn FindBestMatchAsync.
        var frameworks = new Mock<ICompetencyFrameworkRepository>();
        frameworks.Setup(f => f.GetPolicyAsync()).ReturnsAsync(new CompetencyScoringPolicy
        {
            OverallReadyThreshold = 70
        });
        var resolver = new Mock<ICompetencyFrameworkResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new FrameworkResolution(fw, fw.RoleKey, fw.TargetLevel, false));
        resolver.Setup(r => r.GetCatalogAsync())
            .ReturnsAsync(new List<FrameworkCatalogEntry>
            {
                new(fw.RoleKey, fw.DisplayRole, "ASP.NET Core", new List<string> { fw.TargetLevel }, "Provisional")
            });

        var gate = new Mock<ISubscriptionGateService>();
        gate.Setup(g => g.CheckCoachGenerationAsync(UserId)).Returns(Task.CompletedTask);

        var scheduler = new Mock<IJobScheduler>();

        var competencyResolver = new Mock<ICompetencyResolver>();
        competencyResolver.Setup(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(new CompetencyResolution(
                CompetencyResolutionMode.Framework,
                fw.RoleKey,
                fw.RoleKey,
                fw.Id,
                fw.DisplayRole,
                fw.TargetLevel,
                "backend",
                "Backend Developer",
                new List<string> { "C#", "SQL" },
                0.97,
                "Exact supported framework matched.",
                fw,
                false,
                new List<string> { "Backend Developer" }));

        var svc = CreateService(
            jobs, assessments, roadmaps,
            profiles.Object, frameworks.Object, gate.Object, scheduler.Object, resolver.Object, competencyResolver.Object);

        await svc.StartDiagnosticAsync(UserId);

        Assert.False(oldActive.IsActive);
        Assert.False(oldSuggested.IsActive);
        Assert.Contains(assessments.Items, a => a.Status == CandidateAssessmentStatus.PendingGeneration);
        Assert.Contains(jobs.Items, j => j.Purpose == CandidatePersonalSetPurpose.CvDiagnostic);
        scheduler.Verify(s => s.EnqueueCandidatePersonalSet(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task CancelActiveJob_RejectsCompletedJob()
    {
        var job = new CandidatePersonalSetJob
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            Status = CandidatePersonalSetJobStatus.Completed,
            Purpose = CandidatePersonalSetPurpose.CvDiagnostic,
            JobDescription = "x"
        };
        var svc = CreateService(new FakeJobRepo(job), new FakeAssessmentRepo(), new FakeRoadmapRepo());
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CancelActiveJobAsync(UserId, job.Id));
    }

    [Fact]
    public async Task HandleDrillSessionCompleted_DoesNotWriteCompetencyScores()
    {
        // Drill chỉ cập nhật roadmap item — không đụng assessment Scored.
        var itemId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var item = new CandidateRoadmapItem
        {
            Id = itemId,
            Topic = "LINQ",
            Status = CandidateRoadmapItemStatus.InProgress
        };
        var roadmap = new CandidateRoadmap
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            FrameworkId = Guid.NewGuid(),
            Skill = "C#",
            Status = CandidateRoadmapStatus.Active,
            Items = new List<CandidateRoadmapItem>
            {
                item,
                new() { Id = Guid.NewGuid(), Topic = "Gate", IsReassessmentGate = true, Status = CandidateRoadmapItemStatus.Pending }
            }
        };
        var job = new CandidatePersonalSetJob
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            Status = CandidatePersonalSetJobStatus.Completed,
            Purpose = CandidatePersonalSetPurpose.CvDrill,
            QuestionSetId = setId,
            RoadmapItemId = itemId,
            JobDescription = "drill"
        };
        var assessmentBefore = new CandidateAssessment
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            FrameworkId = roadmap.FrameworkId,
            Status = CandidateAssessmentStatus.Scored,
            OverallReadiness = 55
        };

        var assessments = new FakeAssessmentRepo(assessmentBefore);
        var svc = CreateService(new FakeJobRepo(job), assessments, new FakeRoadmapRepo(roadmap));

        var session = new PracticeSession
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            QuestionSetId = setId,
            OverallScore = 80,
            Status = "COMPLETED"
        };

        await svc.HandleDrillSessionCompletedAsync(session);

        Assert.Equal(CandidateRoadmapItemStatus.Completed, item.Status);
        Assert.Equal(55, assessmentBefore.OverallReadiness);
        Assert.Equal(CandidateAssessmentStatus.Scored, assessmentBefore.Status);
        Assert.Single(assessments.Items); // không tạo assessment mới
    }

    [Fact]
    public async Task GetRoadmap_OtherCandidate_ThrowsForbidden()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var roadmap = new CandidateRoadmap
        {
            Id = Guid.NewGuid(),
            CandidateUserId = owner,
            Skill = "C#",
            IsActive = true
        };
        var svc = CreateService(new FakeJobRepo(), new FakeAssessmentRepo(), new FakeRoadmapRepo(roadmap));
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.GetRoadmapAsync(other, roadmap.Id));
    }

    [Fact]
    public async Task GetAssessment_OtherCandidate_ThrowsForbidden()
    {
        var owner = Guid.NewGuid();
        var assessment = new CandidateAssessment
        {
            Id = Guid.NewGuid(),
            CandidateUserId = owner,
            Status = CandidateAssessmentStatus.Scored
        };
        var svc = CreateService(new FakeJobRepo(), new FakeAssessmentRepo(assessment), new FakeRoadmapRepo());
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.GetAssessmentAsync(Guid.NewGuid(), assessment.Id));
    }

    private static CoachCompetencyService CreateService(
        FakeJobRepo jobs,
        FakeAssessmentRepo assessments,
        FakeRoadmapRepo roadmaps,
        ICandidateProfileRepository? profiles = null,
        ICompetencyFrameworkRepository? frameworks = null,
        ISubscriptionGateService? gate = null,
        IJobScheduler? scheduler = null,
        ICompetencyFrameworkResolver? resolver = null,
        ICompetencyResolver? competencyResolver = null)
    {
        return new CoachCompetencyService(
            profiles ?? Mock.Of<ICandidateProfileRepository>(),
            frameworks ?? Mock.Of<ICompetencyFrameworkRepository>(),
            resolver ?? Mock.Of<ICompetencyFrameworkResolver>(),
            competencyResolver ?? Mock.Of<ICompetencyResolver>(),
            Mock.Of<IAdaptiveBlueprintBuilder>(),
            assessments,
            roadmaps,
            jobs,
            Mock.Of<IAiFeedbackRepository>(),
            Mock.Of<ICandidateAnswerRepository>(),
            Mock.Of<ICandidateMarketplaceRepository>(),
            gate ?? Mock.Of<ISubscriptionGateService>(),
            scheduler ?? Mock.Of<IJobScheduler>(),
            Mock.Of<ICompetencyProfileService>(),
            Mock.Of<IRoadmapRecommendationService>());
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
        {
            var a = Items.FirstOrDefault(x => x.Id == assessmentId);
            if (a is not null)
            {
                a.OverallReadiness = overallReadiness;
                a.ReadinessStatus = readinessStatus;
                a.ExplanationJson = explanationJson;
            }
            return Task.CompletedTask;
        }
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
