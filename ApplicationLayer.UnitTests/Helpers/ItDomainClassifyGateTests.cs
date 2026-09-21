using ApplicationLayer.Helpers;
using DomainLayer.Exceptions;
using Xunit;

namespace ApplicationLayer.UnitTests.Helpers;

/// <summary>SCRUM-466: gate classify CV / KB + Coach IT domain.</summary>
public sealed class ItDomainClassifyGateTests
{
    [Fact]
    public void EnsureCvPass_ResumeIt_Ok()
    {
        ItDomainClassifyGate.EnsureCvPass("resume", true, null);
    }

    [Fact]
    public void EnsureCvPass_NotResume_Throws422()
    {
        var ex = Assert.Throws<StructuredHttpException>(() =>
            ItDomainClassifyGate.EnsureCvPass("article", true, "Đây là tutorial."));
        Assert.Equal(422, ex.HttpStatusCode);
        Assert.Equal(ItDomainClassifyGate.StageCvClassify, ex.Payload.Stage);
    }

    [Fact]
    public void EnsureCvPass_NotItRole_Throws422()
    {
        var ex = Assert.Throws<StructuredHttpException>(() =>
            ItDomainClassifyGate.EnsureCvPass("resume", false, "CV Marketing."));
        Assert.Equal(422, ex.HttpStatusCode);
        Assert.Contains("Marketing", ex.Payload.Detail ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureCvHasSkills_Empty_Throws422()
    {
        var ex = Assert.Throws<StructuredHttpException>(() =>
            ItDomainClassifyGate.EnsureCvHasSkills(Array.Empty<string>()));
        Assert.Equal(422, ex.HttpStatusCode);
    }

    [Fact]
    public void EnsureKbPass_DocumentationIt_Ok()
    {
        ItDomainClassifyGate.EnsureKbPass("documentation", true, null);
        ItDomainClassifyGate.EnsureKbPass("article", true, null);
    }

    [Fact]
    public void EnsureKbPass_Resume_Throws422()
    {
        var ex = Assert.Throws<StructuredHttpException>(() =>
            ItDomainClassifyGate.EnsureKbPass("resume", true, null));
        Assert.Equal(422, ex.HttpStatusCode);
        Assert.Equal(ItDomainClassifyGate.StageKbClassify, ex.Payload.Stage);
    }

    [Fact]
    public void EnsureItDomainL1_Marketing_Throws()
    {
        var text = """
            Digital Marketing Executive
            Trách nhiệm: content marketing, SEO specialist, social media, sales manager.
            Yêu cầu: kế toán cơ bản, accountant skills.
            """;
        var ex = Assert.Throws<StructuredHttpException>(() =>
            ItDomainClassifyGate.EnsureItDomainL1(text, ItDomainClassifyGate.StageKbClassify));
        Assert.Equal(422, ex.HttpStatusCode);
    }

    [Fact]
    public void EnsureItDomainL1_DotNetDoc_Ok()
    {
        var text = """
            ASP.NET Core internal stack documentation.
            Backend with C#, Docker, Kubernetes, PostgreSQL, REST API, React frontend.
            """;
        ItDomainClassifyGate.EnsureItDomainL1(text, ItDomainClassifyGate.StageKbClassify);
    }

    [Theory]
    [InlineData("cv", "resume")]
    [InlineData("curriculum_vitae", "resume")]
    [InlineData("docs", "documentation")]
    [InlineData("tutorial", "article")]
    public void NormalizeDocumentType_Aliases(string raw, string expected)
    {
        Assert.Equal(expected, ItDomainClassifyGate.NormalizeDocumentType(raw));
    }
}

public sealed class CoachItDomainGateTests
{
    [Fact]
    public void EnsureItSkills_ReactStack_Ok()
    {
        CoachItDomainGate.EnsureItSkills(
            ["React", "TypeScript", "Node.js"],
            summary: "Frontend developer",
            suggestedRole: "Frontend Developer");
    }

    [Fact]
    public void EnsureItSkills_MarketingOnly_Throws()
    {
        var ex = Assert.Throws<BadRequestException>(() =>
            CoachItDomainGate.EnsureItSkills(
                ["SEO", "Content Marketing", "Social Media"],
                summary: "Digital marketing executive",
                targetRole: "Marketing Manager"));
        Assert.Contains("IT", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureItSkills_Empty_Throws()
    {
        Assert.Throws<BadRequestException>(() =>
            CoachItDomainGate.EnsureItSkills(Array.Empty<string>()));
    }
}
