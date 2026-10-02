using System.Text.Json;
using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services;
using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

/// <summary>
/// Bài chẩn đoán đo nhanh mọi skill CV ngoài nhóm core để lộ trình phủ đủ skill trên CV.
/// Case thật: CV Backend Intern, role .NET Backend Junior, làm 0 điểm → lộ trình phải có toàn bộ skill CV.
/// </summary>
public sealed class CoachQuickCheckCoverageTests
{
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly string[] CoreDotnet = ["C#", "ASP.NET Core", "SQL", "REST API", "EF Core"];

    // Skill sau bước Phân tích CV (có cả biến thể / trùng tên như CV thật).
    private static readonly string[] CvSkills =
    [
        "C#", "ASP.NET Core", "ASP.NET", "SignalR", "JWT", "OAuth", "Java", "Spring Boot",
        "React", "React Native", "SQL", "PostgreSQL", "SQL Server", "Docker", "Git", "Azure",
        "Nginx", "REST API", "RESTful APIs", "Firebase"
    ];

    private static readonly string[] ExpectedQuickChecks =
    [
        "SignalR", "JWT", "OAuth", "Java", "Spring Boot", "React", "React Native",
        "PostgreSQL", "Docker", "Git", "Azure", "Nginx", "Firebase"
    ];

    [Fact]
    public void Select_SkipsCoreAndVariants_KeepsCvOrder()
    {
        var selection = CoachQuickCheckSkills.Select(CvSkills, CoreDotnet, maxSkills: 30);

        Assert.Equal(ExpectedQuickChecks, selection.Selected);
        Assert.Empty(selection.Overflow);
    }

    [Fact]
    public void Select_CapsAndReturnsOverflow()
    {
        var selection = CoachQuickCheckSkills.Select(CvSkills, CoreDotnet, maxSkills: 5);

        Assert.Equal(ExpectedQuickChecks.Take(5), selection.Selected);
        Assert.Equal(ExpectedQuickChecks.Skip(5), selection.Overflow);
    }

    [Fact]
    public void QuickCheckMaxSkills_FollowsAdminSettingAndQuestionCap()
    {
        Assert.Equal(12, CoachDiagnosticPolicy.QuickCheckMaxSkills(
            new CompetencyScoringPolicy { ScreeningMaxSkills = 12, ScreeningQuestionsPerSkill = 1 }));
        Assert.Equal(30, CoachDiagnosticPolicy.QuickCheckMaxSkills(
            new CompetencyScoringPolicy { ScreeningMaxSkills = 30, ScreeningQuestionsPerSkill = 1 }));
        // 3 câu/skill → trần 40 câu chỉ đủ 13 skill.
        Assert.Equal(13, CoachDiagnosticPolicy.QuickCheckMaxSkills(
            new CompetencyScoringPolicy { ScreeningMaxSkills = 30, ScreeningQuestionsPerSkill = 3 }));
    }

    [Fact]
    public void BuildDiagnostic_AddsQuickCheckQuestionsAfterCore_AndScoringKeepsLevelOnCore()
    {
        var blueprint = DotnetBlueprintWithQuickChecks(maxQuickChecks: 30);

        var planJson = JsonSerializer.Serialize(
            DiagnosticBlueprintBuilder.BuildDiagnostic(blueprint, new DiagnosticBlueprintBuilder.Options(QuestionsPerSkill: 3)),
            CamelCase);
        var slots = BlueprintComplianceValidator.ParseSlots(planJson);

        Assert.Equal(5 * 3 + ExpectedQuickChecks.Length, slots.Count);
        Assert.All(slots.Take(15), s => Assert.Contains(s.Skill, CoreDotnet));
        Assert.Equal(ExpectedQuickChecks, slots.Skip(15).Select(s => s.Skill));
        Assert.All(slots.Skip(15), s => Assert.Equal(QuestionDifficultyLevel.Easy, s.Difficulty));

        // Level chỉ tính trên core; skill đo nhanh chấm riêng.
        Assert.Equal(CoreDotnet, FrameworkBlueprintBuilder.ToScoringSkills(blueprint).Select(s => s.Skill));
        Assert.Equal(ExpectedQuickChecks, FrameworkBlueprintBuilder.ToQuickCheckScoringSkills(blueprint).Select(s => s.Skill));
    }

    [Fact]
    public void PlanSplitter_SplitsCoreAndChunks_AndMergedQuestionsPassBlueprintValidation()
    {
        var blueprint = DotnetBlueprintWithQuickChecks(maxQuickChecks: 30);
        var planJson = JsonSerializer.Serialize(
            DiagnosticBlueprintBuilder.BuildDiagnostic(blueprint, new DiagnosticBlueprintBuilder.Options(QuestionsPerSkill: 3)),
            CamelCase);

        var parts = CoachPlanSplitter.SplitQuickCheck(planJson, chunkSize: 10);

        Assert.Equal([15, 10, 3], parts.Select(p => p.TotalQuestions));
        Assert.Equal(CoreDotnet, parts[0].Skills);
        foreach (var part in parts)
        {
            var partSlots = BlueprintComplianceValidator.ParseSlots(part.Plan.GetRawText());
            Assert.Equal(Enumerable.Range(1, part.TotalQuestions), partSlots.Select(s => s.Order));
        }

        // Giả lập RAG trả đúng từng phần (order trùng giữa các phần) → validate theo cả đề vẫn khớp.
        var merged = parts
            .SelectMany(part => BlueprintComplianceValidator.ParseSlots(part.Plan.GetRawText()))
            .Select(slot => new RagGeneratedQuestionDto
            {
                Order = slot.Order,
                Skill = slot.Skill,
                Difficulty = slot.Difficulty,
                Question = $"Explain {slot.Skill}: {slot.Topic}?"
            })
            .ToList();
        var result = BlueprintComplianceValidator.Validate(BlueprintComplianceValidator.ParseSlots(planJson), merged);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(Enumerable.Range(1, 28), result.Questions.Select(q => q.Order!.Value));
    }

    [Fact]
    public void PlanSplitter_WithoutQuickCheck_ReturnsEmpty_SoOldSingleCallIsKept()
    {
        var blueprint = DotnetBlueprintWithQuickChecks(maxQuickChecks: 0);
        var planJson = JsonSerializer.Serialize(DiagnosticBlueprintBuilder.BuildDiagnostic(blueprint), CamelCase);

        Assert.Empty(CoachPlanSplitter.SplitQuickCheck(planJson));
    }

    [Fact]
    public async Task Rebuild_ZeroScore_CreatesRoadmapForEveryCvSkill_WithoutLlmForQuickChecks()
    {
        var userId = Guid.NewGuid();
        var framework = new CompetencyFramework
        {
            Id = Guid.NewGuid(),
            RoleKey = "dotnet-backend",
            DisplayRole = ".NET Backend Developer",
            TargetLevel = "Junior"
        };
        for (var i = 0; i < CoreDotnet.Length; i++)
            framework.Skills.Add(new CompetencyFrameworkSkill
            {
                Skill = CoreDotnet[i], ImportanceWeight = 0.2, TargetScore = 70, SortOrder = i
            });

        var blueprint = DotnetBlueprintWithQuickChecks(maxQuickChecks: 30);
        blueprint.FrameworkId = framework.Id;
        var assessment = new CandidateAssessment
        {
            Id = Guid.NewGuid(),
            CandidateUserId = userId,
            Kind = CandidateAssessmentKind.Diagnostic,
            ResolutionMode = CompetencyResolutionMode.Framework,
            BlueprintJson = CompetencyBlueprintJson.Serialize(blueprint),
            ContextSnapshotJson = JsonSerializer.Serialize(new { skills = CvSkills })
        };
        foreach (var c in blueprint.Competencies)
            assessment.SkillResults.Add(new CandidateAssessmentSkillResult
            {
                Skill = c.SkillName, SkillScore = 0, TargetScore = 70, ImportanceWeight = c.Weight
            });

        // Profile chỉ có nhóm core — skill đo nhanh không gộp vào profile.
        var profile = new CandidateSkillPlan();
        foreach (var skill in CoreDotnet)
            profile.Items.Add(new CandidateSkillPlanItem
            {
                Skill = skill, CurrentScore = 0, TargetScore = 70, ImportanceWeight = 0.2
            });

        var stored = new List<CandidateRoadmap>();
        var roadmaps = new Mock<ICandidateRoadmapRepository>();
        roadmaps.Setup(r => r.ListAllByCandidateAsync(userId)).ReturnsAsync(new List<CandidateRoadmap>());
        roadmaps.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<CandidateRoadmap>>()))
            .Callback<IEnumerable<CandidateRoadmap>>(rows => stored.AddRange(rows))
            .Returns(Task.CompletedTask);
        var nodes = new Mock<IRoadmapNodeRepository>();
        nodes.Setup(n => n.ListBySkillAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<RoadmapNode>());
        var rag = new Mock<IRagService>();
        var knowledge = new Mock<IKnowledgeDocumentRepository>();
        knowledge.Setup(k => k.ListSystemDocumentIdsByFolderAsync(It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<Guid>());
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var service = new RoadmapRecommendationService(
            roadmaps.Object, nodes.Object, rag.Object, knowledge.Object, config);
        await service.RebuildFromDiagnosticAsync(userId, assessment, framework, profile);

        // 5 core + 13 skill đo nhanh = toàn bộ skill CV (biến thể đã gộp vào core).
        Assert.Equal(18, stored.Count);
        Assert.All(stored, r => Assert.Equal(CandidateRoadmapKind.Gap, r.Kind));

        var quick = stored.Where(r => !CoreDotnet.Contains(r.Skill)).ToList();
        Assert.Equal(ExpectedQuickChecks.OrderBy(s => s), quick.Select(r => r.Skill).OrderBy(s => s));
        Assert.All(quick, r => Assert.Contains("\"confidence\":\"screening\"", r.ExplanationJson ?? ""));
        Assert.All(quick, r => Assert.Contains(r.Items, i => i.Topic.EndsWith("fundamentals")));
        Assert.All(quick, r => Assert.Equal(3.5, r.PriorityScore));

        // Core học trước, skill đo nhanh xếp sau.
        var lastCoreOrder = stored.Where(r => CoreDotnet.Contains(r.Skill)).Max(r => r.DisplayOrder);
        Assert.All(quick, r => Assert.True(r.DisplayOrder > lastCoreOrder));

        // Không gọi LLM cho skill đo nhanh (dựng roadmap chạy lúc nộp bài).
        rag.Verify(r => r.GenerateAdaptiveRoadmapAsync(
            It.IsAny<RagAdaptiveRoadmapRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        rag.Verify(r => r.RecommendRoadmapAsync(
            It.IsAny<RagRoadmapRecommendRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Merge_DoesNotPutQuickCheckSkillsIntoCompetencyProfile()
    {
        var userId = Guid.NewGuid();
        var blueprint = DotnetBlueprintWithQuickChecks(maxQuickChecks: 30);
        var assessment = new CandidateAssessment
        {
            Id = Guid.NewGuid(),
            CandidateUserId = userId,
            Kind = CandidateAssessmentKind.Diagnostic,
            ResolutionMode = CompetencyResolutionMode.Framework,
            BlueprintJson = CompetencyBlueprintJson.Serialize(blueprint),
            ScopeSkillsJson = JsonSerializer.Serialize(blueprint.Competencies.Select(c => c.SkillName))
        };
        foreach (var c in blueprint.Competencies)
            assessment.SkillResults.Add(new CandidateAssessmentSkillResult
            {
                Skill = c.SkillName, SkillScore = 0, TargetScore = 70, ImportanceWeight = c.Weight
            });

        CandidateSkillPlan? saved = null;
        var plans = new Mock<ICandidateSkillPlanRepository>();
        plans.Setup(p => p.GetByCandidateUserIdAsync(userId)).ReturnsAsync((CandidateSkillPlan?)null);
        plans.Setup(p => p.AddAsync(It.IsAny<CandidateSkillPlan>()))
            .Callback<CandidateSkillPlan>(p => saved = p)
            .Returns(Task.CompletedTask);
        var frameworks = new Mock<ICompetencyFrameworkRepository>();
        frameworks.Setup(f => f.GetPolicyAsync()).ReturnsAsync(new CompetencyScoringPolicy());
        var rules = new Mock<ICompetencyLevelRuleRepository>();
        rules.Setup(r => r.ListAsync()).ReturnsAsync(new List<CompetencyLevelRule>());

        var service = new CompetencyProfileService(plans.Object, frameworks.Object, rules.Object);
        var merge = await service.MergeAsync(userId, assessment, null, blueprint, "vi");

        Assert.NotNull(saved);
        Assert.Equal(CoreDotnet.OrderBy(s => s), saved!.Items.Select(i => i.Skill).OrderBy(s => s));
        Assert.Equal(0, merge.Profile.OverallReadiness);
    }

    [Fact]
    public void ScreeningPlanner_DoesNotReofferVariantsAlreadyMeasured()
    {
        // ScopeSkillsJson lưu tên đã normalize (lowercase).
        var measured = CoreDotnet.Concat(ExpectedQuickChecks).Select(s => s.ToLowerInvariant()).ToList();

        var selected = CoachScreeningPlanner.SelectSkills(
            CvSkills, measured, existingRoadmapSkills: [], framework: null, maxSkills: 30);

        Assert.Empty(selected);
    }

    [Fact]
    public async Task Score_QuickCheckSkillsGetResults_ButOverallUsesCoreOnly()
    {
        var userId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var blueprint = new CompetencyBlueprint
        {
            SourceMode = CompetencyResolutionMode.Framework,
            TargetLevel = "Junior",
            Competencies =
            {
                new CompetencyItem { SkillName = "C#", TargetScore = 70, Weight = 1, Topics = { "OOP" } }
            }
        };
        blueprint.Competencies.AddRange(
            CoachQuickCheckSkills.BuildCompetencies(["Docker", "Git"], "Junior", 70, framework: null));

        var assessment = new CandidateAssessment
        {
            Id = Guid.NewGuid(),
            CandidateUserId = userId,
            QuestionSetId = setId,
            Kind = CandidateAssessmentKind.Diagnostic,
            Status = CandidateAssessmentStatus.ReadyToPractice,
            ScopeSkillsJson = """["C#","Docker","Git"]""",
            BlueprintJson = CompetencyBlueprintJson.Serialize(blueprint),
            ResolutionMode = CompetencyResolutionMode.Framework
        };
        var assessments = new Mock<ICandidateAssessmentRepository>();
        assessments.Setup(a => a.GetByQuestionSetIdAsync(setId)).ReturnsAsync(assessment);
        assessments.Setup(a => a.SaveScoredAssessmentAsync(
                It.IsAny<CandidateAssessment>(),
                It.IsAny<IReadOnlyList<CandidateAssessmentSkillResult>>()))
            .Returns((CandidateAssessment a, IReadOnlyList<CandidateAssessmentSkillResult> results) =>
            {
                a.SkillResults.Clear();
                foreach (var r in results) a.SkillResults.Add(r);
                return Task.CompletedTask;
            });
        assessments.Setup(a => a.UpdateReadinessAsync(
                It.IsAny<Guid>(), It.IsAny<double?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // C# trả lời tốt; Docker 0 điểm; Git chấm lỗi (tính 0).
        var questionIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var answerIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var answers = new Mock<ICandidateAnswerRepository>();
        answers.Setup(a => a.GetEntitiesBySessionIdAsync(sessionId)).ReturnsAsync(
            answerIds.Select((id, i) => new CandidateAnswer
            {
                Id = id, PracticeSessionId = sessionId, QuestionSetQuestionId = questionIds[i], AnswerText = "x"
            }).ToList());
        var feedbacks = new Mock<IAiFeedbackRepository>();
        feedbacks.Setup(f => f.GetBySessionIdAsync(sessionId)).ReturnsAsync(
        [
            new AiFeedback
            {
                CandidateAnswerId = answerIds[0], EvaluationStatus = AiFeedbackEvaluationStatus.Succeeded,
                DimensionScoresJson = """{"correctness":90,"relevance":90,"clarity":90}"""
            },
            new AiFeedback
            {
                CandidateAnswerId = answerIds[1], EvaluationStatus = AiFeedbackEvaluationStatus.Succeeded,
                DimensionScoresJson = """{"correctness":0,"relevance":0,"clarity":0}"""
            },
            new AiFeedback { CandidateAnswerId = answerIds[2], EvaluationStatus = AiFeedbackEvaluationStatus.Failed }
        ]);
        var marketplace = new Mock<ICandidateMarketplaceRepository>();
        marketplace.Setup(m => m.GetQuestionsSnapshotAsync(setId)).ReturnsAsync(
        [
            new PublishedQuestionRow { Id = questionIds[0], Order = 1, Question = "OOP?", Skill = "C#", Difficulty = "medium", QuestionType = "technical" },
            new PublishedQuestionRow { Id = questionIds[1], Order = 2, Question = "Docker image?", Skill = "Docker", Difficulty = "easy", QuestionType = "technical" },
            new PublishedQuestionRow { Id = questionIds[2], Order = 3, Question = "Git rebase?", Skill = "Git", Difficulty = "easy", QuestionType = "technical" }
        ]);
        var frameworks = new Mock<ICompetencyFrameworkRepository>();
        frameworks.Setup(f => f.GetPolicyAsync()).ReturnsAsync(new CompetencyScoringPolicy());

        // Merge lỗi → service dùng Overall tự tính, đúng chỗ cần kiểm tra không bị skill đo nhanh kéo xuống.
        var profile = new Mock<ICompetencyProfileService>();
        profile.Setup(p => p.MergeAsync(
                It.IsAny<Guid>(), It.IsAny<CandidateAssessment>(), It.IsAny<CompetencyFramework?>(),
                It.IsAny<CompetencyBlueprint?>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("merge down"));
        var roadmap = new Mock<IRoadmapRecommendationService>();

        var service = new CoachCompetencyService(
            Mock.Of<ICandidateProfileRepository>(),
            frameworks.Object,
            Mock.Of<ICompetencyFrameworkResolver>(),
            Mock.Of<ICompetencyRoleFamilyRepository>(),
            Mock.Of<ICompetencyResolver>(),
            Mock.Of<IAdaptiveBlueprintBuilder>(),
            assessments.Object,
            Mock.Of<ICandidateRoadmapRepository>(),
            Mock.Of<ICandidatePersonalSetJobRepository>(),
            feedbacks.Object,
            answers.Object,
            marketplace.Object,
            Mock.Of<IPracticeSessionRepository>(),
            Mock.Of<ISubscriptionGateService>(),
            Mock.Of<IJobScheduler>(),
            profile.Object,
            roadmap.Object,
            Mock.Of<ICoachKnowledgeViewService>(),
            Mock.Of<IKnowledgeDocumentRepository>(),
            Mock.Of<IRoadmapNodeRepository>());

        var result = await service.ScoreAssessmentFromSessionAsync(new PracticeSession
        {
            Id = sessionId, CandidateUserId = userId, QuestionSetId = setId
        });

        Assert.True(result.Scored);
        Assert.Equal(["C#", "Docker", "Git"], assessment.SkillResults.Select(r => r.Skill).OrderBy(s => s));
        Assert.Equal(0, assessment.SkillResults.Single(r => r.Skill == "Docker").SkillScore);
        Assert.Equal(0, assessment.SkillResults.Single(r => r.Skill == "Git").SkillScore);
        // Overall chỉ từ C# (90) — nếu tính cả Docker/Git thì sẽ thấp hơn.
        Assert.Equal(90, assessment.OverallReadiness);
        roadmap.Verify(r => r.RebuildFromDiagnosticAsync(
            userId, assessment, It.IsAny<CompetencyFramework?>(), It.IsAny<CandidateSkillPlan>()), Times.Once);
    }

    private static CompetencyBlueprint DotnetBlueprintWithQuickChecks(int maxQuickChecks)
    {
        var blueprint = new CompetencyBlueprint
        {
            SourceMode = CompetencyResolutionMode.Framework,
            RoleKey = "dotnet-backend",
            TargetRole = ".NET Backend Developer",
            TargetLevel = "Junior",
            Competencies = CoreDotnet.Select(skill => new CompetencyItem
            {
                SkillName = skill,
                Weight = 0.2,
                TargetScore = 70,
                Category = CompetencyCategory.RoleCore,
                Topics = [$"{skill} basics", $"{skill} in practice"]
            }).ToList()
        };

        var selection = CoachQuickCheckSkills.Select(CvSkills, CoreDotnet, maxQuickChecks);
        blueprint.Competencies.AddRange(
            CoachQuickCheckSkills.BuildCompetencies(selection.Selected, "Junior", 70, framework: null));
        return blueprint;
    }
}
