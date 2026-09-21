using ApplicationLayer.Helpers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApplicationLayer.UnitTests.Knowledge;

public sealed class CoachRoadmapKnowledgeFolderTests
{
    [Fact]
    public void Resolve_NullConfig_ReturnsDefault()
    {
        Assert.Equal(CoachRoadmapKnowledgeFolder.DefaultFolder, CoachRoadmapKnowledgeFolder.Resolve(null));
    }

    [Fact]
    public void Resolve_ConfiguredFolder_Normalizes()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CoachRoadmapKnowledgeFolder.ConfigKey] = "Coach Roadmap"
            })
            .Build();
        Assert.Equal("coach-roadmap", CoachRoadmapKnowledgeFolder.Resolve(config));
    }
}
