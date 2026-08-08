namespace ApplicationLayer.Helpers;

/// <summary>SCRUM-400: chuẩn hóa phương thức trả lời Candidate — Text | Code.</summary>
public static class AnswerMethodNormalizer
{
    public const string Text = "Text";
    public const string Code = "Code";

    private static readonly HashSet<string> CodeTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        "CODE_COMPLETION",
        "BUG_DETECTION",
        "REFACTORING",
        "TEST_CASE_DESIGN",
        "PERFORMANCE_ANALYSIS"
    };

    /// <summary>Validate bắt buộc cho HR create/update — ném nếu thiếu/sai.</summary>
    public static string Require(string? raw)
    {
        var normalized = NormalizeOrNull(raw);
        if (normalized is null)
            throw new DomainLayer.Exceptions.BadRequestException(
                "answerMethod bắt buộc và chỉ nhận Text hoặc Code.");
        return normalized;
    }

    /// <summary>Chuẩn hóa Text|Code; null nếu giá trị không hợp lệ / trống.</summary>
    public static string? NormalizeOrNull(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var key = raw.Trim().ToLowerInvariant();
        return key switch
        {
            "text" or "theory" or "essay" => Text,
            "code" or "coding" or "programming" => Code,
            _ => null
        };
    }

    /// <summary>
    /// Resolve cho Candidate/Studio: ưu tiên giá trị đã lưu; fallback theo snippet/template (bộ cũ).
    /// </summary>
    public static string Resolve(string? stored, string? codeTemplateType = null, string? codeSnippet = null)
    {
        var fromStored = NormalizeOrNull(stored);
        if (fromStored is not null)
            return fromStored;

        return InferFromTemplate(codeTemplateType, codeSnippet);
    }

    public static string InferFromTemplate(string? codeTemplateType, string? codeSnippet)
    {
        var tpl = (codeTemplateType ?? string.Empty).Trim().ToUpperInvariant();
        if (CodeTemplates.Contains(tpl))
            return Code;
        if (!string.IsNullOrWhiteSpace(codeSnippet))
            return Code;
        return Text;
    }
}
