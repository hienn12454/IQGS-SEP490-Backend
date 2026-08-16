using ApplicationLayer.Helpers;
using DomainLayer.Constants;
using DomainLayer.Exceptions;
using Xunit;

namespace ApplicationLayer.UnitTests.Hr;

public sealed class SkillFitCalculatorTests
{
    [Fact]
    public void Jaccard_AllOverlap_Is100()
    {
        var r = SkillFitCalculator.Compute(new[] { "C#", "SQL" }, new[] { "c#", "sql" });
        Assert.Equal(100, r.FitPercent);
        Assert.Equal(2, r.Matched.Count);
        Assert.Empty(r.MissingOnCv);
        Assert.Empty(r.ExtraOnCv);
    }

    [Fact]
    public void Jaccard_HalfOverlap_Is50()
    {
        var r = SkillFitCalculator.Compute(new[] { "react", "node" }, new[] { "react", "python" });
        Assert.Equal(33.3, r.FitPercent);
        Assert.Single(r.Matched);
        Assert.Contains("node", r.MissingOnCv);
        Assert.Contains("python", r.ExtraOnCv);
    }
}

public sealed class RecommendationP1RulesTests
{
    [Fact]
    public void Restore_Invited_ThrowsConflict()
    {
        Assert.Throws<ConflictException>(() => RecommendationP1Rules.EnsureCanRestore(CandidateRecommendationStatus.Invited));
    }

    [Fact]
    public void Restore_Dismissed_Ok()
    {
        RecommendationP1Rules.EnsureCanRestore(CandidateRecommendationStatus.Dismissed);
    }

    [Fact]
    public void Compare_DifferentSets_ThrowsBadRequest()
    {
        Assert.Throws<BadRequestException>(() =>
            RecommendationP1Rules.EnsureSameQuestionSet(new[] { Guid.NewGuid(), Guid.NewGuid() }));
    }

    [Fact]
    public void Compare_WrongCount_ThrowsBadRequest()
    {
        Assert.Throws<BadRequestException>(() => RecommendationP1Rules.NormalizeCompareIds(new[] { Guid.NewGuid() }));
        Assert.Throws<BadRequestException>(() =>
            RecommendationP1Rules.NormalizeCompareIds(Enumerable.Range(0, 4).Select(_ => Guid.NewGuid())));
    }

    [Fact]
    public void View_Idempotent_KeepsExistingTimestamp()
    {
        var existing = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(existing, RecommendationP1Rules.NextViewedAt(existing, DateTime.UtcNow));
        Assert.NotNull(RecommendationP1Rules.NextViewedAt(null, DateTime.UtcNow));
    }

    [Fact]
    public void Invite_OnlineWithoutLink_Throws()
    {
        Assert.Throws<BadRequestException>(() =>
            RecommendationP1Rules.EnsureInviteSchedule(null, null, "ONLINE", null, null));
    }
}
