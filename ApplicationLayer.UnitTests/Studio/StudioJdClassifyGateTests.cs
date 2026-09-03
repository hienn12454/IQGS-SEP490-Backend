using ApplicationLayer.Studio.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

/// <summary>SCRUM-432: gate classify documentType + isItRole trước khi lưu JD.</summary>
public sealed class StudioJdClassifyGateTests
{
    [Fact]
    public void TryReject_JobDescriptionItRole_Passes()
    {
        var err = StudioJdClassifyGate.TryReject("job_description", true, null);
        Assert.Null(err);
    }

    [Theory]
    [InlineData("resume")]
    [InlineData("article")]
    [InlineData("documentation")]
    [InlineData("other")]
    [InlineData("tutorial")]
    public void TryReject_NonJobDocument_ReturnsNotJobPosting(string documentType)
    {
        var err = StudioJdClassifyGate.TryReject(documentType, true, "Đây là tutorial.");
        Assert.NotNull(err);
        Assert.Equal(StudioJdClassifyGate.ErrorNotJobPosting, err!.ErrorCode);
        Assert.Equal(422, err.StatusCode);
        Assert.Contains("tutorial", err.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryReject_JobButNotIt_ReturnsNotItRole()
    {
        var err = StudioJdClassifyGate.TryReject(
            "job_description",
            false,
            "Vị trí Digital Marketing không thuộc IT.");
        Assert.NotNull(err);
        Assert.Equal(StudioJdClassifyGate.ErrorNotItRole, err!.ErrorCode);
        Assert.Equal(422, err.StatusCode);
        Assert.Contains("Marketing", err.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryReject_MissingType_UsesDefaultNotJobMessage()
    {
        var err = StudioJdClassifyGate.TryReject(null, true, null);
        Assert.NotNull(err);
        Assert.Equal(StudioJdClassifyGate.ErrorNotJobPosting, err!.ErrorCode);
        Assert.Equal(StudioJdClassifyGate.DefaultNotJobPosting, err.Message);
    }

    [Fact]
    public void FromRagFailure_ClassifyStage_Maps422()
    {
        var ex = StudioJdClassifyGate.FromRagFailure(
            StudioJdClassifyGate.StageClassify,
            422,
            "Đây không phải tin tuyển dụng.",
            ["Đây không phải tin tuyển dụng."],
            documentType: "article",
            isItRole: true);
        Assert.Equal(422, ex.StatusCode);
        Assert.Equal(StudioJdClassifyGate.ErrorNotJobPosting, ex.ErrorCode);
    }

    [Fact]
    public void FromRagFailure_Infra_Maps502()
    {
        var ex = StudioJdClassifyGate.FromRagFailure(
            "JD_ANALYZE",
            502,
            "LLM timeout",
            null);
        Assert.Equal(502, ex.StatusCode);
        Assert.Equal(StudioJdClassifyGate.ErrorClassifyFailed, ex.ErrorCode);
    }

    [Theory]
    [InlineData("JD", "job_description")]
    [InlineData("job-posting", "job_description")]
    [InlineData("CV", "resume")]
    public void NormalizeDocumentType_Aliases(string raw, string expected)
    {
        Assert.Equal(expected, StudioJdClassifyGate.NormalizeDocumentType(raw));
    }
}
