using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Helpers;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using System.Text.Json;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

public sealed class StudioPlanSettingsSnapshotHelperTests
{
    [Fact]
    public void BuildFrom_NormalizesFocusWeightsAndProducesFingerprint()
    {
        var settings = new StudioSettings
        {
            NumberOfQuestions = 10,
            Difficulty = QuestionDifficulty.Medium,
            QuestionDistributionJson = JsonSerializer.Serialize(new[]
            {
                new { category = "technical", percentage = 60, questionCount = 6 },
                new { category = "behavioral", percentage = 40, questionCount = 4 }
            }),
            QuestionStylesJson = JsonSerializer.Serialize(new[] { "system_design", "coding" }),
            CodeTemplatesJson = JsonSerializer.Serialize(new[] { "BUG_DETECTION", "CODE_COMPLETION" }),
            FocusAreas =
            {
                new StudioFocusArea { Name = "API", Weight = 0.6m, OrderIndex = 1, IsActive = true },
                new StudioFocusArea { Name = "Behavior", Weight = 40m, OrderIndex = 2, IsActive = true }
            }
        };

        var snapshot = StudioPlanSettingsSnapshotHelper.BuildFrom(settings);

        Assert.Equal(60m, snapshot.FocusAreas[0].Weight);
        Assert.Equal(40m, snapshot.FocusAreas[1].Weight);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Fingerprint));
        Assert.Equal(snapshot.Fingerprint.Length, 64);
    }

    [Fact]
    public void EmbedAndExtract_RoundTripPreservesFingerprint()
    {
        var settings = new StudioSettings
        {
            NumberOfQuestions = 8,
            Difficulty = QuestionDifficulty.Easy,
            QuestionDistributionJson = JsonSerializer.Serialize(new[]
            {
                new { category = "technical", percentage = 75, questionCount = 6 },
                new { category = "behavioral", percentage = 25, questionCount = 2 }
            }),
            FocusAreas =
            {
                new StudioFocusArea { Name = "Core", Weight = 100m, OrderIndex = 0, IsActive = true }
            }
        };

        var built = StudioPlanSettingsSnapshotHelper.BuildFrom(settings);
        var embeddedJson = StudioPlanSettingsSnapshotHelper.EmbedInSourcePlanJson(
            "{\"roleTitle\":\"Dev\"}", built);
        var extracted = StudioPlanSettingsSnapshotHelper.TryExtract(embeddedJson);

        Assert.NotNull(extracted);
        Assert.Equal(built.Fingerprint, extracted!.Fingerprint);
    }

    [Fact]
    public void IsStale_TrueWhenSettingsChange()
    {
        var settings = new StudioSettings
        {
            NumberOfQuestions = 10,
            Difficulty = QuestionDifficulty.Medium,
            QuestionDistributionJson = JsonSerializer.Serialize(new[]
            {
                new { category = "technical", percentage = 100, questionCount = 10 }
            })
        };
        var snapshot = StudioPlanSettingsSnapshotHelper.BuildFrom(settings);
        settings.NumberOfQuestions = 12;

        Assert.True(StudioPlanSettingsSnapshotHelper.IsStale(snapshot, settings));
    }

    [Fact]
    public void IsStale_FalseWhenUnchanged()
    {
        var settings = new StudioSettings
        {
            NumberOfQuestions = 10,
            Difficulty = QuestionDifficulty.Medium,
            QuestionDistributionJson = JsonSerializer.Serialize(new[]
            {
                new { category = "technical", percentage = 100, questionCount = 10 }
            })
        };
        var snapshot = StudioPlanSettingsSnapshotHelper.BuildFrom(settings);

        Assert.False(StudioPlanSettingsSnapshotHelper.IsStale(snapshot, settings));
    }
}
