using System.Text.Json;
using DomainLayer.Entities;

namespace ApplicationLayer.Helpers;

/// <summary>Đọc các field tóm tắt từ PlanJson để hiển thị danh sách plan.</summary>
public static class PlanJsonSummaryReader
{
    public static PlanJsonSummary Read(QuestionGenerationJob job, QuestionGenerationPlan plan)
    {
        var summary = new PlanJsonSummary();

        if (string.IsNullOrWhiteSpace(plan.PlanJson))
            return summary;

        try
        {
            using var doc = JsonDocument.Parse(plan.PlanJson);
            var root = doc.RootElement;

            // Role = tiêu đề vị trí thật — KHÔNG dùng skills join (gây chuỗi dài trên Insights/KPI).
            if (TryGetStringAny(root, out var roleTitle,
                    "roleTitle", "role_title", "jobTitle", "job_title", "title", "detectedRole", "detected_role")
                && !string.IsNullOrWhiteSpace(roleTitle)
                && !LooksLikeSkillsList(roleTitle))
            {
                summary.JobTitle = roleTitle.Trim();
                summary.Role = roleTitle.Trim();
            }

            if (TryGetString(root, "level", out var level) && !string.IsNullOrWhiteSpace(level))
                summary.Level = level;
            else if (TryGetString(root, "difficulty", out var difficulty) && !string.IsNullOrWhiteSpace(difficulty))
                summary.Level = difficulty;

            if (TryGetInt(root, "totalQuestions", out var totalQuestions) && totalQuestions > 0)
                summary.Question = totalQuestions;
            else if (TryGetInt(root, "total_questions", out totalQuestions) && totalQuestions > 0)
                summary.Question = totalQuestions;

            if (TryGetStringAny(root, out var experienceLevel, "experienceLevel", "experience_level")
                && !string.IsNullOrWhiteSpace(experienceLevel))
                summary.ExperienceLevel = experienceLevel;

            if (root.TryGetProperty("skills", out var skillsEl) && skillsEl.ValueKind == JsonValueKind.Array)
            {
                summary.Skills = skillsEl.EnumerateArray()
                    .Select(s => s.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // PlanJson lỗi format — trả summary rỗng
        }

        return summary;
    }

    /// <summary>
    /// Chuỗi kiểu ".NET, C#, Azure, Docker..." không phải role title — bỏ qua khi gán Role.
    /// </summary>
    internal static bool LooksLikeSkillsList(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > 80) return true;
        var commaCount = trimmed.Count(c => c == ',');
        return commaCount >= 2;
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.String)
            return false;

        value = el.GetString();
        return true;
    }

    private static bool TryGetStringAny(
        JsonElement root,
        out string? value,
        params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (TryGetString(root, name, out value))
                return true;
        }

        value = null;
        return false;
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

    public sealed class PlanJsonSummary
    {
        public string JobTitle { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string ExperienceLevel { get; set; } = string.Empty;
        public int Question { get; set; }
        public List<string> Skills { get; set; } = new();
    }
}
