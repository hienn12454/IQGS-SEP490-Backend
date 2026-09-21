using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

/// <summary>SCRUM-461: gợi ý level liền kề khi Target Readiness = READY.</summary>
public class NextLevelSuggestionTests
{
    [Theory]
    [InlineData("Fresher", "Junior")]
    [InlineData("Junior", "Middle")]
    [InlineData("Middle", "Senior")]
    [InlineData("fresher", "Junior")]
    public void TryGetNextLevel_AdjacentLadder(string current, string expected)
        => Assert.Equal(expected, NextLevelSuggestion.TryGetNextLevel(current));

    [Theory]
    [InlineData("Senior")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Lead")]
    public void TryGetNextLevel_NoNext_ReturnsNull(string? current)
        => Assert.Null(NextLevelSuggestion.TryGetNextLevel(current));

    [Fact]
    public void Build_NotReady_NoSuggestion()
    {
        var r = NextLevelSuggestion.Build(
            CompetencyTargetReadinessStatus.NotReady,
            CoachSeniorityLevel.Junior,
            CompetencyResolutionMode.Framework,
            nextFrameworkExistsExact: true);
        Assert.Null(r.SuggestedNextLevel);
        Assert.False(r.Available);
    }

    [Fact]
    public void Build_ReadyFramework_WithNextCatalog_Available()
    {
        var r = NextLevelSuggestion.Build(
            CompetencyTargetReadinessStatus.Ready,
            CoachSeniorityLevel.Junior,
            CompetencyResolutionMode.Framework,
            nextFrameworkExistsExact: true);
        Assert.Equal(CoachSeniorityLevel.Middle, r.SuggestedNextLevel);
        Assert.True(r.Available);
        Assert.Contains("Middle", r.Message ?? "");
    }

    [Fact]
    public void Build_ReadyFramework_MissingNextCatalog_Unavailable()
    {
        var r = NextLevelSuggestion.Build(
            CompetencyTargetReadinessStatus.Ready,
            CoachSeniorityLevel.Fresher,
            CompetencyResolutionMode.Framework,
            nextFrameworkExistsExact: false);
        Assert.Equal(CoachSeniorityLevel.Junior, r.SuggestedNextLevel);
        Assert.False(r.Available);
        Assert.Contains("Chưa có competency framework", r.Message ?? "");
    }

    [Fact]
    public void Build_ReadyAdaptive_AlwaysAvailable()
    {
        var r = NextLevelSuggestion.Build(
            CompetencyTargetReadinessStatus.Ready,
            CoachSeniorityLevel.Fresher,
            CompetencyResolutionMode.Adaptive,
            nextFrameworkExistsExact: false);
        Assert.Equal(CoachSeniorityLevel.Junior, r.SuggestedNextLevel);
        Assert.True(r.Available);
        Assert.Contains("adaptive", r.Message ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_ReadySenior_NoSuggestion()
    {
        var r = NextLevelSuggestion.Build(
            CompetencyTargetReadinessStatus.Ready,
            CoachSeniorityLevel.Senior,
            CompetencyResolutionMode.Framework,
            nextFrameworkExistsExact: true);
        Assert.Null(r.SuggestedNextLevel);
        Assert.False(r.Available);
    }
}
