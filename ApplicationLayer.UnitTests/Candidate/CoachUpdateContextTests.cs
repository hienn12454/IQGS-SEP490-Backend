using ApplicationLayer.DTOs.Candidate;
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

/// <summary>SCRUM-458: Confirm Goal không đè skill CV / InterviewGoal.</summary>
public sealed class CoachUpdateContextTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task UpdateContext_DoesNotOverwriteCvSkillsOrInterviewGoal()
    {
        const string originalCvJson =
            """{"skills":["C#","EF Core"],"summary":"Backend junior","suggestedRole":".NET Backend"}""";
        var profile = new CandidateProfile
        {
            UserId = UserId,
            SuggestedRole = ".NET Backend Developer",
            TargetRole = "Backend",
            TargetLevel = "Junior",
            SelfAssessedLevel = "Junior",
            YearsOfExperience = 1,
            InterviewGoal = "Old interview goal",
            TechStack = new[] { "C#", "EF Core" },
            CvEvaluationJson = originalCvJson,
            CoachContextConfirmed = false
        };

        var profiles = new Mock<ICandidateProfileRepository>();
        profiles.Setup(p => p.GetByUserIdAsync(UserId)).ReturnsAsync(profile);
        profiles.Setup(p => p.UpdateAsync(It.IsAny<CandidateProfile>())).Returns(Task.CompletedTask);

        var fw = new CompetencyFramework
        {
            Id = Guid.NewGuid(),
            RoleKey = "dotnet-backend",
            DisplayRole = ".NET Backend Developer",
            TargetLevel = "Junior",
            Technology = "ASP.NET Core",
            Status = CompetencyFrameworkStatus.Active,
            Skills = new List<CompetencyFrameworkSkill>
            {
                new() { Skill = "C#", ImportanceWeight = 0.5, TargetScore = 70, RequiredDifficulty = "medium", SortOrder = 1 },
                new() { Skill = "EF Core", ImportanceWeight = 0.5, TargetScore = 65, RequiredDifficulty = "medium", SortOrder = 2 }
            }
        };

        var frameworks = new Mock<ICompetencyFrameworkRepository>();
        var frameworkResolver = new Mock<ICompetencyFrameworkResolver>();
        frameworkResolver.Setup(r => r.GetCatalogAsync())
            .ReturnsAsync(new List<FrameworkCatalogEntry>
            {
                new(fw.RoleKey, fw.DisplayRole, fw.Technology, new List<string> { fw.TargetLevel }, "Provisional")
            });

        var competencyResolver = new Mock<ICompetencyResolver>();
        competencyResolver
            .Setup(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(new CompetencyResolution(
                CompetencyResolutionMode.Framework,
                fw.RoleKey,
                fw.RoleKey,
                fw.Id,
                fw.DisplayRole,
                fw.TargetLevel,
                "backend",
                "Backend Developer",
                new List<string> { "C#", "EF Core" },
                0.97,
                "Exact supported framework matched.",
                fw,
                false,
                new List<string> { fw.DisplayRole }));

        var svc = new CoachCompetencyService(
            profiles.Object,
            frameworks.Object,
            frameworkResolver.Object,
            competencyResolver.Object,
            Mock.Of<IAdaptiveBlueprintBuilder>(),
            Mock.Of<ICandidateAssessmentRepository>(),
            Mock.Of<ICandidateRoadmapRepository>(),
            Mock.Of<ICandidatePersonalSetJobRepository>(),
            Mock.Of<IAiFeedbackRepository>(),
            Mock.Of<ICandidateAnswerRepository>(),
            Mock.Of<ICandidateMarketplaceRepository>(),
            Mock.Of<ISubscriptionGateService>(),
            Mock.Of<IJobScheduler>(),
            Mock.Of<ICompetencyProfileService>(),
            Mock.Of<IRoadmapRecommendationService>());

        await svc.UpdateContextAsync(UserId, new UpdateCoachContextDto
        {
            TargetRole = ".NET Backend Developer",
            SelfAssessedLevel = "Middle",
            TargetLevel = "Middle",
            YearsOfExperience = 2
        });

        Assert.Equal(".NET Backend Developer", profile.TargetRole);
        Assert.Equal("Middle", profile.TargetLevel);
        Assert.Equal("Middle", profile.SelfAssessedLevel);
        Assert.Equal(2, profile.YearsOfExperience);
        Assert.True(profile.CoachContextConfirmed);

        // Không đè skill CV / TechStack và không đụng InterviewGoal cũ.
        Assert.Equal(originalCvJson, profile.CvEvaluationJson);
        Assert.Equal(new[] { "C#", "EF Core" }, profile.TechStack);
        Assert.Equal("Old interview goal", profile.InterviewGoal);

        profiles.Verify(p => p.UpdateAsync(profile), Times.Once);
    }

    [Fact]
    public async Task UpdateContext_RejectsTargetLevelBelowSelfAssessed()
    {
        var profile = new CandidateProfile
        {
            UserId = UserId,
            TargetRole = "Backend",
            TargetLevel = "Junior",
            SelfAssessedLevel = "Junior",
            TechStack = new[] { "C#" },
            CvEvaluationJson = """{"skills":["C#"]}"""
        };

        var profiles = new Mock<ICandidateProfileRepository>();
        profiles.Setup(p => p.GetByUserIdAsync(UserId)).ReturnsAsync(profile);

        var svc = new CoachCompetencyService(
            profiles.Object,
            Mock.Of<ICompetencyFrameworkRepository>(),
            Mock.Of<ICompetencyFrameworkResolver>(),
            Mock.Of<ICompetencyResolver>(),
            Mock.Of<IAdaptiveBlueprintBuilder>(),
            Mock.Of<ICandidateAssessmentRepository>(),
            Mock.Of<ICandidateRoadmapRepository>(),
            Mock.Of<ICandidatePersonalSetJobRepository>(),
            Mock.Of<IAiFeedbackRepository>(),
            Mock.Of<ICandidateAnswerRepository>(),
            Mock.Of<ICandidateMarketplaceRepository>(),
            Mock.Of<ISubscriptionGateService>(),
            Mock.Of<IJobScheduler>(),
            Mock.Of<ICompetencyProfileService>(),
            Mock.Of<IRoadmapRecommendationService>());

        var ex = await Assert.ThrowsAsync<DomainLayer.Exceptions.BadRequestException>(() =>
            svc.UpdateContextAsync(UserId, new UpdateCoachContextDto
            {
                TargetRole = "Backend",
                SelfAssessedLevel = "Junior",
                TargetLevel = "Fresher"
            }));

        Assert.Contains("cao hơn", ex.Message, StringComparison.OrdinalIgnoreCase);
        profiles.Verify(p => p.UpdateAsync(It.IsAny<CandidateProfile>()), Times.Never);
    }
}
