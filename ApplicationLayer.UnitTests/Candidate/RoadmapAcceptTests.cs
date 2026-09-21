using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services;
using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

/// <summary>SCRUM-462: preview/accept lộ trình — min skill, gate, guard drill.</summary>
public sealed class RoadmapAcceptTests
{
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");

    private static CoachCompetencyService CreateService(Mock<ICandidateRoadmapRepository> roadmaps)
    {
        var gate = new Mock<ISubscriptionGateService>();
        gate.Setup(g => g.CheckCoachGenerationAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);

        return new CoachCompetencyService(
            Mock.Of<ICandidateProfileRepository>(),
            Mock.Of<ICompetencyFrameworkRepository>(),
            Mock.Of<ICompetencyFrameworkResolver>(),
            Mock.Of<ICompetencyResolver>(),
            Mock.Of<IAdaptiveBlueprintBuilder>(),
            Mock.Of<ICandidateAssessmentRepository>(),
            roadmaps.Object,
            Mock.Of<ICandidatePersonalSetJobRepository>(),
            Mock.Of<IAiFeedbackRepository>(),
            Mock.Of<ICandidateAnswerRepository>(),
            Mock.Of<ICandidateMarketplaceRepository>(),
            gate.Object,
            Mock.Of<IJobScheduler>(),
            Mock.Of<ICompetencyProfileService>(),
            Mock.Of<IRoadmapRecommendationService>());
    }

    private static CandidateRoadmap MakeDraft(string skill, bool includeTopic, bool withGate = true)
    {
        var roadmap = new CandidateRoadmap
        {
            Id = Guid.NewGuid(),
            CandidateUserId = UserId,
            Skill = skill,
            Status = CandidateRoadmapStatus.Suggested,
            AcceptedAt = null,
            IsActive = true,
            TargetScore = 70,
            Kind = CandidateRoadmapKind.Gap,
            Priority = "medium",
            SourceMode = CompetencySourceMode.Framework,
            ExplanationJson = """{"reason":"test","kbSource":"inferred","skillSource":"cv"}"""
        };
        if (includeTopic)
        {
            roadmap.Items.Add(new CandidateRoadmapItem
            {
                Id = Guid.NewGuid(),
                Topic = $"{skill} fundamentals",
                SortOrder = 1,
                Status = CandidateRoadmapItemStatus.Pending,
                IsIncluded = true,
                IsReassessmentGate = false
            });
        }
        else
        {
            roadmap.Items.Add(new CandidateRoadmapItem
            {
                Id = Guid.NewGuid(),
                Topic = $"{skill} skipped",
                SortOrder = 1,
                Status = CandidateRoadmapItemStatus.Pending,
                IsIncluded = false,
                IsReassessmentGate = false
            });
        }
        if (withGate)
        {
            roadmap.Items.Add(new CandidateRoadmapItem
            {
                Id = Guid.NewGuid(),
                Topic = "Re-assessment",
                SortOrder = 99,
                Status = CandidateRoadmapItemStatus.Pending,
                IsIncluded = true,
                IsReassessmentGate = true
            });
        }
        return roadmap;
    }

    [Fact]
    public async Task Accept_Rejects_WhenNoSkillHasIncludedTopic()
    {
        var draft = MakeDraft("Spring", includeTopic: false);
        var roadmaps = new Mock<ICandidateRoadmapRepository>();
        roadmaps.Setup(r => r.ListByCandidateAsync(UserId)).ReturnsAsync(new List<CandidateRoadmap> { draft });

        var svc = CreateService(roadmaps);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => svc.AcceptRoadmapsAsync(UserId));
        Assert.Contains("ít nhất 1 skill", ex.Message, StringComparison.OrdinalIgnoreCase);
        roadmaps.Verify(r => r.UpdateAsync(It.IsAny<CandidateRoadmap>()), Times.Never);
    }

    [Fact]
    public async Task Accept_SetsActiveAndAcceptedAt_WhenAtLeastOneSkill()
    {
        var learn = MakeDraft("Spring", includeTopic: true);
        var drop = MakeDraft("SQL", includeTopic: false);
        var list = new List<CandidateRoadmap> { learn, drop };
        var roadmaps = new Mock<ICandidateRoadmapRepository>();
        roadmaps.Setup(r => r.ListByCandidateAsync(UserId)).ReturnsAsync(() => list.Where(x => x.IsActive).ToList());
        roadmaps.Setup(r => r.UpdateAsync(It.IsAny<CandidateRoadmap>()))
            .Callback<CandidateRoadmap>(r =>
            {
                var idx = list.FindIndex(x => x.Id == r.Id);
                if (idx >= 0) list[idx] = r;
            })
            .Returns(Task.CompletedTask);

        var svc = CreateService(roadmaps);
        var result = await svc.AcceptRoadmapsAsync(UserId);

        Assert.NotNull(learn.AcceptedAt);
        Assert.Equal(CandidateRoadmapStatus.Active, learn.Status);
        Assert.False(drop.IsActive);
        Assert.Contains(result, r => r.Id == learn.Id && r.AcceptedAt is not null);
    }

    [Fact]
    public async Task UpdateDraft_RejectsTurningOffReassessmentGate()
    {
        var draft = MakeDraft("Spring", includeTopic: true);
        var gate = draft.Items.Single(i => i.IsReassessmentGate);
        var roadmaps = new Mock<ICandidateRoadmapRepository>();
        roadmaps.Setup(r => r.ListByCandidateAsync(UserId)).ReturnsAsync(new List<CandidateRoadmap> { draft });

        var svc = CreateService(roadmaps);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.UpdateRoadmapDraftAsync(UserId, new UpdateRoadmapDraftDto
            {
                Items = { new UpdateRoadmapDraftItemDto { ItemId = gate.Id, IsIncluded = false } }
            }));
        Assert.Contains("Re-assessment", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartDrill_Rejects_WhenNotAccepted()
    {
        var draft = MakeDraft("Spring", includeTopic: true);
        var topic = draft.Items.First(i => !i.IsReassessmentGate);
        var roadmaps = new Mock<ICandidateRoadmapRepository>();
        roadmaps.Setup(r => r.GetByIdAsync(draft.Id)).ReturnsAsync(draft);

        var svc = CreateService(roadmaps);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.StartDrillForRoadmapItemAsync(UserId, draft.Id, topic.Id));
        Assert.Contains("chấp nhận lộ trình", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OutsideCv_Provenance_WhenSkillNotInSnapshotCv()
    {
        var cv = RoadmapRecommendationService.ParseCvSkillsFromSnapshot(
            """{"skills":["React","TypeScript"],"coreSkills":["React","TypeScript","ASP.NET Core"]}""");
        Assert.True(RoadmapRecommendationService.IsSkillFromCv("React", cv));
        Assert.False(RoadmapRecommendationService.IsSkillFromCv("ASP.NET Core", cv));

        var reason = RoadmapRecommendationService.BuildOutsideCvReason(
            "ASP.NET Core", "Backend", "Junior", 0.8, 70);
        Assert.Contains("ASP.NET Core", reason, StringComparison.Ordinal);
        Assert.Contains("Framework Junior", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rebuild_SetsOutsideCv_WhenSkillNotInCvSnapshot()
    {
        var userId = Guid.NewGuid();
        var fw = new CompetencyFramework
        {
            Id = Guid.NewGuid(),
            RoleKey = "java-backend",
            DisplayRole = "Java Backend",
            TargetLevel = "Junior",
            Skills =
            {
                new CompetencyFrameworkSkill { Skill = "Spring", ImportanceWeight = 0.6, TargetScore = 70 },
                new CompetencyFrameworkSkill { Skill = "Kafka", ImportanceWeight = 0.5, TargetScore = 65 }
            }
        };
        var profile = new CandidateSkillPlan
        {
            Items =
            {
                new CandidateSkillPlanItem { Skill = "Spring", CurrentScore = 40, TargetScore = 70, ImportanceWeight = 0.6 },
                new CandidateSkillPlanItem { Skill = "Kafka", CurrentScore = 30, TargetScore = 65, ImportanceWeight = 0.5 }
            }
        };
        var stored = new List<CandidateRoadmap>();
        var roadmaps = new Mock<ICandidateRoadmapRepository>();
        roadmaps.Setup(r => r.ListAllByCandidateAsync(userId)).ReturnsAsync(stored);
        roadmaps.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<CandidateRoadmap>>()))
            .Callback<IEnumerable<CandidateRoadmap>>(rows => stored.AddRange(rows))
            .Returns(Task.CompletedTask);

        var nodeRepo = new Mock<IRoadmapNodeRepository>();
        nodeRepo.Setup(n => n.ListBySkillAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<RoadmapNode>());

        var rag = new Mock<IRagService>();
        rag.Setup(r => r.RecommendRoadmapAsync(
                It.IsAny<ApplicationLayer.DTOs.Rag.RagRoadmapRecommendRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicationLayer.DTOs.Rag.RagRoadmapRecommendResult { Success = false });

        var knowledge = new Mock<IKnowledgeDocumentRepository>();
        knowledge.Setup(k => k.ListSystemDocumentIdsByFolderAsync(It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<Guid>());
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var svc = new RoadmapRecommendationService(
            roadmaps.Object, nodeRepo.Object, rag.Object, knowledge.Object, config);
        var assessment = new CandidateAssessment
        {
            Id = Guid.NewGuid(),
            CandidateUserId = userId,
            ContextSnapshotJson = """{"skills":["Spring"],"coreSkills":["Spring","Kafka"]}""",
            SkillResults =
            {
                new CandidateAssessmentSkillResult { Skill = "Spring", SkillScore = 40, TargetScore = 70, ImportanceWeight = 0.6 },
                new CandidateAssessmentSkillResult { Skill = "Kafka", SkillScore = 30, TargetScore = 65, ImportanceWeight = 0.5 }
            }
        };
        await svc.RebuildFromDiagnosticAsync(userId, assessment, fw, profile);

        var spring = stored.Single(r => r.Skill == "Spring");
        var kafka = stored.Single(r => r.Skill == "Kafka");
        var (springSrc, _) = RoadmapRecommendationService.ParseSkillProvenanceFromJson(spring.ExplanationJson);
        var (kafkaSrc, kafkaReason) = RoadmapRecommendationService.ParseSkillProvenanceFromJson(kafka.ExplanationJson);
        Assert.Equal("cv", springSrc);
        Assert.Equal("outsideCv", kafkaSrc);
        Assert.False(string.IsNullOrWhiteSpace(kafkaReason));
        Assert.All(spring.Items, i => Assert.True(i.IsIncluded));
        Assert.Null(spring.AcceptedAt);
    }
}
