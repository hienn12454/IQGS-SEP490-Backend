using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Moq;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

public sealed class AdaptiveCompetencyUnitTests
{
    [Fact]
    public void TargetScorePolicy_NullJson_FallsBackToOverallReadyThreshold()
    {
        var policy = new CompetencyScoringPolicy { OverallReadyThreshold = 70 };
        Assert.Equal(70, CompetencyTargetScorePolicy.Resolve(policy, "Junior"));
    }

    [Fact]
    public void TargetScorePolicy_JsonByLevel_UsesMatchingKey()
    {
        var policy = new CompetencyScoringPolicy
        {
            OverallReadyThreshold = 70,
            TargetScoreByLevelJson = """{"Fresher":60,"Junior":70,"Middle":80,"Senior":85}"""
        };
        Assert.Equal(60, CompetencyTargetScorePolicy.Resolve(policy, "Fresher"));
        Assert.Equal(85, CompetencyTargetScorePolicy.Resolve(policy, "Senior"));
        Assert.Equal(70, CompetencyTargetScorePolicy.Resolve(policy, "Unknown"));
    }

    [Fact]
    public void FrameworkBlueprintBuilder_MapsSkillsAndNormalizesCategory()
    {
        var fw = new CompetencyFramework
        {
            Id = Guid.NewGuid(),
            RoleKey = "java-backend",
            DisplayRole = "Java Backend",
            TargetLevel = "Junior"
        };
        var skills = new List<CompetencyFrameworkSkill>
        {
            new() { Skill = "Spring", ImportanceWeight = 0.6, TargetScore = 72, RequiredDifficulty = "easy", TopicsJson = """["DI"]""" },
            new() { Skill = "SQL", ImportanceWeight = 0.4, TargetScore = 68, RequiredDifficulty = "hard", TopicsJson = """["Index"]""" }
        };
        var bp = FrameworkBlueprintBuilder.Build(fw, "Java Backend", skills, new CompetencyScoringPolicy { OverallReadyThreshold = 70 });
        Assert.Equal(CompetencyResolutionMode.Framework, bp.SourceMode);
        Assert.Equal(CompetencyCategory.Fundamental, bp.Competencies[0].Category);
        Assert.Equal(CompetencyCategory.Advanced, bp.Competencies[1].Category);
        Assert.Equal(72, bp.Competencies[0].TargetScore);
    }

    [Fact]
    public void GapAnalysis_PriorityIsMaxGapTimesWeight()
    {
        var gaps = GapAnalysisService.Analyze(
        [
            ("Spring", 40, 70, 0.6),
            ("SQL", 80, 70, 0.4)
        ]);
        Assert.Equal("Spring", gaps[0].Skill);
        Assert.Equal(18, gaps[0].PriorityScore);
        Assert.Equal(0, gaps[1].PriorityScore);
    }

    [Fact]
    public async Task AdaptiveBlueprintBuilder_EmptyRetrieval_DoesNotFallbackFramework()
    {
        var rag = new Mock<IRagService>();
        rag.Setup(r => r.RetrieveCompetencyContextAsync(It.IsAny<RagCompetencyContextRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagCompetencyContextResult { Success = false, Error = "EMPTY_RETRIEVAL" });

        var builder = new AdaptiveBlueprintBuilder(rag.Object);
        var resolution = new CompetencyResolution(
            CompetencyResolutionMode.Adaptive, "backend", null, null, "Backend", "Junior",
            "backend", "Backend Developer", ["Go"], 0.88, "adaptive", null, false, ["Backend Developer"]);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            builder.BuildAsync(resolution, ["Go"], new CompetencyScoringPolicy { OverallReadyThreshold = 70 }));
        Assert.Contains("ADAPTIVE_BLUEPRINT_GENERATION_FAILED", ex.Message);
        rag.Verify(r => r.GenerateAdaptiveCompetencyBlueprintAsync(
            It.IsAny<RagAdaptiveBlueprintRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void DiagnosticFromBlueprint_DoesNotRequireFrameworkEntity()
    {
        var bp = new CompetencyBlueprint
        {
            TargetRole = "Backend",
            TargetLevel = "Junior",
            Competencies =
            {
                new CompetencyItem { SkillName = "Go", Category = CompetencyCategory.RoleCore, Weight = 0.5, Topics = ["goroutine"] },
                new CompetencyItem { SkillName = "SQL", Category = CompetencyCategory.Fundamental, Weight = 0.5, Topics = ["index"] }
            }
        };
        var plan = DiagnosticBlueprintBuilder.BuildDiagnostic(bp);
        Assert.NotNull(plan);
    }

    [Fact]
    public void TargetReadiness_OverallBelowThreshold_IsNotReady()
    {
        var dto = new ApplicationLayer.DTOs.Candidate.CoachAssessmentDto
        {
            OverallReadiness = 56,
            Skills =
            {
                new ApplicationLayer.DTOs.Candidate.CoachSkillResultDto
                {
                    Skill = "Go", SkillScore = 50, TargetScore = 70, Gap = 20, ImportanceWeight = 1
                }
            }
        };
        TargetReadinessMapper.Apply(dto, new CompetencyScoringPolicy { OverallReadyThreshold = 70 }, "Junior", "Fresher");
        Assert.Equal(CompetencyTargetReadinessStatus.NotReady, dto.TargetReadinessStatus);
        Assert.Equal("Junior", dto.TargetLevel);
        Assert.Equal("Fresher", dto.EstimatedBand);
        Assert.Single(dto.SkillGaps);
        Assert.Equal(80, dto.ReadinessPercent); // 56/70*100
    }

    [Fact]
    public void FallbackScoringSkills_UsesDistinctNormalizedNames()
    {
        var policy = new CompetencyScoringPolicy { OverallReadyThreshold = 70 };
        var skills = CompetencyProfileService.FallbackScoringSkills(
            [" Go ", "go", "SQL"], policy, "Junior");
        Assert.Equal(2, skills.Count);
        Assert.Contains(skills, s => s.Skill == "go");
        Assert.Contains(skills, s => s.Skill == "sql");
        Assert.All(skills, s => Assert.Equal(70, s.TargetScore));
    }
}
