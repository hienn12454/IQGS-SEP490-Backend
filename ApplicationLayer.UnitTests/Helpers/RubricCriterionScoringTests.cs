using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Helpers;

public class RubricCriterionScoringTests
{
    private static RubricNormalizer.RubricDocumentV1 BuildRubric(params (string Label, int Weight)[] items)
    {
        var doc = new RubricNormalizer.RubricDocumentV1();
        foreach (var (label, weight) in items)
        {
            doc.Criteria.Add(new RubricNormalizer.RubricCriterionV1
            {
                Id = label.ToLowerInvariant(),
                Label = label,
                Weight = weight,
                Anchors = new Dictionary<string, string> { ["50"] = "ok", ["100"] = "great" }
            });
        }
        return doc;
    }

    [Fact]
    public void BuildRagCriteria_AssignsCodesInOrder()
    {
        var criteria = RubricCriterionScoring.BuildRagCriteria(BuildRubric(("Accuracy", 60), ("Examples", 40)));

        Assert.Equal(["C1", "C2"], criteria.Select(c => c.Code).ToArray());
        Assert.Equal(60, criteria[0].Weight);
    }

    [Fact]
    public void BuildRagCriteria_ReturnsEmpty_WhenWeightsDoNotSumTo100()
    {
        // Rubric sai trọng số thì không dùng: quay về chấm tổng thể, không tự chia lại.
        var criteria = RubricCriterionScoring.BuildRagCriteria(BuildRubric(("A", 50), ("B", 30)));
        Assert.Empty(criteria);
    }

    [Fact]
    public void BuildRagCriteria_ReturnsEmpty_WhenNoRubric()
    {
        Assert.Empty(RubricCriterionScoring.BuildRagCriteria(null));
        Assert.Empty(RubricCriterionScoring.BuildRagCriteria(new RubricNormalizer.RubricDocumentV1()));
    }

    [Fact]
    public void ComputeWeightedScore_MultipliesByWeight()
    {
        var criteria = RubricCriterionScoring.BuildRagCriteria(BuildRubric(("Accuracy", 60), ("Examples", 40)));
        var stored = RubricCriterionScoring.BuildStoredScores(
            criteria,
            new Dictionary<string, double> { ["C1"] = 80, ["C2"] = 50 });

        // 80 * 0.6 + 50 * 0.4 = 68
        Assert.Equal(68, RubricCriterionScoring.ComputeWeightedScore(stored), 5);
    }

    [Fact]
    public void ComputeWeightedScore_HeavyCriterionDominates()
    {
        var criteria = RubricCriterionScoring.BuildRagCriteria(BuildRubric(("Core", 90), ("Style", 10)));
        var stored = RubricCriterionScoring.BuildStoredScores(
            criteria,
            new Dictionary<string, double> { ["C1"] = 0, ["C2"] = 100 });

        Assert.Equal(10, RubricCriterionScoring.ComputeWeightedScore(stored), 5);
    }

    [Fact]
    public void HasAllScores_RejectsMissingCodeOrOutOfRange()
    {
        var criteria = RubricCriterionScoring.BuildRagCriteria(BuildRubric(("A", 50), ("B", 50)));

        var missing = new EvaluateAnswerResult
        {
            Success = true,
            CriterionScores = new Dictionary<string, double> { ["C1"] = 70 }
        };
        var outOfRange = new EvaluateAnswerResult
        {
            Success = true,
            CriterionScores = new Dictionary<string, double> { ["C1"] = 70, ["C2"] = 140 }
        };
        var ok = new EvaluateAnswerResult
        {
            Success = true,
            CriterionScores = new Dictionary<string, double> { ["C1"] = 70, ["C2"] = 40 }
        };

        Assert.False(RubricCriterionScoring.HasAllScores(missing, criteria));
        Assert.False(RubricCriterionScoring.HasAllScores(outOfRange, criteria));
        Assert.False(RubricCriterionScoring.HasAllScores(null, criteria));
        Assert.True(RubricCriterionScoring.HasAllScores(ok, criteria));
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsLabelWeightScore()
    {
        var criteria = RubricCriterionScoring.BuildRagCriteria(BuildRubric(("Accuracy", 60), ("Examples", 40)));
        var stored = RubricCriterionScoring.BuildStoredScores(
            criteria,
            new Dictionary<string, double> { ["C1"] = 80.4, ["C2"] = 49.6 });

        var restored = RubricCriterionScoring.Deserialize(RubricCriterionScoring.Serialize(stored));

        Assert.NotNull(restored);
        Assert.Equal("Accuracy", restored![0].Label);
        Assert.Equal(60, restored[0].Weight);
        // Điểm từng tiêu chí được làm tròn khi lưu.
        Assert.Equal(80, restored[0].Score);
        Assert.Equal(50, restored[1].Score);
    }

    [Fact]
    public void Deserialize_ReturnsNull_ForEmptyOrBrokenJson()
    {
        Assert.Null(RubricCriterionScoring.Deserialize(null));
        Assert.Null(RubricCriterionScoring.Deserialize("[]"));
        Assert.Null(RubricCriterionScoring.Deserialize("not json"));
    }
}
