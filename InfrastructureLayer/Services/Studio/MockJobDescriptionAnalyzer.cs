using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Interfaces;
using System.Text.RegularExpressions;

namespace InfrastructureLayer.Services.Studio;

/// <summary>
/// Mock analyzer cho Auto-detected summary — nhận diện Role/Seniority/Language/Skills từ JD text.
/// Thiết kế để thay bằng AI provider thật sau này qua cùng interface IJobDescriptionAnalyzer.
/// </summary>
public sealed class MockJobDescriptionAnalyzer : IJobDescriptionAnalyzer
{
    private static readonly (string Keyword, string Skill)[] SkillRules =
    [
        (".net core", ".NET Core"),
        (".net", ".NET"),
        ("asp.net", "ASP.NET"),
        ("c#", "C#"),
        ("postgresql", "PostgreSQL"),
        ("sql server", "SQL Server"),
        ("sql", "SQL"),
        ("azure", "Azure"),
        ("aws", "AWS"),
        ("microservices", "Microservices"),
        ("system design", "System Design"),
        ("ci/cd", "CI/CD"),
        ("docker", "Docker"),
        ("kubernetes", "Kubernetes"),
        ("redis", "Redis"),
        ("api", "API"),
        ("testing", "Testing"),
        ("xunit", "xUnit"),
        ("react", "React"),
        ("typescript", "TypeScript"),
        ("javascript", "JavaScript"),
        ("python", "Python"),
        ("java", "Java"),
    ];

    public Task<AnalyzeJobDescriptionResponse> AnalyzeAsync(string content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lower = content.ToLowerInvariant();

        var role = DetectRole(lower, content);
        var seniority = DetectSeniority(lower);
        var language = DetectLanguage(content);
        var skills = DetectSkills(lower);

        return Task.FromResult(new AnalyzeJobDescriptionResponse(role, seniority, language, skills));
    }

    private static string DetectRole(string lower, string original)
    {
        if (lower.Contains("senior .net") || lower.Contains("senior backend") || (lower.Contains(".net") && lower.Contains("backend")))
            return "Senior .NET Backend Engineer";
        if (lower.Contains("backend engineer") || lower.Contains("backend developer"))
            return "Backend Engineer";
        if (lower.Contains("frontend") || lower.Contains("front-end"))
            return "Frontend Engineer";
        if (lower.Contains("full stack") || lower.Contains("fullstack"))
            return "Full Stack Engineer";
        if (lower.Contains("devops"))
            return "DevOps Engineer";
        if (lower.Contains("data engineer"))
            return "Data Engineer";
        if (lower.Contains("data scientist"))
            return "Data Scientist";
        if (lower.Contains("product manager"))
            return "Product Manager";
        if (lower.Contains("qa") || lower.Contains("quality assurance") || lower.Contains("test engineer"))
            return "QA Engineer";

        var titleMatch = Regex.Match(original, @"(?im)^\s*(?:job title|position|role)\s*[:\-]\s*(.+)$");
        if (titleMatch.Success)
            return titleMatch.Groups[1].Value.Trim().TrimEnd('.');

        return "Software Engineer";
    }

    private static string DetectSeniority(string lower)
    {
        if (lower.Contains("intern") || lower.Contains("internship"))
            return "Intern";
        if (lower.Contains("junior") || lower.Contains("entry-level") || lower.Contains("entry level"))
            return "Junior";
        if (lower.Contains("lead") || lower.Contains("principal") || lower.Contains("staff engineer"))
            return "Lead";
        if (lower.Contains("senior") || lower.Contains("sr."))
            return "Senior";
        if (lower.Contains("mid-level") || lower.Contains("mid level") || Regex.IsMatch(lower, @"\bmid\b"))
            return "Mid";
        return "Mid";
    }

    private static string DetectLanguage(string content)
    {
        var vietnameseChars = content.Count(c => "ăâêôơưđáàảãạéèẻẽẹíìỉĩịóòỏõọúùủũụýỳỷỹỵĂÂÊÔƠƯĐ".Contains(c));
        if (vietnameseChars >= 8)
            return "Vietnamese";
        return "English";
    }

    private static string[] DetectSkills(string lower)
    {
        var skills = new List<string>();
        foreach (var (keyword, skill) in SkillRules)
        {
            if (lower.Contains(keyword) && !skills.Contains(skill, StringComparer.OrdinalIgnoreCase))
                skills.Add(skill);
        }

        return skills.Take(12).ToArray();
    }
}
