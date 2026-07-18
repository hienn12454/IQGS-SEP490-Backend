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

    public static CandidateQuestionSetListItemDto ToListItemDto(PublishedQuestionSetRow row) => new()
    {
        Id = row.Id,
        Title = ResolveTitle(row.Title, row.CompanyName),
        CompanyName = row.CompanyName,
        CompanyLogo = CompanyLogoResolver.Resolve(row.CompanyLogo, row.CompanyWebsite, row.CompanyName),
        Description = row.Description,
        Difficulty = row.Difficulty,
        Skills = ParseJsonList<string>(row.SkillsJson),
        TotalQuestions = row.TotalQuestions,
        EstimatedTimeMinutes = row.TotalQuestions * EstimatedMinutesPerQuestion,
        Rating = RoundRating(row.Rating),
        AttemptCount = row.AttemptCount
    };

    public static string ResolveTitle(string? title, string companyName) =>
        string.IsNullOrWhiteSpace(title) ? companyName : title;

    /// <summary>Làm tròn rating về 1 chữ số thập phân cho UI (vd 4.75 → 4.8) — giữ null khi chưa có dữ liệu.</summary>
    public static double? RoundRating(double? rating) =>
        rating is double r ? Math.Round(r, 1) : null;

    public static List<T> ParseJsonList<T>(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new();
}
