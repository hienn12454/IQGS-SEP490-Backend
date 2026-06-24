using System.Text.RegularExpressions;
using DomainLayer.Constants;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Helpers;

/// <summary>
/// Validate JD trên BE — tối thiểu 100 từ, không bắt buộc số dòng (tránh lỗi client gửi một đoạn dài).
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
                [$"File '{label}' không có nội dung text."]);
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

        if (errors.Count > 0)
        {
            throw StructuredHttpException.FromBe(
                "JD không hợp lệ",
                ErrorStage.JdValidation,
                errors,
                errors[0]);
        }

        return prepared;
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
