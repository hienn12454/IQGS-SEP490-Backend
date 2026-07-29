using System.Text.RegularExpressions;
using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// Parse instruction chat refine → số câu / độ khó / loại câu ưu tiên.
/// RAG bắt buộc khớp so_cau = NumberOfQuestions, nên phải set đúng từ instruction (không chỉ để trong HrNote).
/// </summary>
public static partial class StudioRefineInstructionParser
{
    public sealed record ParsedRefine(
        int? NumberOfQuestions,
        QuestionDifficulty? Difficulty,
        bool TechnicalOnly,
        int QuestionDelta);

    public static ParsedRefine Parse(string instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
            return new ParsedRefine(null, null, false, 0);

        var text = instruction.Trim();
        var lower = text.ToLowerInvariant();

        int? count = TryParseExplicitCount(lower);
        var delta = 0;
        if (count is null)
            delta = InferQuestionDelta(lower);

        QuestionDifficulty? difficulty = null;
        if (ContainsAny(lower, "toàn bộ là khó", "toan bo la kho", "all hard", "all difficult", "toàn khó", "toan kho")
            || (ContainsAny(lower, "harder", "khó hơn", "kho hon") && !ContainsAny(lower, "easier", "dễ hơn")))
            difficulty = QuestionDifficulty.Hard;
        else if (ContainsAny(lower, " easier", "dễ hơn", "de hon", "all easy", "toàn dễ"))
            difficulty = QuestionDifficulty.Easy;
        else if (ContainsAny(lower, "medium", "trung bình", "trung binh"))
            difficulty = QuestionDifficulty.Medium;
        else if (Regex.IsMatch(lower, @"\b(hard|khó|kho)\b") && !ContainsAny(lower, "harder"))
            difficulty = QuestionDifficulty.Hard;
        else if (Regex.IsMatch(lower, @"\b(easy|dễ|de)\b"))
            difficulty = QuestionDifficulty.Easy;

        var technicalOnly = ContainsAny(lower,
            "technical only", "chỉ technical", "chi technical",
            "toàn về kỹ thuật", "toan ve ky thuat", "toàn về kĩ thuật", "toan ve ki thuat",
            "chỉ kỹ thuật", "chi ky thuat", "chỉ kĩ thuật", "chi ki thuat",
            "phần kỹ thuật", "phan ky thuat", "phần kĩ thuật", "phan ki thuat",
            "kỹ thuật thôi", "ky thuat thoi", "kĩ thuật thôi", "ki thuat thoi",
            "code thôi", "code thoi", "chỉ code", "chi code",
            "toàn về tech", "toan ve tech", "chỉ tech", "chi tech");

        // "cho tôi 20 …" khi không viết chữ "câu"
        count ??= TryParseChoToiCount(lower);

        if (count is int c)
            count = Math.Clamp(c, 5, 50);

        return new ParsedRefine(count, difficulty, technicalOnly, delta);
    }

    public static int ResolveNumberOfQuestions(int baseline, ParsedRefine parsed)
    {
        var n = baseline <= 0 ? 15 : baseline;
        if (parsed.NumberOfQuestions is int explicitCount)
            return explicitCount;
        if (parsed.QuestionDelta != 0)
            return Math.Clamp(n + parsed.QuestionDelta, 5, 50);
        return n;
    }

    public static List<string> ResolveQuestionTypes(ParsedRefine parsed, IReadOnlyList<string>? baselineTypes = null)
    {
        if (parsed.TechnicalOnly)
            return ["technical", "problem_solving"];
        var fromSettings = StudioQuestionTypesHelper.Normalize(baselineTypes);
        return fromSettings.Count > 0 ? fromSettings : StudioQuestionTypesHelper.DefaultTypes.ToList();
    }

    private static int? TryParseExplicitCount(string lower)
    {
        // "20 câu", "20 cau", "20 questions", "20 câu hỏi", "20 cau hoi"
        var m = Regex.Match(lower, @"(\d+)\s*(câu\s*hỏi|cau\s*hoi|câu|cau|questions?|qs)\b");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n1))
            return n1;

        // "tầm 20", "khoảng 20", "about 20", "around 20"
        m = Regex.Match(lower, @"(?:tầm|tam|khoảng|khoang|about|around|~)\s*(\d+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n2))
            return n2;

        // "total 20", "có 20 question", "cho tôi 20"
        m = Regex.Match(lower, @"(?:total|có|co|muốn|muon|cho\s+tôi|cho\s+toi)\s+(\d+)\s*(?:câu|cau|question)?");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n3) && n3 is >= 5 and <= 50)
            return n3;

        return null;
    }

    private static int? TryParseChoToiCount(string lower)
    {
        var m = Regex.Match(lower, @"cho\s+(?:tôi|toi|mình|minh)\s+(\d+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n is >= 5 and <= 50)
            return n;
        return null;
    }

    private static int InferQuestionDelta(string lower)
    {
        if (ContainsAny(lower, "add more system design", "thêm system design", "them system design"))
            return 2;
        if (ContainsAny(lower, "add one more behavioral", "thêm 1 behavioral", "them 1 behavioral"))
            return 1;
        if (ContainsAny(lower, "reduce behavioral", "bớt behavioral", "bot behavioral"))
            return -1;
        if (ContainsAny(lower, "thêm câu", "them cau", "add more question", "more questions"))
            return 2;
        if (ContainsAny(lower, "bớt câu", "bot cau", "fewer questions", "ít câu hơn"))
            return -2;
        return 0;
    }

    private static bool ContainsAny(string haystack, params string[] needles)
        => needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));
}
