using System.Text.Json;
using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.QuestionSet;

namespace ApplicationLayer.Services.Mapping;

internal static class PublishedQuestionSetMapper
{
    /// <summary>Ước lượng thời gian làm bài — chưa có số liệu thực tế, tạm tính theo số câu hỏi.</summary>
    public const int EstimatedMinutesPerQuestion = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static CandidateQuestionSetListItemDto ToListItemDto(
        PublishedQuestionSetRow row, int minAttemptsForTrending = 10) => new()
    {
        Id = row.Id,
        Title = ResolveTitle(row.Title, row.CompanyName),
        CompanyId = row.CompanyId,
        CompanyName = row.CompanyName,
        CompanyLogo = CompanyLogoResolver.Resolve(row.CompanyLogo, row.CompanyWebsite, row.CompanyName),
        Description = row.Description,
        Difficulty = row.Difficulty,
        Skills = MergeSkills(row.QuestionSkills, row.SkillsJson),
        TotalQuestions = row.TotalQuestions,
        EstimatedTimeMinutes = row.TimeLimitMinutes ?? row.TotalQuestions * EstimatedMinutesPerQuestion,
        TimeLimitMinutes = row.TimeLimitMinutes,
        Rating = RoundRating(row.Rating),
        AttemptCount = row.AttemptCount,
        IsPinned = row.IsPinned,
        IsTrending = row.AttemptCount >= minAttemptsForTrending
    };

    public static string ResolveTitle(string? title, string companyName) =>
        string.IsNullOrWhiteSpace(title) ? companyName : title;

    /// <summary>OverallScore của phiên luyện tập chấm trên thang 0-100 (SCRUM-304) — hệ số quy đổi sang rating sao 0-5.</summary>
    private const double OverallScoreToStarRatingDivisor = 20.0;

    /// <summary>
    /// Quy đổi OverallScore trung bình (thang 0-100, do AI chấm — SCRUM-304) sang rating sao hiển thị marketplace
    /// (thang 0-5, vd 4.8 ★). Clamp về [0, 5] để chống lệch thang nếu dữ liệu bất thường, làm tròn 1 chữ số thập phân.
    /// Giữ null khi chưa có phiên nào được chấm.
    /// </summary>
    public static double? RoundRating(double? overallScoreAverage) =>
        overallScoreAverage is double avg
            ? Math.Round(Math.Clamp(avg / OverallScoreToStarRatingDivisor, 0, 5), 1)
            : null;

    public static List<T> ParseJsonList<T>(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new();

    /// <summary>
    /// Gộp skill cho chip filter: skill thực tế của các câu hỏi (nguồn chính) + skill HR nhập trên job (fallback/bổ sung),
    /// khử trùng lặp không phân biệt hoa thường — vì SkillsJson của job là optional, HR bỏ trống sẽ rỗng.
    /// </summary>
    public static List<string> MergeSkills(IEnumerable<string>? questionSkills, string? skillsJson)
    {
        var merged = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var skill in (questionSkills ?? Enumerable.Empty<string>()).Concat(ParseJsonList<string>(skillsJson)))
        {
            var trimmed = skill?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed))
                merged.Add(trimmed);
        }

        return merged;
    }
}
