using ApplicationLayer.Settings;
using Xunit;

namespace ApplicationLayer.UnitTests.Knowledge;

/// <summary>SCRUM-448: Admin SYSTEM cho phép .jsonl; HR thì không.</summary>
public sealed class KnowledgeBaseExtensionAllowlistTests
{
    [Fact]
    public void System_IncludesJsonl()
    {
        var settings = new KnowledgeBaseSettings();
        var allowed = settings.GetAllowedExtensionsForScope("SYSTEM");
        Assert.Contains(".jsonl", allowed);
        Assert.Contains(".pdf", allowed);
    }

    [Fact]
    public void Hr_ExcludesJsonl()
    {
        var settings = new KnowledgeBaseSettings();
        var allowed = settings.GetAllowedExtensionsForScope("HR");
        Assert.DoesNotContain(".jsonl", allowed);
        Assert.Contains(".pdf", allowed);
        Assert.Contains(".docx", allowed);
        Assert.Contains(".txt", allowed);
    }

    [Fact]
    public void Scope_IsCaseInsensitive()
    {
        var settings = new KnowledgeBaseSettings();
        Assert.Contains(".jsonl", settings.GetAllowedExtensionsForScope("system"));
        Assert.DoesNotContain(".jsonl", settings.GetAllowedExtensionsForScope("hr"));
    }
}
