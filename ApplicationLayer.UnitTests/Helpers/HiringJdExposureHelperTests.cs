using ApplicationLayer.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Helpers;

/// <summary>SCRUM-465: candidate chỉ thấy PublicJobDescription; blob null → không file URL; hiring thiếu public JD → gate.</summary>
public class HiringJdExposureHelperTests
{
    [Theory]
    [InlineData(false, "Full extract JD", null)]
    [InlineData(false, null, null)]
    [InlineData(true, null, null)]
    [InlineData(true, "   ", null)]
    [InlineData(true, "  Short public JD  ", "Short public JD")]
    public void ResolveCandidateJobDescription_OnlyPublicWhenHiring(
        bool isHiring,
        string? publicJd,
        string? expected)
    {
        Assert.Equal(
            expected,
            HiringJdExposureHelper.ResolveCandidateJobDescription(isHiring, publicJd));
    }

    [Theory]
    [InlineData(false, "job-descriptions/a.png", false)]
    [InlineData(true, null, false)]
    [InlineData(true, "  ", false)]
    [InlineData(true, "job-descriptions/a.png", true)]
    public void ShouldExposeJdFileUrl_RequiresHiringAndBlob(
        bool isHiring,
        string? blobPath,
        bool expected)
    {
        Assert.Equal(
            expected,
            HiringJdExposureHelper.ShouldExposeJdFileUrl(isHiring, blobPath));
    }

    [Theory]
    [InlineData(false, null, false)]
    [InlineData(false, "", false)]
    [InlineData(true, null, true)]
    [InlineData(true, "   ", true)]
    [InlineData(true, "Ok short JD", false)]
    public void RequiresPublicJobDescription_BlocksEmptyHiring(
        bool isHiring,
        string? publicJd,
        bool expected)
    {
        Assert.Equal(
            expected,
            HiringJdExposureHelper.RequiresPublicJobDescription(isHiring, publicJd));
    }

    [Fact]
    public void BuildPublicJobDescriptionPreview_NullWhenNotHiringOrEmpty()
    {
        Assert.Null(HiringJdExposureHelper.BuildPublicJobDescriptionPreview(false, "long text"));
        Assert.Null(HiringJdExposureHelper.BuildPublicJobDescriptionPreview(true, null));
        Assert.Null(HiringJdExposureHelper.BuildPublicJobDescriptionPreview(true, "  "));
    }

    [Fact]
    public void BuildPublicJobDescriptionPreview_TruncatesAtMaxChars()
    {
        var longText = new string('a', 250);
        var preview = HiringJdExposureHelper.BuildPublicJobDescriptionPreview(true, longText, 200);
        Assert.NotNull(preview);
        Assert.EndsWith("…", preview);
        Assert.Equal(201, preview!.Length); // 200 chars + ellipsis
    }
}
