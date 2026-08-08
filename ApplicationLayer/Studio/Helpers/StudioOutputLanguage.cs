namespace ApplicationLayer.Studio.Helpers;

/// <summary>Chuẩn hóa ngôn ngữ đầu ra Studio (UI: Vietnamese | English).</summary>
public static class StudioOutputLanguage
{
    public const string Vietnamese = "Vietnamese";
    public const string English = "English";

    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Vietnamese;

        var t = raw.Trim();
        if (t.Equals("English", StringComparison.OrdinalIgnoreCase)
            || t.Equals("en", StringComparison.OrdinalIgnoreCase)
            || t.Equals("en-US", StringComparison.OrdinalIgnoreCase)
            || t.Equals("en-GB", StringComparison.OrdinalIgnoreCase))
            return English;

        if (t.Equals("Vietnamese", StringComparison.OrdinalIgnoreCase)
            || t.Equals("vi", StringComparison.OrdinalIgnoreCase)
            || t.Equals("vn", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Việt", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Viet", StringComparison.OrdinalIgnoreCase))
            return Vietnamese;

        // Fallback: nếu nhận "en" lạ / tên đầy đủ khác
        return t.Contains("eng", StringComparison.OrdinalIgnoreCase) ? English : Vietnamese;
    }

    /// <summary>Directive ngắn đưa vào RAG hrNote / prompt.</summary>
    public static string RagInstruction(string? raw)
    {
        var lang = Normalize(raw);
        return lang == English
            ? "OUTPUT_LANGUAGE=English. Write ALL question text, sample answers, rationale, goals, summary, and notes in English."
            : "OUTPUT_LANGUAGE=Vietnamese. Write ALL question text, sample answers, rationale, goals, summary, and notes in Vietnamese (Tiếng Việt).";
    }
}
