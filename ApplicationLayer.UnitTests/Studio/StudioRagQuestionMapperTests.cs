using System.Text.Json;
using ApplicationLayer.Studio.Helpers;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

/// <summary>SCRUM-421: Parse origin/provenance từ TagsJson câu hỏi.</summary>
public class StudioRagQuestionMapperTests
{
    [Fact]
    public void ExtractCitations_ParsesOriginUsedForReason()
    {
        var tagsJson = """
            {
              "citations": [
                {
                  "sourceFile": "job-description",
                  "chunkIndex": 0,
                  "excerpt": "Yeu cau Git",
                  "knowledgeBase": "hr",
                  "origin": "HR",
                  "usedFor": ["why-asked"]
                },
                {
                  "sourceFile": "git.pdf",
                  "chunkIndex": 1,
                  "excerpt": "Branching workflow",
                  "knowledgeBase": "system",
                  "origin": "SYSTEM",
                  "usedFor": ["technical-body"]
                }
              ],
              "sourceProvenance": {
                "primaryOrigin": "HR",
                "items": [
                  { "origin": "HR", "sourceFile": "job-description", "usedFor": ["why-asked"] },
                  { "origin": "SYSTEM", "sourceFile": "git.pdf", "usedFor": ["technical-body"] }
                ]
              },
              "missingAdminWarning": false
            }
            """;

        var meta = StudioRagQuestionMapper.ParseMeta(tagsJson);
        var citations = StudioRagQuestionMapper.ExtractCitations(meta.Citations);

        Assert.Equal(2, citations.Count);
        Assert.Equal("HR", citations[0].Origin);
        Assert.Contains("why-asked", citations[0].UsedFor!);
        Assert.Equal("SYSTEM", citations[1].Origin);
        Assert.Equal("system", citations[1].KnowledgeBase);
        Assert.False(meta.MissingAdminWarning);
        Assert.NotNull(meta.SourceProvenance);
    }

    [Fact]
    public void ExtractCitations_InfersOriginFromKnowledgeBaseWhenLegacy()
    {
        var tagsJson = """
            {
              "citations": [
                {
                  "sourceFile": "policy.pdf",
                  "chunkIndex": 0,
                  "knowledgeBase": "system"
                }
              ]
            }
            """;

        var citations = StudioRagQuestionMapper.ExtractCitationsFromTagsJson(tagsJson);
        Assert.Single(citations);
        Assert.Equal("SYSTEM", citations[0].Origin);
    }

    [Fact]
    public void MapToStudioQuestionDto_ExposesRationaleFromTagsJson()
    {
        var q = new InterviewQuestion
        {
            Content = "Giải thích SRP?",
            Difficulty = QuestionDifficulty.Medium,
            Type = QuestionType.Technical,
            OrderIndex = 1,
            TagsJson = """{"rationale":"Đánh giá SOLID","skill":"SOLID","focusArea":"SRP"}"""
        };

        var dto = StudioRagQuestionMapper.MapToStudioQuestionDto(q);
        Assert.Equal("Đánh giá SOLID", dto.Rationale);
        Assert.Equal("SOLID", dto.Skill);
        Assert.Equal("SRP", dto.FocusArea);
    }

    [Fact]
    public void MapToStudioQuestionDto_SkillNullWhenMissing()
    {
        var q = new InterviewQuestion
        {
            Content = "Q?",
            Difficulty = QuestionDifficulty.Easy,
            Type = QuestionType.Technical,
            OrderIndex = 1,
            TagsJson = """{"rationale":"only"}"""
        };
        var dto = StudioRagQuestionMapper.MapToStudioQuestionDto(q);
        Assert.Null(dto.Skill);
        Assert.Null(dto.FocusArea);
    }
}
