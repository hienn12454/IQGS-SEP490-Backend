using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class RoadmapRecommendationTests
{
    [Fact]
    public void PriorityScore_IsGapTimesWeight()
    {
        Assert.Equal(6, RoadmapRecommendationService.ComputePriorityScore(20, 0.3));
        Assert.Equal(0, RoadmapRecommendationService.ComputePriorityScore(-5, 0.9));
    }

    [Fact]
    public async Task Rebuild_CreatesGapAndAdvanced_AndDoesNotCallInventedTopicsWhenNodesExist()
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
                new CompetencyFrameworkSkill { Skill = "SQL", ImportanceWeight = 0.4, TargetScore = 70 }
            }
        };
        var profile = new CandidateSkillPlan
        {
            Items =
            {
                new CandidateSkillPlanItem { Skill = "Spring", CurrentScore = 40, TargetScore = 70, ImportanceWeight = 0.6 },
                new CandidateSkillPlanItem { Skill = "SQL", CurrentScore = 80, TargetScore = 70, ImportanceWeight = 0.4 }
            }
        };
        var nodes = new List<RoadmapNode>
        {
            new() { Id = Guid.NewGuid(), RoleKey = "java-backend", Level = "Junior", Skill = "Spring", Topic = "DI", Importance = 0.9, SourceTitle = "kb", SourceUrl = "https://example.test/di" }
        };
        var stored = new List<CandidateRoadmap>();
        var roadmaps = new Mock<ICandidateRoadmapRepository>();
        roadmaps.Setup(r => r.ListAllByCandidateAsync(userId)).ReturnsAsync(stored);
        roadmaps.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<CandidateRoadmap>>()))
            .Callback<IEnumerable<CandidateRoadmap>>(rows => stored.AddRange(rows))
            .Returns(Task.CompletedTask);

        var nodeRepo = new Mock<IRoadmapNodeRepository>();
        nodeRepo.Setup(n => n.ListBySkillAsync("java-backend", "Junior", "Spring")).ReturnsAsync(nodes);
        nodeRepo.Setup(n => n.ListBySkillAsync("java-backend", "Junior", "SQL")).ReturnsAsync([]);

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
            SkillResults =
            {
                new CandidateAssessmentSkillResult { Skill = "Spring", SkillScore = 40, TargetScore = 70, ImportanceWeight = 0.6 },
                new CandidateAssessmentSkillResult { Skill = "SQL", SkillScore = 80, TargetScore = 70, ImportanceWeight = 0.4 }
            }
        };
        await svc.RebuildFromDiagnosticAsync(userId, assessment, fw, profile);

        Assert.Equal(2, stored.Count);
        var gap = stored.Single(r => r.Skill == "Spring");
        var advanced = stored.Single(r => r.Skill == "SQL");
        Assert.Equal(CandidateRoadmapKind.Gap, gap.Kind);
        Assert.Equal(CandidateRoadmapKind.Advanced, advanced.Kind);
        Assert.Equal(18, gap.PriorityScore);
        Assert.Contains(gap.Items, i => i.SourceUrl == "https://example.test/di");
        Assert.Contains(gap.Items, i => i.IsReassessmentGate);
        Assert.Contains("\"kbSource\":\"inferred\"", gap.ExplanationJson ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rebuild_AdaptiveWithoutFramework_CreatesRoadmapFromBlueprintTopics()
    {
        var userId = Guid.NewGuid();
        var stored = new List<CandidateRoadmap>();
        var roadmaps = new Mock<ICandidateRoadmapRepository>();
        roadmaps.Setup(r => r.ListAllByCandidateAsync(userId)).ReturnsAsync(stored);
        roadmaps.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<CandidateRoadmap>>()))
            .Callback<IEnumerable<CandidateRoadmap>>(rows => stored.AddRange(rows))
            .Returns(Task.CompletedTask);

        var rag = new Mock<IRagService>();
        rag.Setup(r => r.RetrieveCompetencyContextAsync(
                It.IsAny<ApplicationLayer.DTOs.Rag.RagCompetencyContextRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("rag down"));
        rag.Setup(r => r.GenerateAdaptiveRoadmapAsync(
                It.IsAny<ApplicationLayer.DTOs.Rag.RagAdaptiveRoadmapRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("rag down"));

        var svc = new RoadmapRecommendationService(
            roadmaps.Object,
            Mock.Of<IRoadmapNodeRepository>(),
            rag.Object,
            EmptyKnowledge(),
            EmptyConfig());

        var bp = new CompetencyBlueprint
        {
            SourceMode = CompetencyResolutionMode.Adaptive,
            TargetRole = "Backend",
            TargetLevel = "Junior",
            Competencies =
            {
                new CompetencyItem
                {
                    SkillName = "Go",
                    TargetScore = 70,
                    Weight = 1,
                    Topics = { "goroutine", "channels" }
                }
            }
        };
        var assessment = new CandidateAssessment
        {
            Id = Guid.NewGuid(),
            CandidateUserId = userId,
            ResolutionMode = CompetencyResolutionMode.Adaptive,
            BlueprintJson = CompetencyBlueprintJson.Serialize(bp)
        };
        var profile = new CandidateSkillPlan
        {
            Items =
            {
                new CandidateSkillPlanItem { Skill = "Go", CurrentScore = 40, TargetScore = 70, ImportanceWeight = 1 }
            }
        };

        await svc.RebuildFromDiagnosticAsync(userId, assessment, framework: null, profile);
        var row = Assert.Single(stored);
        Assert.Equal(CompetencySourceMode.RagDynamic, row.SourceMode);
        Assert.Contains(row.Items, i => i.Topic == "goroutine");
        Assert.Contains(row.Items, i => i.IsReassessmentGate);
    }

    /// <summary>SCRUM-461: profile còn asp.net cũ không được dựng lại khi diagnostic chỉ đo React/TS.</summary>
    [Fact]
    public async Task Rebuild_IgnoresStaleProfileSkillsOutsideAssessment()
    {
        var userId = Guid.NewGuid();
        var stored = new List<CandidateRoadmap>();
        var roadmaps = new Mock<ICandidateRoadmapRepository>();
        roadmaps.Setup(r => r.ListAllByCandidateAsync(userId)).ReturnsAsync(stored);
        roadmaps.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<CandidateRoadmap>>()))
            .Callback<IEnumerable<CandidateRoadmap>>(rows => stored.AddRange(rows))
            .Returns(Task.CompletedTask);
        roadmaps.Setup(r => r.ArchiveActiveByCandidateAsync(userId)).Returns(Task.CompletedTask);

        var rag = new Mock<IRagService>();
        rag.Setup(r => r.RetrieveCompetencyContextAsync(
                It.IsAny<ApplicationLayer.DTOs.Rag.RagCompetencyContextRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicationLayer.DTOs.Rag.RagCompetencyContextResult { Success = true, Chunks = [] });
        rag.Setup(r => r.GenerateAdaptiveRoadmapAsync(
                It.IsAny<ApplicationLayer.DTOs.Rag.RagAdaptiveRoadmapRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicationLayer.DTOs.Rag.RagAdaptiveRoadmapResult { Success = false });

        var svc = new RoadmapRecommendationService(
            roadmaps.Object,
            Mock.Of<IRoadmapNodeRepository>(),
            rag.Object,
            EmptyKnowledge(),
            EmptyConfig());

        var bp = new CompetencyBlueprint
        {
            SourceMode = CompetencyResolutionMode.Adaptive,
            TargetRole = "Frontend",
            TargetLevel = "Junior",
            Competencies =
            {
                new CompetencyItem { SkillName = "React", TargetScore = 70, Weight = 0.5, Topics = { "hooks" } },
                new CompetencyItem { SkillName = "TypeScript", TargetScore = 70, Weight = 0.5, Topics = { "types" } }
            }
        };
        var assessment = new CandidateAssessment
        {
            Id = Guid.NewGuid(),
            CandidateUserId = userId,
            ResolutionMode = CompetencyResolutionMode.Adaptive,
            Kind = CandidateAssessmentKind.Diagnostic,
            BlueprintJson = CompetencyBlueprintJson.Serialize(bp),
            SkillResults =
            {
                new CandidateAssessmentSkillResult { Skill = "React", SkillScore = 0, TargetScore = 70, ImportanceWeight = 0.5 },
                new CandidateAssessmentSkillResult { Skill = "TypeScript", SkillScore = 0, TargetScore = 70, ImportanceWeight = 0.5 }
            }
        };
        var profile = new CandidateSkillPlan
        {
            ResolutionMode = CompetencyResolutionMode.Adaptive,
            Items =
            {
                new CandidateSkillPlanItem { Skill = "asp.net core", CurrentScore = 118, TargetScore = 70, ImportanceWeight = 0.2 },
                new CandidateSkillPlanItem { Skill = "linq", CurrentScore = 100, TargetScore = 70, ImportanceWeight = 0.2 },
                new CandidateSkillPlanItem { Skill = "React", CurrentScore = 0, TargetScore = 70, ImportanceWeight = 0.5 },
                new CandidateSkillPlanItem { Skill = "TypeScript", CurrentScore = 0, TargetScore = 70, ImportanceWeight = 0.5 }
            }
        };

        await svc.RebuildFromDiagnosticAsync(userId, assessment, framework: null, profile);

        Assert.Equal(2, stored.Count);
        Assert.All(stored, r => Assert.DoesNotContain("asp.net", r.Skill, StringComparison.OrdinalIgnoreCase));
        Assert.All(stored, r => Assert.DoesNotContain("linq", r.Skill, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(stored, r => r.Skill == "React");
        Assert.Contains(stored, r => r.Skill == "TypeScript");
    }

    [Fact]
    public async Task Rebuild_TruncatesTopicLongerThanColumnLimit()
    {
        var userId = Guid.NewGuid();
        var stored = new List<CandidateRoadmap>();
        var roadmaps = new Mock<ICandidateRoadmapRepository>();
        roadmaps.Setup(r => r.ListAllByCandidateAsync(userId)).ReturnsAsync(stored);
        roadmaps.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<CandidateRoadmap>>()))
            .Callback<IEnumerable<CandidateRoadmap>>(rows => stored.AddRange(rows))
            .Returns(Task.CompletedTask);

        var longTopic = new string('a', 400);
        var fw = new CompetencyFramework
        {
            Id = Guid.NewGuid(),
            RoleKey = "java-backend",
            DisplayRole = "Java Backend",
            TargetLevel = "Junior",
            Skills = { new CompetencyFrameworkSkill { Skill = "Spring", ImportanceWeight = 1, TargetScore = 70 } }
        };
        var nodes = new List<RoadmapNode>
        {
            new() { Id = Guid.NewGuid(), RoleKey = "java-backend", Level = "Junior", Skill = "Spring", Topic = longTopic, Importance = 1 }
        };
        var nodeRepo = new Mock<IRoadmapNodeRepository>();
        nodeRepo.Setup(n => n.ListBySkillAsync("java-backend", "Junior", "Spring")).ReturnsAsync(nodes);

        var rag = new Mock<IRagService>();
        rag.Setup(r => r.RecommendRoadmapAsync(
                It.IsAny<ApplicationLayer.DTOs.Rag.RagRoadmapRecommendRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicationLayer.DTOs.Rag.RagRoadmapRecommendResult { Success = false });

        var svc = new RoadmapRecommendationService(
            roadmaps.Object,
            nodeRepo.Object,
            rag.Object,
            EmptyKnowledge(),
            EmptyConfig());
        var assessment = new CandidateAssessment { Id = Guid.NewGuid(), CandidateUserId = userId };
        var profile = new CandidateSkillPlan
        {
            Items = { new CandidateSkillPlanItem { Skill = "Spring", CurrentScore = 40, TargetScore = 70, ImportanceWeight = 1 } }
        };

        await svc.RebuildFromDiagnosticAsync(userId, assessment, fw, profile);
        var topic = Assert.Single(stored).Items.First(i => !i.IsReassessmentGate).Topic;
        Assert.Equal(300, topic.Length);
        Assert.Contains("\"kbSource\":\"inferred\"", Assert.Single(stored).ExplanationJson ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rebuild_WithCoachRoadmapDocs_SetsKbSourceSystem()
    {
        var userId = Guid.NewGuid();
        var stored = new List<CandidateRoadmap>();
        var roadmaps = new Mock<ICandidateRoadmapRepository>();
        roadmaps.Setup(r => r.ListAllByCandidateAsync(userId)).ReturnsAsync(stored);
        roadmaps.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<CandidateRoadmap>>()))
            .Callback<IEnumerable<CandidateRoadmap>>(rows => stored.AddRange(rows))
            .Returns(Task.CompletedTask);

        var fw = new CompetencyFramework
        {
            Id = Guid.NewGuid(),
            RoleKey = "java-backend",
            DisplayRole = "Java Backend",
            TargetLevel = "Junior",
            Skills = { new CompetencyFrameworkSkill { Skill = "Spring", ImportanceWeight = 1, TargetScore = 70 } }
        };
        var nodes = new List<RoadmapNode>
        {
            new() { Id = Guid.NewGuid(), RoleKey = "java-backend", Level = "Junior", Skill = "Spring", Topic = "DI", Importance = 1 }
        };
        var nodeRepo = new Mock<IRoadmapNodeRepository>();
        nodeRepo.Setup(n => n.ListBySkillAsync("java-backend", "Junior", "Spring")).ReturnsAsync(nodes);

        var docId = Guid.NewGuid();
        var knowledge = new Mock<IKnowledgeDocumentRepository>();
        knowledge.Setup(k => k.ListSystemDocumentIdsByFolderAsync(CoachRoadmapKnowledgeFolder.DefaultFolder))
            .ReturnsAsync(new List<Guid> { docId });

        ApplicationLayer.DTOs.Rag.RagRoadmapRecommendRequest? captured = null;
        var rag = new Mock<IRagService>();
        rag.Setup(r => r.RecommendRoadmapAsync(
                It.IsAny<ApplicationLayer.DTOs.Rag.RagRoadmapRecommendRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<ApplicationLayer.DTOs.Rag.RagRoadmapRecommendRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new ApplicationLayer.DTOs.Rag.RagRoadmapRecommendResult { Success = false });

        var svc = new RoadmapRecommendationService(
            roadmaps.Object, nodeRepo.Object, rag.Object, knowledge.Object, EmptyConfig());
        await svc.RebuildFromDiagnosticAsync(
            userId,
            new CandidateAssessment { Id = Guid.NewGuid(), CandidateUserId = userId },
            fw,
            new CandidateSkillPlan
            {
                Items = { new CandidateSkillPlanItem { Skill = "Spring", CurrentScore = 40, TargetScore = 70, ImportanceWeight = 1 } }
            });

        Assert.NotNull(captured);
        Assert.Contains(docId, captured!.DocumentIds ?? []);
        Assert.Contains("\"kbSource\":\"system\"", Assert.Single(stored).ExplanationJson ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAfterReassessment_DoesNotArchiveOtherSkills()
    {
        var userId = Guid.NewGuid();
        var other = new CandidateRoadmap
        {
            Id = Guid.NewGuid(),
            CandidateUserId = userId,
            Skill = "SQL",
            Kind = CandidateRoadmapKind.Gap,
            IsActive = true,
            Gap = 20
        };
        var spring = new CandidateRoadmap
        {
            Id = Guid.NewGuid(),
            CandidateUserId = userId,
            Skill = "Spring",
            Kind = CandidateRoadmapKind.Gap,
            IsActive = true,
            Gap = 30
        };
        var roadmaps = new Mock<ICandidateRoadmapRepository>();
        roadmaps.Setup(r => r.ListByCandidateAsync(userId)).ReturnsAsync([other, spring]);
        roadmaps.Setup(r => r.UpdateAsync(It.IsAny<CandidateRoadmap>())).Returns(Task.CompletedTask);

        var svc = new RoadmapRecommendationService(
            roadmaps.Object,
            Mock.Of<IRoadmapNodeRepository>(),
            Mock.Of<IRagService>(),
            EmptyKnowledge(),
            EmptyConfig());

        var fw = new CompetencyFramework
        {
            Id = Guid.NewGuid(),
            Skills = { new CompetencyFrameworkSkill { Skill = "Spring", TargetScore = 70, ImportanceWeight = 0.6 } }
        };
        var assessment = new CandidateAssessment
        {
            SkillResults =
            {
                new CandidateAssessmentSkillResult { Skill = "Spring", SkillScore = 72, TargetScore = 70, Gap = -2, ImportanceWeight = 0.6 }
            }
        };

        await svc.RefreshAfterReassessmentAsync(userId, assessment, fw, new CandidateSkillPlan());
        Assert.True(other.IsActive);
        Assert.Equal(CandidateRoadmapKind.Advanced, spring.Kind);
        Assert.Equal(0, spring.PriorityScore);
        roadmaps.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<CandidateRoadmap>>()), Times.Never);
    }

    private static IKnowledgeDocumentRepository EmptyKnowledge()
    {
        var knowledge = new Mock<IKnowledgeDocumentRepository>();
        knowledge.Setup(k => k.ListSystemDocumentIdsByFolderAsync(It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<Guid>());
        return knowledge.Object;
    }

    private static IConfiguration EmptyConfig()
        => new ConfigurationBuilder().AddInMemoryCollection().Build();
}
