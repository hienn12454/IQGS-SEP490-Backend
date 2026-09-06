using DomainLayer.Constants;
using DomainLayer.Exceptions;
using Xunit;

namespace ApplicationLayer.UnitTests.Knowledge;

public sealed class KnowledgeDocumentTypeTests
{
    [Theory]
    [InlineData("Policy", "Policy")]
    [InlineData("policy", "Policy")]
    [InlineData("InternalStack", "InternalStack")]
    [InlineData("Rubric", "Rubric")]
    [InlineData("RolePack", "RolePack")]
    public void NormalizeForStorage_Hr_AcceptsKnownTypes(string input, string expected)
    {
        var result = KnowledgeDocumentType.NormalizeForStorage(input, requireHrType: true);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeForStorage_Hr_RejectsMissing()
    {
        Assert.Throws<BadRequestException>(() =>
            KnowledgeDocumentType.NormalizeForStorage(null, requireHrType: true));
    }

    [Fact]
    public void NormalizeForStorage_Hr_RejectsUnclassified()
    {
        Assert.Throws<BadRequestException>(() =>
            KnowledgeDocumentType.NormalizeForStorage("Unclassified", requireHrType: true));
    }

    [Fact]
    public void NormalizeForStorage_Admin_DefaultsUnclassified()
    {
        var result = KnowledgeDocumentType.NormalizeForStorage(null, requireHrType: false);
        Assert.Equal(KnowledgeDocumentType.Unclassified, result);
    }

    [Theory]
    [InlineData(null, "Unclassified")]
    [InlineData("", "Unclassified")]
    [InlineData("Policy", "Policy")]
    [InlineData("unknown-x", "Unclassified")]
    public void FromSection_MapsExpected(string? section, string expected)
    {
        Assert.Equal(expected, KnowledgeDocumentType.FromSection(section));
    }
}
