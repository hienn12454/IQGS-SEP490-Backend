using System.Text.Json;
using ApplicationLayer.DTOs.Rag;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-452: kiểm tra bộ câu hỏi RAG trả về có đúng blueprint của Backend hay không.
/// Lý do: skill + difficulty của từng câu là input cho công thức competency, nên LLM
/// không được tự đổi. Câu nào lệch skill/thiếu slot -> fail job thay vì chấm điểm sai.
/// </summary>
public static class BlueprintComplianceValidator
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public sealed record Slot(int Order, string Skill, string Topic, string Difficulty);

    public sealed record Result(
        bool Ok,
        string? Error,
        List<RagGeneratedQuestionDto> Questions);

    /// <summary>Đọc outline từ PlanJson của job (blueprint do Backend dựng).</summary>
    public static List<Slot> ParseSlots(string? planJson)
    {
        var slots = new List<Slot>();
        if (string.IsNullOrWhiteSpace(planJson)) return slots;
        try
        {
            using var doc = JsonDocument.Parse(planJson);
            if (!doc.RootElement.TryGetProperty("recommendedQuestionOutline", out var outline)
                || outline.ValueKind != JsonValueKind.Array)
                return slots;

            foreach (var row in outline.EnumerateArray())
            {
                var skill = ReadString(row, "skill");
                if (string.IsNullOrWhiteSpace(skill)) continue;
                var order = row.TryGetProperty("order", out var o) && o.TryGetInt32(out var v) ? v : slots.Count + 1;
                var topic = ReadString(row, "topic") ?? ReadString(row, "focusArea") ?? skill!;
                var difficulty = DiagnosticBlueprintBuilder.NormalizeDifficulty(ReadString(row, "difficulty"));
                slots.Add(new Slot(order, skill!.Trim(), topic, difficulty));
            }
        }
        catch (JsonException)
        {
            return new List<Slot>();
        }
        return slots.OrderBy(s => s.Order).ToList();
    }

    /// <summary>
    /// Gán từng câu hỏi RAG vào slot blueprint theo skill (ưu tiên đúng difficulty).
    /// Slot là nguồn sự thật: skill/difficulty/topic của câu hỏi được ghi lại theo slot.
    /// </summary>
    public static Result Validate(
        IReadOnlyList<Slot> slots,
        IReadOnlyList<RagGeneratedQuestionDto> generated)
    {
        if (slots.Count == 0)
            // Không có blueprint (vd. job JD thường) -> không áp luật này.
            return new Result(true, null, generated.ToList());

        if (generated.Count < slots.Count)
            return new Result(false,
                $"RAG chỉ sinh {generated.Count}/{slots.Count} câu theo blueprint. Hãy thử lại.",
                new List<RagGeneratedQuestionDto>());

        var allowedSkills = slots
            .Select(s => s.Skill)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var unmatchedSkills = new List<string>();
        var candidates = new List<(RagGeneratedQuestionDto Question, string Skill, string Difficulty)>();
        foreach (var q in generated.OrderBy(q => q.Order ?? int.MaxValue))
        {
            var mapped = MapSkill(q.Skill, allowedSkills);
            if (mapped is null)
            {
                unmatchedSkills.Add(string.IsNullOrWhiteSpace(q.Skill) ? "(trống)" : q.Skill!);
                continue;
            }
            candidates.Add((q, mapped, DiagnosticBlueprintBuilder.NormalizeDifficulty(q.Difficulty)));
        }

        if (unmatchedSkills.Count > 0)
            return new Result(false,
                "RAG sinh câu hỏi cho skill ngoài blueprint: "
                + string.Join(", ", unmatchedSkills.Distinct(StringComparer.OrdinalIgnoreCase))
                + $". Blueprint chỉ cho phép: {string.Join(", ", allowedSkills)}.",
                new List<RagGeneratedQuestionDto>());

        var remaining = new List<(RagGeneratedQuestionDto Question, string Skill, string Difficulty)>(candidates);
        var filled = new List<(Slot Slot, RagGeneratedQuestionDto Question)>();

        foreach (var slot in slots)
        {
            // Ưu tiên câu đúng cả skill lẫn difficulty, sau đó chấp nhận cùng skill khác difficulty.
            var idx = remaining.FindIndex(c =>
                string.Equals(c.Skill, slot.Skill, StringComparison.OrdinalIgnoreCase)
                && c.Difficulty == slot.Difficulty);
            if (idx < 0)
                idx = remaining.FindIndex(c =>
                    string.Equals(c.Skill, slot.Skill, StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
                return new Result(false,
                    $"Blueprint cần thêm câu cho skill \"{slot.Skill}\" (mức {slot.Difficulty}) nhưng RAG không sinh đủ. Hãy thử lại.",
                    new List<RagGeneratedQuestionDto>());

            filled.Add((slot, remaining[idx].Question));
            remaining.RemoveAt(idx);
        }

        var normalized = new List<RagGeneratedQuestionDto>(filled.Count);
        foreach (var (slot, question) in filled.OrderBy(f => f.Slot.Order))
        {
            question.Order = slot.Order;
            question.Skill = slot.Skill;
            question.Difficulty = slot.Difficulty;
            if (string.IsNullOrWhiteSpace(question.FocusArea))
                question.FocusArea = slot.Topic;
            normalized.Add(question);
        }

        return new Result(true, null, normalized);
    }

    /// <summary>Map tên skill LLM trả về (vd. "C# .NET") sang skill chuẩn của framework ("C#").</summary>
    public static string? MapSkill(string? raw, IReadOnlyList<string> allowedSkills)
    {
        var value = Normalize(raw);
        if (value.Length == 0) return null;

        foreach (var allowed in allowedSkills)
            if (Normalize(allowed) == value)
                return allowed;

        // Chứa lẫn nhau: "asp.net core web api" ~ "asp.net core"; chọn skill khớp dài nhất cho ổn định.
        string? best = null;
        var bestLen = 0;
        foreach (var allowed in allowedSkills)
        {
            var a = Normalize(allowed);
            if (a.Length == 0) continue;
            if ((value.Contains(a, StringComparison.Ordinal) || a.Contains(value, StringComparison.Ordinal))
                && a.Length > bestLen)
            {
                best = allowed;
                bestLen = a.Length;
            }
        }
        return best;
    }

    /// <summary>Bỏ dấu cách và ký tự phụ để "EF Core" == "efcore" == "ef-core".</summary>
    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '#' || c == '+' || c == '.')
            .ToArray();
        return new string(chars);
    }

    private static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Tổng số câu blueprint yêu cầu — dùng cho note gửi RAG.</summary>
    public static int ReadTotalQuestions(string? planJson, int fallback)
    {
        if (string.IsNullOrWhiteSpace(planJson)) return fallback;
        try
        {
            using var doc = JsonDocument.Parse(planJson);
            if (doc.RootElement.TryGetProperty("totalQuestions", out var total)
                && total.TryGetInt32(out var value) && value > 0)
                return value;
        }
        catch (JsonException)
        {
            return fallback;
        }
        return fallback;
    }

    /// <summary>Skill được blueprint cho phép — dùng cho note gửi RAG.</summary>
    public static List<string> ReadSkills(string? planJson)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(planJson)) return result;
        try
        {
            using var doc = JsonDocument.Parse(planJson);
            if (doc.RootElement.TryGetProperty("skills", out var skills) && skills.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in skills.EnumerateArray())
                {
                    var value = s.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(value)) result.Add(value);
                }
            }
        }
        catch (JsonException)
        {
            return result;
        }
        return result;
    }

    /// <summary>Serialize lại slot cho log/debug.</summary>
    public static string Describe(IReadOnlyList<Slot> slots)
        => JsonSerializer.Serialize(slots, JsonOpts);
}
