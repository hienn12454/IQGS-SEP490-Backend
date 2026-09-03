using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Interfaces;
using System.Text.RegularExpressions;

namespace InfrastructureLayer.Services.Studio;

/// <summary>
/// Analyzer heuristic (fallback) — Role/Seniority/Language/Skills/Position từ JD text.
/// SCRUM-416: không fallback ảo "Software Engineer"/"Mid"; field không chắc → null.
/// SCRUM-432: không gọi LLM classify — coi text đã qua JobDescriptionValidator = pass (unit test / offline).
/// Production dùng RagJobDescriptionAnalyzer.
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
        var position = ExtractPosition(content) ?? role;

        return Task.FromResult(new AnalyzeJobDescriptionResponse(role, seniority, language, skills, position));
    }

    private static string? DetectRole(string lower, string original)
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

        var fromLine = ExtractPosition(original);
        if (!string.IsNullOrWhiteSpace(fromLine))
            return fromLine;

        // SCRUM-416: không fallback "Software Engineer"
        return null;
    }

    /// <summary>
    /// SCRUM-416: lấy vị trí từ dòng nhãn EN/VI hoặc câu “Tìm kiếm một …”.
    /// </summary>
    public static string? ExtractPosition(string original)
    {
        if (string.IsNullOrWhiteSpace(original))
            return null;

        var labeled = Regex.Match(
            original,
            @"(?im)^\s*(?:job\s*title|position|role|vị\s*trí|chức\s*danh|tuyển\s*dụng)\s*[:\-]\s*(.+)$");
        if (labeled.Success)
            return TruncatePosition(labeled.Groups[1].Value);

        var seeking = Regex.Match(
            original,
            @"(?is)(?:we\s+are\s+(?:looking\s+for|hiring)|looking\s+for|hiring|tìm\s*kiếm(?:\s+một)?|đang\s+tuyển)\s+(.+?)(?:\s+với|\s+với\s+|\s+to\s+|\s+who\s+|\.|,|\n)");
        if (seeking.Success)
            return TruncatePosition(seeking.Groups[1].Value);

        return null;
    }

    private static string TruncatePosition(string raw)
    {
        var cleaned = raw.Trim().TrimEnd('.', ',', ';', ':');
        if (cleaned.Length > 150)
            cleaned = cleaned[..150].Trim();
        return cleaned;
    }

    private static string? DetectSeniority(string lower)
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
        // SCRUM-416: không default Mid
        return null;
    }

    private static string? DetectLanguage(string content)
    {
        var vietnameseChars = content.Count(c => "ăâêôơưđáàảãạéèẻẽẹíìỉĩịóòỏõọúùủũụýỳỷỹỵĂÂÊÔƠƯĐ".Contains(c));
        if (vietnameseChars >= 8)
            return "Vietnamese";
        // Chỉ English khi text chủ yếu ASCII chữ cái — không đoán bừa
        var letters = content.Count(char.IsLetter);
        if (letters >= 40 && vietnameseChars == 0)
            return "English";
        return null;
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
