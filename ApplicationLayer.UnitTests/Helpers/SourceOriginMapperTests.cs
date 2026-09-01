using ApplicationLayer.Studio.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Helpers;

/// <summary>SCRUM-419: scope HR/Admin trên plan sources và citation.</summary>
public class SourceOriginMapperTests
{
    [Fact]
    public void ExtractCitationSourcesWithScope_ReadsKnowledgeBase()
    {
        const string json = """
            {
              "citations": [
                { "sourceFile": "job-description", "knowledgeBase": "hr", "chunkIndex": 0 },
                { "sourceFile": "policy.pdf", "knowledgeBase": "system", "chunkIndex": 2 },
                { "sourceFile": "hr-guide.pdf", "knowledgeBase": "hr", "chunkIndex": 1 }
              ]
            }
            """;

        var items = StudioRagPlanMapper.ExtractCitationSourcesWithScope(json);

        Assert.Equal(3, items.Count);
        Assert.Equal("JD", items.First(x => x.File == "job-description").Scope);
        Assert.Equal("SYSTEM", items.First(x => x.File == "policy.pdf").Scope);
        Assert.Equal("HR", items.First(x => x.File == "hr-guide.pdf").Scope);
    }

    [Fact]
    public void BuildPlanSourceDetails_MapsMetaAndFiles()
    {
        var sourcesUsed = new List<string>
        {
            "job-description",
            "knowledge-documents:2",
            "rag-retrieve",
            "policy.pdf"
        };

        const string json = """
            {
              "citations": [
                { "sourceFile": "policy.pdf", "knowledgeBase": "system", "chunkIndex": 0 }
              ]
            }
            """;

        var details = StudioRagPlanMapper.BuildPlanSourceDetails(sourcesUsed, json);

        Assert.Equal("JD", details[0].Scope);
        Assert.Equal("HR", details[1].Scope);
        Assert.Null(details[2].Scope);
        Assert.Equal("SYSTEM", details[3].Scope);
    }

    [Fact]
    public void ExtractCitations_ParsesKnowledgeBaseFromTagsJsonShape()
    {
        var raw = new List<object>
        {
            new Dictionary<string, object?>
            {
                ["sourceFile"] = "internal.pdf",
                ["knowledgeBase"] = "system",
                ["chunkIndex"] = 1,
                ["excerpt"] = "sample"
            }
        };

        var citations = StudioRagQuestionMapper.ExtractCitations(raw);

        Assert.Single(citations);
        Assert.Equal("system", citations[0].KnowledgeBase);
        Assert.Equal("internal.pdf", citations[0].SourceFile);
    }
}
