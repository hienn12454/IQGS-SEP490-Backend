using System.Text.Json.Nodes;
using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Helpers;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

/// <summary>SCRUM-426: PatchOutline persist + ExtractOutline đọc citations.</summary>
public sealed class StudioPlanOutlineCitationsTests
{
    private static InterviewPlan MakeSource() => new()
    {
        Title = "Backend interview",
        SeniorityLevel = "Mid",
        TotalQuestions = 2,
        InterviewLengthMinutes = 45,
        SourcePlanJson = """{"roleTitle":"Backend","summary":"Plan","totalQuestions":2,"difficulty":"medium"}"""
    };

    [Fact]
    public void Apply_OutlineItems_PersistsCitations()
    {
        var citations = new List<StudioQuestionCitationDto>
        {
            new(
                "job-description",
                ChunkIndex: 2,
                Excerpt: "Nắm vững OOP và SOLID.",
                KnowledgeBase: "hr",
                Origin: "HR",
                UsedFor: ["why-asked"])
        };

        var outline = new List<PlanOutlineItemDto>
        {
            new(1, "technical", "medium", "SOLID", "SRP", "Đánh giá SOLID", "Code", citations),
            new(2, "behavioral", "easy", "English", "Communication", "Đánh giá tiếng Anh", "Text", null)
        };

        var mapped = StudioPlanSettingsPatcher.Apply(
            MakeSource(),
            sourceSections: [],
            sourceFocus: [],
            targetTotal: 2,
            targetDifficulty: QuestionDifficulty.Medium,
            targetMinutes: 45,
            questionTypes: ["technical", "behavioral"],
            outlineItems: outline);

        var extracted = StudioRagPlanMapper.ExtractOutlineItems(mapped.SourcePlanJson);
        Assert.Equal(2, extracted.Count);

        Assert.NotNull(extracted[0].Citations);
        Assert.Single(extracted[0].Citations!);
        Assert.Equal("job-description", extracted[0].Citations![0].SourceFile);
        Assert.Equal(2, extracted[0].Citations![0].ChunkIndex);
        Assert.Equal("Nắm vững OOP và SOLID.", extracted[0].Citations![0].Excerpt);
        Assert.Equal("HR", extracted[0].Citations![0].Origin);
        Assert.Contains("why-asked", extracted[0].Citations![0].UsedFor!);

        Assert.True(extracted[1].Citations is null || extracted[1].Citations.Count == 0);
    }

    [Fact]
    public void ExtractOutlineItems_ReadsSnakeAndCamelCitations()
    {
        var json = """
        {
          "recommendedQuestionOutline": [
            {
              "order": 1,
              "type": "technical",
              "difficulty": "easy",
              "skill": "OOP",
              "focusArea": "Encapsulation",
              "goal": "x",
              "answerMethod": "Text",
              "citations": [
                {
                  "source_file": "job-description",
                  "chunk_index": 1,
                  "excerpt": "Tech stack .NET",
                  "knowledge_base": "hr",
                  "origin": "HR",
                  "used_for": ["why-asked"]
                }
              ]
            }
          ]
        }
        """;

        var items = StudioRagPlanMapper.ExtractOutlineItems(json);
        Assert.Single(items);
        Assert.NotNull(items[0].Citations);
        Assert.Equal(1, items[0].Citations![0].ChunkIndex);
        Assert.Equal("Tech stack .NET", items[0].Citations![0].Excerpt);
    }
}
