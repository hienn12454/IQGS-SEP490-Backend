using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.DTOs.QuestionSet;
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

public sealed class CoachScoreAssessmentTests
{
    [Fact]
    public async Task Score_WhenNoAssessment_ReturnsSkippedNotThrow()
    {
        var svc = CreateService(
            assessments: Mock.Of<ICandidateAssessmentRepository>(a =>
                a.GetByQuestionSetIdAsync(It.IsAny<Guid>()) == Task.FromResult<CandidateAssessment?>(null)
                && a.ListByCandidateAsync(It.IsAny<Guid>()) == Task.FromResult(new List<CandidateAssessment>())),
            jobs: Mock.Of<ICandidatePersonalSetJobRepository>(j =>
                j.GetByQuestionSetIdIncludingInactiveAsync(It.IsAny<Guid>())
                    == Task.FromResult<CandidatePersonalSetJob?>(null)));

        var result = await svc.ScoreAssessmentFromSessionAsync(new PracticeSession
        {
            Id = Guid.NewGuid(),
            CandidateUserId = Guid.NewGuid(),
            QuestionSetId = Guid.NewGuid()
        });

        Assert.False(result.Scored);
        Assert.Equal("ASSESSMENT_NOT_FOUND", result.SkipReason);
    }

    [Fact]
    public async Task Score_DiagnosticWithBlueprint_MarksScoredAndRebuildsRoadmap()
    {
        var userId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var answerId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        var bp = new CompetencyBlueprint
        {
            SourceMode = CompetencyResolutionMode.Framework,
            TargetLevel = "Junior",
            Competencies =
            {
                new CompetencyItem { SkillName = "C#", TargetScore = 70, Weight = 1, Topics = { "OOP" } }
            }
        };
        var assessment = new CandidateAssessment
        {
            Id = assessmentId,
            CandidateUserId = userId,
            QuestionSetId = setId,
            Kind = CandidateAssessmentKind.Diagnostic,
            Status = CandidateAssessmentStatus.ReadyToPractice,
            ScopeSkillsJson = """["C#"]""",
            BlueprintJson = CompetencyBlueprintJson.Serialize(bp),
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

        var answers = new Mock<ICandidateAnswerRepository>();
        answers.Setup(a => a.GetEntitiesBySessionIdAsync(sessionId)).ReturnsAsync(
        [
            new CandidateAnswer { Id = answerId, PracticeSessionId = sessionId, QuestionSetQuestionId = questionId, AnswerText = "ok" }
        ]);

        var feedbacks = new Mock<IAiFeedbackRepository>();
        feedbacks.Setup(f => f.GetBySessionIdAsync(sessionId)).ReturnsAsync(
        [
            new AiFeedback
            {
                CandidateAnswerId = answerId,
                EvaluationStatus = AiFeedbackEvaluationStatus.Succeeded,
                Score = 90,
                DimensionScoresJson = """{"correctness":90,"relevance":88,"clarity":92}"""
            }
        ]);

        var marketplace = new Mock<ICandidateMarketplaceRepository>();
        marketplace.Setup(m => m.GetQuestionsSnapshotAsync(setId)).ReturnsAsync(
        [
            new PublishedQuestionRow
            {
                Id = questionId,
                Order = 1,
                Question = "What is OOP?",
                Skill = "C#",
                Difficulty = "medium",
                QuestionType = "technical"
            }
        ]);

        var frameworks = new Mock<ICompetencyFrameworkRepository>();
        frameworks.Setup(f => f.GetPolicyAsync()).ReturnsAsync(new CompetencyScoringPolicy
        {
            OverallReadyThreshold = 70,
            CorrectnessWeight = 0.5,
            RelevanceWeight = 0.3,
            ClarityWeight = 0.2,
            EasyDifficultyWeight = 1,
            MediumDifficultyWeight = 1,
            HardDifficultyWeight = 1,
            DevelopingMaxExclusive = 50,
            NearTargetMaxExclusive = 70,
            ReadyMaxExclusive = 85
        });

        var profile = new Mock<ICompetencyProfileService>();
        profile.Setup(p => p.MergeAsync(userId, It.IsAny<CandidateAssessment>(), null, It.IsAny<CompetencyBlueprint?>()))
            .ReturnsAsync(new ProfileMergeResult(
                new CandidateSkillPlan
                {
                    Items =
                    {
                        new CandidateSkillPlanItem { Skill = "C#", CurrentScore = 90, TargetScore = 70, ImportanceWeight = 1 }
                    },
                    OverallReadiness = 90,
                    ReadinessStatus = CompetencyReadinessStatus.Strong
                },
                null, null, [], "Junior", "ok", 1));

        var roadmap = new Mock<IRoadmapRecommendationService>();
        roadmap.Setup(r => r.RebuildFromDiagnosticAsync(
                userId, It.IsAny<CandidateAssessment>(), null, It.IsAny<CandidateSkillPlan>()))
            .Returns(Task.CompletedTask);

        var svc = CreateService(
            assessments: assessments.Object,
            answers: answers.Object,
            feedbacks: feedbacks.Object,
            marketplace: marketplace.Object,
            frameworks: frameworks.Object,
            profile: profile.Object,
            roadmap: roadmap.Object);

        var result = await svc.ScoreAssessmentFromSessionAsync(new PracticeSession
        {
            Id = sessionId,
            CandidateUserId = userId,
            QuestionSetId = setId
        });

        Assert.True(result.Scored);
        Assert.Equal(assessmentId, result.AssessmentId);
        Assert.Equal(CandidateAssessmentStatus.Scored, result.Status);
        Assert.True(result.RoadmapUpdated);
        Assert.Equal(CandidateAssessmentStatus.Scored, assessment.Status);
        Assert.NotEmpty(assessment.SkillResults);
        roadmap.Verify(r => r.RebuildFromDiagnosticAsync(
            userId, It.IsAny<CandidateAssessment>(), null, It.IsAny<CandidateSkillPlan>()), Times.Once);
    }

    [Fact]
    public async Task Score_RoadmapFail_StillReturnsScoredFalseRoadmapFlag()
    {
        var userId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var answerId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        var bp = new CompetencyBlueprint
        {
            Competencies = { new CompetencyItem { SkillName = "SQL", TargetScore = 70, Weight = 1 } }
        };
        var assessment = new CandidateAssessment
        {
            Id = assessmentId,
            CandidateUserId = userId,
            QuestionSetId = setId,
            Kind = CandidateAssessmentKind.Diagnostic,
            Status = CandidateAssessmentStatus.ReadyToPractice,
            BlueprintJson = CompetencyBlueprintJson.Serialize(bp)
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

        var answers = new Mock<ICandidateAnswerRepository>();
        answers.Setup(a => a.GetEntitiesBySessionIdAsync(sessionId)).ReturnsAsync(
        [
            new CandidateAnswer { Id = answerId, PracticeSessionId = sessionId, QuestionSetQuestionId = questionId, AnswerText = "ok" }
        ]);
        var feedbacks = new Mock<IAiFeedbackRepository>();
        feedbacks.Setup(f => f.GetBySessionIdAsync(sessionId)).ReturnsAsync(
        [
            new AiFeedback
            {
                CandidateAnswerId = answerId,
                EvaluationStatus = AiFeedbackEvaluationStatus.Succeeded,
                Score = 80
            }
        ]);
        var marketplace = new Mock<ICandidateMarketplaceRepository>();
        marketplace.Setup(m => m.GetQuestionsSnapshotAsync(setId)).ReturnsAsync(
        [
            new PublishedQuestionRow { Id = questionId, Order = 1, Question = "q", Skill = "SQL", Difficulty = "easy", QuestionType = "technical" }
        ]);
        var frameworks = new Mock<ICompetencyFrameworkRepository>();
        frameworks.Setup(f => f.GetPolicyAsync()).ReturnsAsync(new CompetencyScoringPolicy
        {
            OverallReadyThreshold = 70,
            CorrectnessWeight = 1,
            RelevanceWeight = 0,
            ClarityWeight = 0,
            EasyDifficultyWeight = 1,
            MediumDifficultyWeight = 1,
            HardDifficultyWeight = 1,
            DevelopingMaxExclusive = 50,
            NearTargetMaxExclusive = 70,
            ReadyMaxExclusive = 85
        });
        var profile = new Mock<ICompetencyProfileService>();
        profile.Setup(p => p.MergeAsync(userId, It.IsAny<CandidateAssessment>(), null, It.IsAny<CompetencyBlueprint?>()))
            .ReturnsAsync(new ProfileMergeResult(
                new CandidateSkillPlan
                {
                    Items = { new CandidateSkillPlanItem { Skill = "SQL", CurrentScore = 80, TargetScore = 70, ImportanceWeight = 1 } },
                    OverallReadiness = 80
                },
                null, null, [], null, "", 1));

        var roadmap = new Mock<IRoadmapRecommendationService>();
        roadmap.Setup(r => r.RebuildFromDiagnosticAsync(
                It.IsAny<Guid>(), It.IsAny<CandidateAssessment>(), It.IsAny<CompetencyFramework?>(), It.IsAny<CandidateSkillPlan>()))
            .ThrowsAsync(new InvalidOperationException("rag down"));

        var svc = CreateService(
            assessments: assessments.Object,
            answers: answers.Object,
            feedbacks: feedbacks.Object,
            marketplace: marketplace.Object,
            frameworks: frameworks.Object,
            profile: profile.Object,
            roadmap: roadmap.Object);

        var result = await svc.ScoreAssessmentFromSessionAsync(new PracticeSession
        {
            Id = sessionId,
            CandidateUserId = userId,
            QuestionSetId = setId
        });

        Assert.True(result.Scored);
        Assert.False(result.RoadmapUpdated);
        Assert.Equal(CandidateAssessmentStatus.Scored, assessment.Status);
    }

    private static CoachCompetencyService CreateService(
        ICandidateAssessmentRepository? assessments = null,
        ICandidatePersonalSetJobRepository? jobs = null,
        ICandidateAnswerRepository? answers = null,
        IAiFeedbackRepository? feedbacks = null,
        ICandidateMarketplaceRepository? marketplace = null,
        ICompetencyFrameworkRepository? frameworks = null,
        ICompetencyProfileService? profile = null,
        IRoadmapRecommendationService? roadmap = null)
        => new(
            Mock.Of<ICandidateProfileRepository>(),
            frameworks ?? Mock.Of<ICompetencyFrameworkRepository>(),
            Mock.Of<ICompetencyFrameworkResolver>(),
            Mock.Of<ICompetencyResolver>(),
            Mock.Of<IAdaptiveBlueprintBuilder>(),
            assessments ?? Mock.Of<ICandidateAssessmentRepository>(),
            Mock.Of<ICandidateRoadmapRepository>(),
            jobs ?? Mock.Of<ICandidatePersonalSetJobRepository>(),
            feedbacks ?? Mock.Of<IAiFeedbackRepository>(),
            answers ?? Mock.Of<ICandidateAnswerRepository>(),
            marketplace ?? Mock.Of<ICandidateMarketplaceRepository>(),
            Mock.Of<ISubscriptionGateService>(),
            Mock.Of<IJobScheduler>(),
            profile ?? Mock.Of<ICompetencyProfileService>(),
            roadmap ?? Mock.Of<IRoadmapRecommendationService>());
}
