using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Services.Coach;
using Xunit;

namespace ApplicationLayer.UnitTests.Candidate;

/// <summary>
/// SCRUM-503: chốt lại luật so khớp skill. Trước đây dùng substring nên câu hỏi
/// JavaScript lọt vào slot Java rồi được dán nhãn Java → đề Re-assessment lạc đề.
/// </summary>
public sealed class TechSkillMatcherTests
{
    [Theory]
    // Cặp bẫy: tên lồng nhau nhưng là công nghệ khác
    [InlineData("JavaScript", "Java")]
    [InlineData("Java", "JavaScript")]
    [InlineData("TypeScript", "Java")]
    [InlineData("Django", "Go")]
    [InlineData("MongoDB", "Go")]
    [InlineData("C++", "C#")]
    [InlineData("C#", "C")]
    [InlineData("MySQL", "SQL")]
    [InlineData("NoSQL", "SQL")]
    [InlineData("React Native", "React")]
    public void Compare_RejectsConfusableSkills(string candidate, string allowed)
        => Assert.Equal(TechSkillMatcher.MatchKind.None, TechSkillMatcher.Compare(candidate, allowed));

    [Theory]
    // Đồng nghĩa / biến thể viết khác phải vẫn khớp
    [InlineData("ef-core", "EF Core")]
    [InlineData("golang", "Go")]
    [InlineData("csharp", "C#")]
    [InlineData("reactjs", "React")]
    [InlineData("dotnet", ".NET")]
    // Tên dài hơn nhưng chứa đúng dãy token của skill
    [InlineData("ASP.NET Core Web API", "ASP.NET Core")]
    [InlineData("Java 17", "Java")]
    [InlineData("React Hooks", "React")]
    public void Compare_AcceptsAliasAndTokenSubset(string candidate, string allowed)
        => Assert.NotEqual(TechSkillMatcher.MatchKind.None, TechSkillMatcher.Compare(candidate, allowed));

    [Fact]
    public void MapToAllowed_PicksExactOverSubset()
    {
        var mapped = TechSkillMatcher.MapToAllowed("ASP.NET Core", ["ASP.NET", "ASP.NET Core"]);
        Assert.Equal("ASP.NET Core", mapped);
    }

    [Fact]
    public void MapToAllowed_ReturnsNull_WhenOnlyConfusableAvailable()
        => Assert.Null(TechSkillMatcher.MapToAllowed("JavaScript", ["Java"]));

    [Fact]
    public void MentionsSkill_IgnoresPartialToken()
    {
        Assert.True(TechSkillMatcher.MentionsSkill("Giải thích garbage collector trong Java.", "Java"));
        Assert.False(TechSkillMatcher.MentionsSkill("Giải thích closure trong JavaScript.", "Java"));
    }

    [Fact]
    public void FindConflictingSkill_DetectsLanguageDrift()
    {
        var conflict = TechSkillMatcher.FindConflictingSkill(
            "Trong JavaScript, hoisting hoạt động thế nào?", "Java");
        Assert.Equal("javascript", conflict);
    }

    [Fact]
    public void Relevance_FailsJob_WhenQuestionDriftsToAnotherLanguage()
    {
        var result = QuestionRelevanceValidator.Check(
        [
            new RagGeneratedQuestionDto
            {
                Order = 1,
                Skill = "Java",
                FocusArea = "Collections",
                Question = "Event loop trong JavaScript xử lý microtask thế nào?"
            }
        ]);
        Assert.False(result.Ok);
        Assert.Contains("JavaScript", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Relevance_AcceptsQuestionAboutEcosystemOfSameSkill()
    {
        // Câu Java hỏi qua Spring Boot không nhắc chữ "Java" vẫn hợp lệ — tránh false positive.
        var result = QuestionRelevanceValidator.Check(
        [
            new RagGeneratedQuestionDto
            {
                Order = 1,
                Skill = "Java",
                FocusArea = "Dependency Injection",
                Question = "Spring Boot quản lý bean scope singleton và prototype khác nhau thế nào?"
            }
        ]);
        Assert.True(result.Ok);
    }
}
