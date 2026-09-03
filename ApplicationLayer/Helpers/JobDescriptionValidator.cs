using System.Text.RegularExpressions;
using DomainLayer.Constants;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Helpers;

/// <summary>
/// Validate JD trên BE — độ dài, signal groups cấu trúc, và domain IT (SCRUM-416).
/// </summary>
public static partial class JobDescriptionValidator
{
    public const int MinWords = 100;
    public const int MinChars = 400;
    public const int MaxChars = 30_000;
    public const int MaxWords = 5_000;
    private const int MinSignalGroups = 2;

    private static readonly (string Name, Regex Pattern)[] SignalGroups =
    [
        ("vai trò", RoleSignalRegex()),
        ("yêu cầu", RequirementSignalRegex()),
        ("trách nhiệm", ResponsibilitySignalRegex()),
        ("level", LevelSignalRegex()),
    ];

    /// <summary>Vai trò / chức danh IT (EN + VI).</summary>
    private static readonly string[] ItRoleKeywords =
    [
        "software engineer", "software developer", "backend", "front-end", "frontend", "full stack", "fullstack",
        "full-stack", "devops", "sre", "site reliability", "data engineer", "data scientist", "machine learning",
        "ml engineer", "ai engineer", "qa engineer", "test engineer", "quality assurance", "mobile developer",
        "ios developer", "android developer", "security engineer", "cybersecurity", "cloud engineer",
        "platform engineer", "database administrator", "dba", "system admin", "sysadmin", "it support",
        "technical lead", "tech lead", "solution architect", "software architect", "embedded",
        "lập trình viên", "kỹ sư phần mềm", "nhà phát triển", "phát triển phần mềm", "lập trình",
        "kiểm thử phần mềm", "kỹ sư qa", "kỹ sư dữ liệu",
    ];

    /// <summary>Công nghệ / stack IT — PM/BA chỉ pass khi có nhóm này.</summary>
    private static readonly string[] ItTechKeywords =
    [
        ".net", "asp.net", "c#", "csharp", "java", "spring boot", "python", "django", "fastapi", "flask",
        "javascript", "typescript", "react", "next.js", "nextjs", "angular", "vue", "node.js", "nodejs",
        "golang", "go lang", "rust", "kotlin", "swift", "php", "laravel", "ruby on rails",
        "sql", "postgresql", "postgres", "mysql", "mongodb", "redis", "elasticsearch",
        "docker", "kubernetes", "k8s", "ci/cd", "jenkins", "github actions", "gitlab ci",
        "aws", "azure", "gcp", "google cloud", "terraform", "ansible",
        "rest api", "graphql", "microservices", "kafka", "rabbitmq", "grpc",
        "git", "linux", "api gateway", "unit test", "xunit", "junit", "pytest",
        "html", "css", "tailwind", "webpack",
    ];

    private static readonly string[] NonItKeywords =
    [
        "marketing", "digital marketing", "content marketing", "seo specialist", "social media",
        "sales executive", "sales manager", "account executive", "business development",
        "kế toán", "accountant", "accounting", "bookkeeper", "kiểm toán", "auditor",
        "luật sư", "lawyer", "legal counsel", "pháp chế",
        "giáo viên", "teacher", "giảng viên", "nhân viên y tế", "bác sĩ", "nurse", "điều dưỡng",
        "nhà hàng", "restaurant", "pha chế", "bartender", "khách sạn", "hotel receptionist",
        "bất động sản", "real estate", "môi giới",
        "nhân viên bán hàng", "cashier", "thu ngân", "kho vận thuần", "warehouse picker",
        "hr generalist thuần hành chính", "lễ tân", "receptionist",
        "fashion designer", "thiết kế thời trang", "makeup artist",
    ];

    public static string Validate(string? text, string fileName = "JD")
    {
        var prepared = JobDescriptionTextNormalizer.PrepareForValidation(text);
        var label = string.IsNullOrWhiteSpace(fileName) ? "JD" : fileName;
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(prepared))
        {
            throw StructuredHttpException.FromBe(
                "JD không hợp lệ",
                ErrorStage.JdValidation,
                [$"File '{label}' không có nội dung text."],
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var charCount = prepared.Length;
        var wordCount = CountWords(prepared);

        if (charCount < MinChars)
        {
            errors.Add(
                $"JD quá ngắn ({charCount} ký tự, tối thiểu {MinChars}). " +
                "Vui lòng bổ sung mô tả vai trò, yêu cầu và trọng tâm phỏng vấn.");
        }
        else if (charCount > MaxChars)
        {
            errors.Add(
                $"JD quá dài ({charCount:N0} ký tự, tối đa {MaxChars:N0}). " +
                "Vui lòng tách file hoặc rút gọn phần không liên quan.");
        }

        if (wordCount < MinWords)
        {
            errors.Add(
                $"JD quá ngắn ({wordCount} từ, tối thiểu {MinWords}). " +
                "Vui lòng bổ sung chi tiết về vai trò và yêu cầu công việc.");
        }
        else if (wordCount > MaxWords)
        {
            errors.Add(
                $"JD quá dài ({wordCount:N0} từ, tối đa {MaxWords:N0}). " +
                "Vui lòng rút gọn hoặc tách thành nhiều file.");
        }

        var matchedGroups = SignalGroups
            .Where(g => g.Pattern.IsMatch(prepared))
            .Select(g => g.Name)
            .ToList();

        if (matchedGroups.Count < MinSignalGroups)
        {
            errors.Add(
                "Thiếu thông tin cốt lõi trong JD: cần có ít nhất 2 trong 4 nhóm " +
                "(vai trò, yêu cầu, trách nhiệm, level/kinh nghiệm). " +
                $"Hiện chỉ nhận diện được: {(matchedGroups.Count == 0 ? "không có" : string.Join(", ", matchedGroups))}.");
        }

        // SCRUM-416: chỉ nhận JD thuộc IT/phần mềm (keyword bilingual, deterministic).
        var itError = ValidateItDomain(prepared);
        if (itError is not null)
            errors.Add(itError);

        if (errors.Count > 0)
        {
            throw StructuredHttpException.FromBe(
                "JD không hợp lệ",
                ErrorStage.JdValidation,
                errors,
                errors[0],
                StatusCodes.Status422UnprocessableEntity);
        }

        return prepared;
    }

    /// <summary>
    /// Gate domain IT: reject khi không có tín hiệu IT, hoặc non-IT thắng rõ.
    /// PM/BA generic không đủ — cần ít nhất một tech keyword.
    /// </summary>
    public static string? ValidateItDomain(string text)
    {
        var lower = text.ToLowerInvariant();
        var roleScore = ScoreKeywords(lower, ItRoleKeywords);
        var techScore = ScoreKeywords(lower, ItTechKeywords);
        var itScore = roleScore + techScore;
        var nonItScore = ScoreKeywords(lower, NonItKeywords);

        if (itScore == 0)
        {
            return
                "Hệ thống chỉ nhận Job Description thuộc lĩnh vực IT/phần mềm. " +
                "JD này không có tín hiệu kỹ thuật đủ rõ (vai trò IT hoặc công nghệ như .NET, React, Java, SQL…).";
        }

        if (nonItScore > itScore)
        {
            return
                "Hệ thống chỉ nhận Job Description thuộc lĩnh vực IT/phần mềm. " +
                "JD này nghiêng về ngành ngoài IT (ví dụ marketing, sales, kế toán, luật…). " +
                "Vui lòng dùng JD kỹ thuật/phần mềm.";
        }

        return null;
    }

    /// <summary>Cho unit test / debug — điểm IT vs non-IT.</summary>
    public static (int ItScore, int NonItScore) ScoreItDomain(string text)
    {
        var lower = text.ToLowerInvariant();
        var it = ScoreKeywords(lower, ItRoleKeywords) + ScoreKeywords(lower, ItTechKeywords);
        var nonIt = ScoreKeywords(lower, NonItKeywords);
        return (it, nonIt);
    }

    private static int ScoreKeywords(string lower, string[] keywords)
    {
        var score = 0;
        foreach (var keyword in keywords)
        {
            if (lower.Contains(keyword, StringComparison.Ordinal))
                score++;
        }

        return score;
    }

    public static int CountWords(string text)
        => WordTokenRegex().Matches(text).Count;

    [GeneratedRegex(@"\S+")]
    private static partial Regex WordTokenRegex();

    [GeneratedRegex(@"job\s*description|position|role|vị\s*trí|nhân\s*viên|tuyển\s*dụng", RegexOptions.IgnoreCase)]
    private static partial Regex RoleSignalRegex();

    [GeneratedRegex(@"must[\s-]*have|requirement|yêu\s*cầu|qualification|kỹ\s*năng|kinh\s*nghiệm", RegexOptions.IgnoreCase)]
    private static partial Regex RequirementSignalRegex();

    [GeneratedRegex(@"responsibilit|nhiệm\s*vụ|mô\s*tả\s*công\s*việc|trách\s*nhiệm", RegexOptions.IgnoreCase)]
    private static partial Regex ResponsibilitySignalRegex();

    [GeneratedRegex(@"\bSWE\b|senior|junior|mid[\s-]*level|\byears?\b|năm\s*kinh\s*nghiệm|\blevel\b", RegexOptions.IgnoreCase)]
    private static partial Regex LevelSignalRegex();
}
