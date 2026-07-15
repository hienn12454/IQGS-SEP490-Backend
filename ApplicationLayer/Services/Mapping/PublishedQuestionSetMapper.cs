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
        CompanyLogo = row.CompanyLogo,
        Difficulty = row.Difficulty,
        Skills = ParseJsonList<string>(row.SkillsJson),
        TotalQuestions = row.TotalQuestions,
        EstimatedTimeMinutes = row.TotalQuestions * EstimatedMinutesPerQuestion
    };

    public static string ResolveTitle(string? title, string companyName) =>
        string.IsNullOrWhiteSpace(title) ? companyName : title;

    public static List<T> ParseJsonList<T>(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new();
}
