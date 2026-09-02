namespace ApplicationLayer.Studio.Helpers;

/// <summary>SCRUM-417: chuẩn hóa cấp độ JD (UI/DB PascalCase ↔ RAG lowercase).</summary>
public static class StudioJdSeniority
{
    public static readonly string[] AllowedDisplay =
        ["Intern", "Junior", "Mid", "Senior", "Lead"];

    /// <summary>Chuẩn hóa về Intern|Junior|Mid|Senior|Lead; null nếu không hợp lệ.</summary>
    public static string? NormalizeDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var key = value.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
        return key switch
        {
            "intern" or "internship" or "fresher" => "Intern",
            "junior" or "entry" or "entrylevel" => "Junior",
            "mid" or "middle" or "midlevel" => "Mid",
            "senior" or "sr" => "Senior",
            "lead" or "principal" or "staff" => "Lead",
            _ => null
        };
    }

    /// <summary>Map display → intern|junior|mid|senior|lead cho RAG GeneratePlanRequest.</summary>
    public static string? ToRagExperienceLevel(string? displayOrRaw)
    {
        var display = NormalizeDisplay(displayOrRaw);
        return display?.ToLowerInvariant();
    }

    public static bool IsValid(string? value) => NormalizeDisplay(value) is not null;
}
