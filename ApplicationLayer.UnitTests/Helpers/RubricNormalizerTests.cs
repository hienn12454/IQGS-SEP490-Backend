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
