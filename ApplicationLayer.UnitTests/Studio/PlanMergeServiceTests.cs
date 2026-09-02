using ApplicationLayer.Studio.Helpers;
using DomainLayer.Studio.Enums;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

/// <summary>SCRUM-420: merge baseline + Git-only patch giữ phần còn lại.</summary>
public class PlanMergeServiceTests
{
    private const string BaselineJson = """
        {
          "roleTitle": "Backend Developer",
          "summary": "Plan .NET và SQL",
          "difficulty": "medium",
          "level": "medium",
          "experienceLevel": "mid",
          "totalQuestions": 10,
          "skills": ["C#", "SQL", "REST"],
          "questionTypeDistribution": [
            { "type": "technical", "count": 6, "reason": "Core stack" },
            { "type": "behavioral", "count": 4, "reason": "Soft skills" }
          ],
          "difficultyDistribution": [
            { "difficulty": "medium", "count": 10 }
          ],
          "coverage": [
            { "skill": "C#", "questionCount": 4, "focusAreas": ["OOP"], "sourceFiles": ["job-description"] },
            { "skill": "SQL", "questionCount": 3, "focusAreas": ["Queries"], "sourceFiles": ["job-description"] },
            { "skill": "REST", "questionCount": 3, "focusAreas": ["API"], "sourceFiles": ["job-description"] }
          ],
          "recommendedQuestionOutline": [],
          "citations": [
            { "knowledgeBase": "hr", "sourceFile": "job-description", "chunkIndex": 0, "excerpt": "Backend developer" }
          ]
        }
        """;

    [Fact]
    public void Merge_GitOnlyPatch_ReplacesCoverage_KeepsOtherFields()
    {
        var patch = """
            {
              "replaceCoverage": [
                {
                  "skill": "Git",
                  "questionCount": 10,
                  "focusAreas": ["Syntax", "Branching"],
                  "sourceFiles": ["job-description", "git-internals.pdf"]
                }
              ],
              "replaceSkills": ["Git"],
              "instructionApplied": "Chỉ git syntax"
            }
            """;

        using var patchDoc = System.Text.Json.JsonDocument.Parse(patch);

        var mapped = PlanMergeService.Merge(BaselineJson, patchDoc.RootElement, 10, 45);

        Assert.Equal(10, mapped.TotalQuestions);
        Assert.Contains("Git", mapped.SourcePlanJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"skill\":\"SQL\"", mapped.SourcePlanJson.Replace(" ", ""), StringComparison.Ordinal);
        Assert.Contains(".NET", mapped.SourcePlanJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(QuestionDifficulty.Medium, mapped.Difficulty);
    }

    [Fact]
    public void Merge_UpdateSummaryOnly_PreservesCoverage()
    {
        var patch = """
            { "updateSummary": "Updated summary after refine" }
            """;
        using var patchDoc = System.Text.Json.JsonDocument.Parse(patch);

        var mapped = PlanMergeService.Merge(BaselineJson, patchDoc.RootElement, 10, 45);

        Assert.Contains("Updated summary after refine", mapped.SourcePlanJson, StringComparison.Ordinal);
        Assert.Contains("\"skill\":\"C#\"", mapped.SourcePlanJson.Replace(" ", ""), StringComparison.Ordinal);
    }
}
