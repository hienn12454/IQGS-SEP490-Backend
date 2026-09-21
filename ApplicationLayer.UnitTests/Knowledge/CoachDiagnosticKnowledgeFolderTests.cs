using ApplicationLayer.Helpers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApplicationLayer.UnitTests.Knowledge;

/// <summary>Folder SYSTEM cho sinh đề Coach (test-candidate).</summary>
public sealed class CoachDiagnosticKnowledgeFolderTests
{
    [Fact]
    public void Resolve_NullConfig_ReturnsDefault()
    {
        Assert.Equal(CoachDiagnosticKnowledgeFolder.DefaultFolder, CoachDiagnosticKnowledgeFolder.Resolve(null));
    }

    [Fact]
    public void Resolve_MissingKey_ReturnsDefault()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        Assert.Equal("test-candidate", CoachDiagnosticKnowledgeFolder.Resolve(config));
    }

    [Fact]
    public void Resolve_ConfiguredFolder_Normalizes()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CoachDiagnosticKnowledgeFolder.ConfigKey] = "Test Candidate"
            })
            .Build();
        Assert.Equal("test-candidate", CoachDiagnosticKnowledgeFolder.Resolve(config));
    }

    [Fact]
    public void Resolve_InvalidFolder_FallsBackToDefault()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CoachDiagnosticKnowledgeFolder.ConfigKey] = "!!!"
            })
            .Build();
        // Normalize("!!!") → null → default
        Assert.Equal("test-candidate", CoachDiagnosticKnowledgeFolder.Resolve(config));
    }

    [Fact]
    public void Normalize_TestCandidate_IsValidFolderKey()
    {
        Assert.Equal("test-candidate", KnowledgeFolderHelper.Normalize("test-candidate"));
        Assert.Equal("test-candidate", KnowledgeFolderHelper.Normalize("Test-Candidate"));
    }

    [Fact]
    public void SerializeKbSource_Inferred_RoundTrips()
    {
        var json = CoachDiagnosticKnowledgeFolder.SerializeKbSource("inferred");
        Assert.Contains("kbSource", json);
        Assert.Equal(
            CoachDiagnosticKnowledgeFolder.KbSourceInferred,
            CoachDiagnosticKnowledgeFolder.ParseKbSourceFromGapSkillsJson(json));
    }

    [Fact]
    public void SerializeKbSource_System_RoundTrips()
    {
        var json = CoachDiagnosticKnowledgeFolder.SerializeKbSource(CoachDiagnosticKnowledgeFolder.KbSourceSystem);
        Assert.Equal(
            CoachDiagnosticKnowledgeFolder.KbSourceSystem,
            CoachDiagnosticKnowledgeFolder.ParseKbSourceFromGapSkillsJson(json));
    }

    [Fact]
    public void ParseKbSource_ArrayGapSkills_ReturnsNull()
    {
        Assert.Null(CoachDiagnosticKnowledgeFolder.ParseKbSourceFromGapSkillsJson("[\"C#\"]"));
        Assert.Null(CoachDiagnosticKnowledgeFolder.ParseKbSourceFromGapSkillsJson("[]"));
    }
}
