using ApplicationLayer.Helpers;
using DomainLayer.Exceptions;
using Xunit;

namespace ApplicationLayer.UnitTests.Knowledge;

/// <summary>SCRUM-450: sanitize folder UI grouping.</summary>
public sealed class KnowledgeFolderHelperTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("unsorted", null)]
    [InlineData("SWE", "swe")]
    [InlineData("dotnet junior", "dotnet-junior")]
    [InlineData("flask_qa", "flask_qa")]
    public void Normalize_MapsExpected(string? input, string? expected)
    {
        Assert.Equal(expected, KnowledgeFolderHelper.Normalize(input));
    }

    [Fact]
    public void Normalize_SymbolsOnly_BecomesUnsorted()
    {
        Assert.Null(KnowledgeFolderHelper.Normalize("!!!"));
    }

    [Fact]
    public void DisplayName_UnsortedWhenNull()
    {
        Assert.Equal("unsorted", KnowledgeFolderHelper.DisplayName(null));
        Assert.Equal("swe", KnowledgeFolderHelper.DisplayName("swe"));
    }
}
