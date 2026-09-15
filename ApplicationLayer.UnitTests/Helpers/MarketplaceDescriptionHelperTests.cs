using ApplicationLayer.Helpers;
using Xunit;

namespace ApplicationLayer.UnitTests.Helpers;

public class MarketplaceDescriptionHelperTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Bộ câu hỏi fullstack", false)]
    [InlineData("STUDIO_SAVE; project=abc", true)]
    [InlineData("studio_save; project=abc", true)]
    [InlineData("  STUDIO_SAVE; project=abc", true)]
    [InlineData("STUDIO_MIRROR; project=xyz", true)]
    [InlineData("Studio_Mirror foo", true)]
    public void IsInternalMarker_DetectsPrefixes(string? note, bool expected)
        => Assert.Equal(expected, MarketplaceDescriptionHelper.IsInternalMarker(note));

    [Fact]
    public void ToPublic_ReturnsNull_ForMarkerOrEmpty()
    {
        Assert.Null(MarketplaceDescriptionHelper.ToPublic(null));
        Assert.Null(MarketplaceDescriptionHelper.ToPublic("  "));
        Assert.Null(MarketplaceDescriptionHelper.ToPublic("STUDIO_SAVE; project=05c57e78-9c20-44b5-9eea-360c830807eb"));
        Assert.Null(MarketplaceDescriptionHelper.ToPublic("STUDIO_MIRROR; sync"));
    }

    [Fact]
    public void ToPublic_TrimsAndKeepsHumanText()
    {
        Assert.Equal(
            "Bộ câu hỏi Fullstack API Docker",
            MarketplaceDescriptionHelper.ToPublic("  Bộ câu hỏi Fullstack API Docker  "));
    }

    [Fact]
    public void ToPublic_TruncatesAtMaxLength()
    {
        var longText = new string('a', MarketplaceDescriptionHelper.MaxLength + 50);
        var result = MarketplaceDescriptionHelper.ToPublic(longText);
        Assert.NotNull(result);
        Assert.Equal(MarketplaceDescriptionHelper.MaxLength, result!.Length);
    }

    [Fact]
    public void ResolveForStudioSave_PrefersProjectDescription()
    {
        var result = MarketplaceDescriptionHelper.ResolveForStudioSave(
            "Mô tả project Studio",
            "STUDIO_SAVE; project=old");
        Assert.Equal("Mô tả project Studio", result);
    }

    [Fact]
    public void ResolveForStudioSave_KeepsExistingHumanNote_WhenProjectEmpty()
    {
        var result = MarketplaceDescriptionHelper.ResolveForStudioSave(
            null,
            "Mô tả cũ từ Question Builder");
        Assert.Equal("Mô tả cũ từ Question Builder", result);
    }

    [Fact]
    public void ResolveForStudioSave_ReturnsNull_WhenBothMarkerOrEmpty()
    {
        Assert.Null(MarketplaceDescriptionHelper.ResolveForStudioSave(
            null,
            "STUDIO_SAVE; project=abc"));
        Assert.Null(MarketplaceDescriptionHelper.ResolveForStudioSave("  ", null));
    }
}
