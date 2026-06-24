using System.Text.Json;
using DomainLayer.Entities;

namespace ApplicationLayer.Helpers;

/// <summary>Đọc các field tóm tắt từ PlanJson để hiển thị danh sách plan.</summary>
public static class PlanJsonSummaryReader
{
    private const int JobDescriptionPreviewLength = 120;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static PlanJsonSummary Read(QuestionGenerationJob job, QuestionGenerationPlan plan)
    {
        var fallbackSkills = DeserializeSkills(job.SkillsJson);
        var summary = new PlanJsonSummary
        {
            JobTitle = string.Empty,
            Role = JoinSkills(fallbackSkills),
            Level = job.Difficulty,
            Question = job.NumberOfQuestions
        };

        if (string.IsNullOrWhiteSpace(plan.PlanJson))
            return ApplyJobTitleFallback(summary, job.JobDescription);

        try
        {
            using var doc = JsonDocument.Parse(plan.PlanJson);
            var root = doc.RootElement;

            if (TryGetString(root, "roleTitle", out var roleTitle) && !string.IsNullOrWhiteSpace(roleTitle))
                summary.JobTitle = roleTitle;

            if (TryGetString(root, "difficulty", out var difficulty) && !string.IsNullOrWhiteSpace(difficulty))
                summary.Level = difficulty;

            if (TryGetInt(root, "totalQuestions", out var totalQuestions) && totalQuestions > 0)
                summary.Question = totalQuestions;

            if (root.TryGetProperty("skills", out var skillsEl) && skillsEl.ValueKind == JsonValueKind.Array)
            {
                var skills = skillsEl.EnumerateArray()
                    .Select(s => s.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToList();

                if (skills.Count > 0)
                    summary.Role = JoinSkills(skills);
            }
        }
        catch (JsonException)
        {
            // PlanJson lỗi format — giữ fallback từ job
        }

        return ApplyJobTitleFallback(summary, job.JobDescription);
    }

    private static PlanJsonSummary ApplyJobTitleFallback(PlanJsonSummary summary, string jobDescription)
    {
        if (!string.IsNullOrWhiteSpace(summary.JobTitle))
            return summary;

        summary.JobTitle = BuildJobDescriptionPreview(jobDescription);
        return summary;
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.String)
            return false;

        value = el.GetString();
        return true;
    }

    private static bool TryGetInt(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var el))
            return false;

        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out value))
            return true;

        return false;
    }

    private static List<string> DeserializeSkills(string skillsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(skillsJson, JsonOptions) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static string JoinSkills(IReadOnlyList<string> skills)
        => string.Join(", ", skills.Where(s => !string.IsNullOrWhiteSpace(s)));

    private static string BuildJobDescriptionPreview(string jobDescription)
    {
        var trimmed = jobDescription.Trim();
        if (trimmed.Length <= JobDescriptionPreviewLength)
            return trimmed;
        return trimmed.Substring(0, JobDescriptionPreviewLength) + "...";
    }

    public sealed class PlanJsonSummary
    {
        public string JobTitle { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public int Question { get; set; }
    }
}
