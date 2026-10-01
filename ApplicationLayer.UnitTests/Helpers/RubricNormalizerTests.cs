using ApplicationLayer.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Helpers;

public class RubricNormalizerTests
{
    [Fact]
    public void NormalizeFromLegacyStrings_DistributesWeightsTo100()
    {
        var doc = RubricNormalizer.NormalizeFromLegacyStrings(["A", "B", "C"]);
        Assert.Equal(3, doc.Criteria.Count);
        Assert.Equal(100, doc.Criteria.Sum(c => c.Weight));
        Assert.All(doc.Criteria, c => Assert.True(c.Anchors.Count >= 2));
    }

    [Fact]
    public void IsPublishReady_RequiresWeight100AndAnchors()
    {
        var doc = RubricNormalizer.NormalizeFromLegacyStrings(["Accuracy", "Depth"]);
        Assert.True(RubricNormalizer.IsPublishReady(doc));
    }

    [Fact]
    public void FlattenForEvaluate_IncludesAnchors()
    {
        var doc = RubricNormalizer.NormalizeFromLegacyStrings(["DI lifecycle"]);
        var flat = RubricNormalizer.FlattenForEvaluate(doc);
        Assert.Single(flat);
        Assert.Contains("Mốc:", flat[0]);
    }

    [Fact]
    public void NormalizeFromLegacyStrings_KeepsExplicitWeightsThatDoNotSumTo100()
    {
        var doc = RubricNormalizer.NormalizeFromLegacyStrings([
            "[50%] Technical correctness",
            "[40%] Practical evidence"
        ]);

        Assert.Equal(2, doc.Criteria.Count);
        Assert.Equal(50, doc.Criteria[0].Weight);
        Assert.Equal(40, doc.Criteria[1].Weight);
        Assert.Equal("Technical correctness", doc.Criteria[0].Label);
        Assert.True(RubricNormalizer.IsWeightInvalid(doc));
        Assert.False(RubricNormalizer.IsPublishReady(doc));
    }

    [Fact]
    public void NormalizeFromLegacyStrings_RejectsNegativeAndUnreadableWeights()
    {
        var negative = RubricNormalizer.NormalizeFromLegacyStrings(["[-10%] Accuracy", "[110%] Depth"]);
        Assert.True(RubricNormalizer.IsWeightInvalid(negative));

        var unreadable = RubricNormalizer.NormalizeFromLegacyStrings(["[abc%] Accuracy"]);
        Assert.True(RubricNormalizer.IsWeightInvalid(unreadable));
        Assert.Contains("[abc%]", unreadable.Criteria[0].Label);
    }

    [Fact]
    public void NormalizeFromJson_DoesNotRewriteExplicitInvalidWeights()
    {
        const string json = """
            {
              "version": 1,
              "criteria": [
                { "id": "a", "label": "A", "weight": 70, "anchors": { "50": "ok", "100": "good" } },
                { "id": "b", "label": "B", "weight": 40, "anchors": { "50": "ok", "100": "good" } }
              ]
            }
            """;
        var doc = RubricNormalizer.NormalizeFromJson(json);
        Assert.Equal(110, doc.Criteria.Sum(c => c.Weight));
        Assert.False(RubricNormalizer.IsPublishReady(doc));
    }

    [Fact]
    public void NormalizeFromJson_ParsesRubricV1Document()
    {
        const string json = """
            {
              "version": 1,
              "scale": "0-100",
              "criteria": [
                {
                  "id": "accuracy",
                  "label": "Hiểu khái niệm",
                  "weight": 60,
                  "anchors": { "50": "Cơ bản", "100": "Xuất sắc" }
                },
                {
                  "id": "depth",
                  "label": "Độ sâu",
                  "weight": 40,
                  "anchors": { "50": "OK", "100": "Tốt" }
                }
              ]
            }
            """;
        var doc = RubricNormalizer.NormalizeFromJson(json);
        Assert.Equal(2, doc.Criteria.Count);
        Assert.Equal(100, doc.Criteria.Sum(c => c.Weight));
        Assert.True(RubricNormalizer.IsPublishReady(doc));
    }
}
