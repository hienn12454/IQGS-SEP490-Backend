using ApplicationLayer.DTOs.Candidate;
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

/// <summary>SCRUM-463: candidate chỉnh skill trên Phân tích CV.</summary>
public sealed class CoachUpdateSkillsTests
{
    private static readonly Guid UserId = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000001");

    private static CompetencyResolution UnsupportedResolution() => new(
        CompetencyResolutionMode.Unsupported,
        null,
        null,
        null,
        "",
        "Junior",
        null,
        null,
        new List<string>(),
        0,
        "test",
        null,
        false,
        new List<string>());

    private static CoachCompetencyService CreateService(Mock<ICandidateProfileRepository> profiles)
    {
        var resolver = new Mock<ICompetencyResolver>();
        resolver
            .Setup(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(UnsupportedResolution());

        var fwResolver = new Mock<ICompetencyFrameworkResolver>();
        fwResolver.Setup(r => r.GetCatalogAsync())
            .ReturnsAsync(new List<FrameworkCatalogEntry>());

        return new CoachCompetencyService(
            profiles.Object,
            Mock.Of<ICompetencyFrameworkRepository>(),
            fwResolver.Object,
            resolver.Object,
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
    }

    [Fact]
    public void NormalizeCoachSkills_DedupesCaseInsensitive_KeepsFirstCasing()
    {
        var result = CoachCompetencyService.NormalizeCoachSkills(
            new[] { "  React ", "react", "TypeScript", "TYPESCRIPT", "" });
        Assert.Equal(new[] { "React", "TypeScript" }, result);
    }

    [Fact]
    public void MergeCvEvaluationSkills_PreservesSummaryAndSuggestedRole()
    {
        var json = """{"skills":["Old"],"summary":"Hello","suggestedRole":"FE"}""";
        var merged = CoachCompetencyService.MergeCvEvaluationSkills(json, new[] { "React", "Next.js" });
        using var doc = System.Text.Json.JsonDocument.Parse(merged);
        var root = doc.RootElement;
        Assert.Equal("Hello", root.GetProperty("summary").GetString());
        Assert.Equal("FE", root.GetProperty("suggestedRole").GetString());
        var skills = root.GetProperty("skills").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Equal(new[] { "React", "Next.js" }, skills);
    }

    [Fact]
    public async Task UpdateCoachSkills_RejectsEmpty()
    {
        var profile = new CandidateProfile
        {
            UserId = UserId,
            TechStack = new[] { "React" },
            CvEvaluationJson = """{"skills":["React"],"summary":"x"}""",
            CoachContextConfirmed = false
        };
        var profiles = new Mock<ICandidateProfileRepository>();
        profiles.Setup(p => p.GetByUserIdAsync(UserId)).ReturnsAsync(profile);

        var svc = CreateService(profiles);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.UpdateCoachSkillsAsync(UserId, new UpdateCoachSkillsDto { Skills = new List<string> { "  ", "" } }));
        profiles.Verify(p => p.UpdateAsync(It.IsAny<CandidateProfile>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCoachSkills_PersistsTechStackAndJson_DoesNotConfirm()
    {
        var profile = new CandidateProfile
        {
            UserId = UserId,
            TechStack = new[] { "Angular", "Ionic" },
            CvEvaluationJson = """{"skills":["Angular","Ionic"],"summary":"FE eng","suggestedRole":"Frontend"}""",
            CoachContextConfirmed = false,
            TargetRole = "Frontend"
        };
        var profiles = new Mock<ICandidateProfileRepository>();
        profiles.Setup(p => p.GetByUserIdAsync(UserId)).ReturnsAsync(profile);
        profiles.Setup(p => p.UpdateAsync(It.IsAny<CandidateProfile>())).Returns(Task.CompletedTask);

        var svc = CreateService(profiles);
        var dto = await svc.UpdateCoachSkillsAsync(UserId, new UpdateCoachSkillsDto
        {
            Skills = new List<string> { "React", "react", "TypeScript", "Next.js" }
        });

        Assert.False(profile.CoachContextConfirmed);
        Assert.Equal(new[] { "React", "TypeScript", "Next.js" }, profile.TechStack);
        Assert.Contains("FE eng", profile.CvEvaluationJson ?? "", StringComparison.Ordinal);
        Assert.Contains("React", profile.CvEvaluationJson ?? "", StringComparison.Ordinal);
        Assert.DoesNotContain("Angular", profile.CvEvaluationJson ?? "", StringComparison.Ordinal);
        Assert.Contains("React", dto.Skills);
        Assert.Contains("TypeScript", dto.Skills);
        Assert.Contains("Next.js", dto.Skills);
        Assert.DoesNotContain("Angular", dto.Skills);
        Assert.False(dto.ContextConfirmed);
        profiles.Verify(p => p.UpdateAsync(profile), Times.Once);
    }
}
