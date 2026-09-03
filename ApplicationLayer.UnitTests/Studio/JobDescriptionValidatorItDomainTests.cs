using ApplicationLayer.Helpers;
using DomainLayer.Exceptions;
using Xunit;

namespace ApplicationLayer.UnitTests.Studio;

/// <summary>SCRUM-416: validate cấu trúc + domain IT; extract vị trí.</summary>
public sealed class JobDescriptionValidatorItDomainTests
{
    private static string PadToMinWords(string body, int minWords = 100)
    {
        var words = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var filler = string.Join(" ", Enumerable.Repeat("chi tiết", Math.Max(0, minWords - words + 5)));
        return body + "\n" + filler;
    }

    private static readonly string ValidItJd = PadToMinWords("""
        Job Description - Full Stack Developer

        Vị trí: Fullstack Developer

        Trách nhiệm:
        - Xây dựng RESTful API sử dụng ASP.NET Core hoặc Node.js.
        - Phát triển giao diện React.js hoặc Next.js.
        - Thiết kế database PostgreSQL và viết unit test.

        Yêu cầu:
        - Kinh nghiệm C#, .NET Core, TypeScript.
        - Hiểu REST API, Docker, Git.

        Level: Mid level, 2+ years experience.
        """);

    private static readonly string MarketingJd = PadToMinWords("""
        Job Description - Digital Marketing Executive

        Vị trí: Digital Marketing Executive

        Trách nhiệm:
        - Lập kế hoạch content marketing và social media campaign.
        - Tối ưu SEO, theo dõi KPI chuyển đổi bán hàng.
        - Phối hợp sales manager và business development.

        Yêu cầu:
        - Kinh nghiệm marketing, SEO specialist, content marketing.
        - Kỹ năng viết bài và quản lý fanpage.

        Level: Junior level, 1+ years experience.
        """);

    [Fact]
    public void Validate_ItJd_Succeeds()
    {
        var prepared = JobDescriptionValidator.Validate(ValidItJd);
        Assert.False(string.IsNullOrWhiteSpace(prepared));
        var (it, nonIt) = JobDescriptionValidator.ScoreItDomain(ValidItJd);
        Assert.True(it > 0);
        Assert.True(it >= nonIt);
    }

    [Fact]
    public void Validate_MarketingJd_ThrowsItDomain()
    {
        var ex = Assert.Throws<StructuredHttpException>(() => JobDescriptionValidator.Validate(MarketingJd));
        Assert.Equal(422, ex.HttpStatusCode);
        Assert.Contains("IT", ex.Payload.Detail ?? ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ex.Payload.Errors, e => e.Contains("IT", StringComparison.OrdinalIgnoreCase)
            || e.Contains("phần mềm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateItDomain_NoTech_ReturnsError()
    {
        var text = "Tuyển dụng nhân viên hành chính văn phòng làm việc tại Hà Nội.";
        var err = JobDescriptionValidator.ValidateItDomain(text);
        Assert.NotNull(err);
    }
}
