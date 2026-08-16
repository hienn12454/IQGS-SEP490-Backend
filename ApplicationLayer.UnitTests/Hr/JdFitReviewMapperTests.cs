using ApplicationLayer.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Hr;

public sealed class JdFitReviewMapperTests
{
    [Theory]
    [InlineData("excellent", "excellent")]
    [InlineData("Tuyệt vời", "excellent")]
    [InlineData("tốt", "good")]
    [InlineData("Tương đối", "fair")]
    [InlineData("Chưa phù hợp", "unfit")]
    [InlineData("nope", null)]
    public void NormalizeVerdict_MapsLabels(string raw, string? expected)
    {
        Assert.Equal(expected, JdFitReviewMapper.NormalizeVerdict(raw));
    }

    [Fact]
    public void StripNumeric_RemovesScoresAndPercents()
    {
        var cleaned = JdFitReviewMapper.StripNumeric("Bộ tốt 85/100 và cover 72%.");
        Assert.NotNull(cleaned);
        Assert.DoesNotContain("85", cleaned);
        Assert.DoesNotContain("72", cleaned);
        Assert.DoesNotContain("%", cleaned);
    }

    [Fact]
    public void Map_DropsUnknownFlagsAndTypes()
    {
        var mapped = JdFitReviewMapper.Map(new ApplicationLayer.DTOs.Rag.EvaluateQuestionSetResult
        {
            Success = true,
            Verdict = "good",
            SummaryVi = "Bộ tốt so với JD.",
            SummaryEn = "The set is a good fit.",
            QuestionFlags =
            [
                new()
                {
                    QuestionId = "a",
                    Flag = "onJd",
                    NoteVi = "Ok",
                    Sources = [new() { ChunkIndex = 0, Excerpt = "Need ASP.NET Core." }]
                },
                new() { QuestionId = "b", Flag = "unknown", NoteVi = "x" }
            ],
            SuggestedActions =
            [
                new() { Type = "add", ReasonVi = "Thêm K8s" },
                new() { Type = "explode", ReasonVi = "nope" }
            ],
            JdSources = [new() { ChunkIndex = 0, Excerpt = "Need ASP.NET Core." }]
        });

        Assert.Equal("good", mapped.Verdict);
        Assert.Single(mapped.QuestionFlags);
        Assert.Equal("onJd", mapped.QuestionFlags[0].Flag);
        Assert.Single(mapped.SuggestedActions);
        Assert.Equal("add", mapped.SuggestedActions[0].Type);
        Assert.Single(mapped.JdSources);
        Assert.Equal(0, mapped.JdSources[0].ChunkIndex);
        Assert.Single(mapped.QuestionFlags[0].Sources);
    }
}

public sealed class JdFitContentHashTests
{
    [Fact]
    public void Compute_ChangesWhenJdOrQuestionTextChanges()
    {
        var q = new DomainLayer.Entities.QuestionSetQuestion
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Order = 1,
            Question = "What is REST?",
            QuestionType = "open",
            Skill = "API",
            IsActive = true
        };
        var a = JdFitContentHash.Compute("JD A", [q]);
        var b = JdFitContentHash.Compute("JD B", [q]);
        q.Question = "What is GraphQL?";
        var c = JdFitContentHash.Compute("JD A", [q]);

        Assert.Equal(64, a.Length);
        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
    }
}
