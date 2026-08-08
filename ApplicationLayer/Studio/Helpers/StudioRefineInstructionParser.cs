using System.Text.RegularExpressions;
using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// SCRUM-388 / SCRUM-389: Parse instruction chat refine → Studio Settings + exclusive/fast-path.
/// </summary>
public static partial class StudioRefineInstructionParser
{
    /// <summary>Intent đầy đủ từ chat.</summary>
    public sealed record StudioChatSettingsIntent(
        int? NumberOfQuestions,
        int? InterviewLengthMinutes,
        QuestionDifficulty? Difficulty,
        IReadOnlyList<string>? QuestionTypes,
        string? QuestionTone,
        string? OutputFormat,
        string? Language,
        bool? IncludeSampleAnswers,
        bool? IncludeScoringRubric,
        IReadOnlyList<string> FocusHints,
        int QuestionDelta,
        bool TechnicalOnly,
        /// <summary>SCRUM-389: thu hẹp coverage chỉ quanh FocusHints.</summary>
        bool ExclusiveFocus);

    /// <summary>Giữ tương thích caller cũ.</summary>
    public sealed record ParsedRefine(
        int? NumberOfQuestions,
        QuestionDifficulty? Difficulty,
        bool TechnicalOnly,
        int QuestionDelta);

    public static StudioChatSettingsIntent ParseIntent(string instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return new StudioChatSettingsIntent(
                null, null, null, null, null, null, null, null, null, [], 0, false, false);
        }

        var text = instruction.Trim();
        var lower = text.ToLowerInvariant();

        int? count = TryParseExplicitCount(lower);
        var delta = 0;
        if (count is null)
            delta = InferQuestionDelta(lower);
        count ??= TryParseChoToiCount(lower);
        if (count is int c)
            count = Math.Clamp(c, 5, 50);

        var minutes = TryParseMinutes(lower);
        var difficulty = TryParseDifficulty(lower);
        var types = TryParseQuestionTypes(lower);
        var technicalOnly = IsTechnicalOnly(lower);
        if (technicalOnly && (types is null || types.Count == 0))
            types = ["technical"];

        var tone = TryParseTone(lower);
        var format = TryParseOutputFormat(lower);
        var language = TryParseLanguage(lower);
        var sample = TryParseSampleAnswers(lower);
        var rubric = TryParseScoringRubric(lower);
        var focus = ExtractFocusHints(text, lower);
        var exclusive = DetectExclusiveFocus(lower, focus, technicalOnly);

        return new StudioChatSettingsIntent(
            count,
            minutes,
            difficulty,
            types,
            tone,
            format,
            language,
            sample,
            rubric,
            focus,
            delta,
            technicalOnly,
            exclusive);
    }

    /// <summary>
    /// SCRUM-389: settings-only → patch local (không RAG).
    /// RAG khi exclusive / có topic focus / đổi coverage-section theo tài liệu.
    /// </summary>
    public static bool CanUseLocalSettingsPatch(StudioChatSettingsIntent intent, string instruction)
    {
        if (intent.ExclusiveFocus)
            return false;
        if (HasMeaningfulTopicFocus(intent.FocusHints))
            return false;
        if (HasTopicChangeKeywords(instruction))
            return false;

        return intent.NumberOfQuestions is not null
               || intent.InterviewLengthMinutes is not null
               || intent.Difficulty is not null
               || intent.QuestionTypes is { Count: > 0 }
               || intent.TechnicalOnly
               || intent.QuestionDelta != 0
               || intent.QuestionTone is not null
               || intent.OutputFormat is not null
               || intent.Language is not null
               || intent.IncludeSampleAnswers is not null
               || intent.IncludeScoringRubric is not null;
    }

    public static bool NeedsRagRefine(StudioChatSettingsIntent intent, string instruction)
        => !CanUseLocalSettingsPatch(intent, instruction);

    public static ParsedRefine Parse(string instruction)
    {
        var intent = ParseIntent(instruction);
        return new ParsedRefine(
            intent.NumberOfQuestions,
            intent.Difficulty,
            intent.TechnicalOnly,
            intent.QuestionDelta);
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

    public static int ResolveNumberOfQuestions(int baseline, StudioChatSettingsIntent intent)
        => ResolveNumberOfQuestions(baseline, new ParsedRefine(intent.NumberOfQuestions, intent.Difficulty, intent.TechnicalOnly, intent.QuestionDelta));

    public static List<string> ResolveQuestionTypes(ParsedRefine parsed, IReadOnlyList<string>? baselineTypes = null)
    {
        if (parsed.TechnicalOnly)
            return ["technical", "problem_solving"];
        var fromSettings = StudioQuestionTypesHelper.Normalize(baselineTypes);
        return fromSettings.Count > 0 ? fromSettings : StudioQuestionTypesHelper.DefaultTypes.ToList();
    }

    public static List<string> ResolveQuestionTypes(StudioChatSettingsIntent intent, IReadOnlyList<string>? baselineTypes = null)
    {
        if (intent.QuestionTypes is { Count: > 0 } explicitTypes)
            return StudioQuestionTypesHelper.Normalize(explicitTypes);
        return ResolveQuestionTypes(
            new ParsedRefine(intent.NumberOfQuestions, intent.Difficulty, intent.TechnicalOnly, intent.QuestionDelta),
            baselineTypes);
    }

    /// <summary>Ưu tiên phút user nêu; không thì plan RAG; cuối cùng số_câu * 3.</summary>
    public static int ResolveInterviewMinutes(
        StudioChatSettingsIntent intent,
        int mappedPlanMinutes,
        int numberOfQuestions,
        int? previousMinutes)
    {
        if (intent.InterviewLengthMinutes is int m)
            return Math.Clamp(m, 15, 180);
        if (mappedPlanMinutes is >= 15 and <= 180)
            return mappedPlanMinutes;
        if (previousMinutes is > 0)
        {
            // Scale nhẹ khi đổi số câu so với baseline ẩn trong previous
            return Math.Clamp(previousMinutes.Value, 15, 180);
        }
        return Math.Clamp(numberOfQuestions * 3, 20, 180);
    }

    private static int? TryParseMinutes(string lower)
    {
        // "60 phút", "45 min", "1 giờ", "1.5 hours"
        var m = Regex.Match(lower, @"(\d+)\s*(?:phút|phut|minutes?|mins?)\b");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var mins))
            return Math.Clamp(mins, 15, 180);

        m = Regex.Match(lower, @"(\d+(?:[.,]\d+)?)\s*(?:giờ|gio|hours?|hrs?)\b");
        if (m.Success && double.TryParse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var hours))
            return Math.Clamp((int)Math.Round(hours * 60), 15, 180);

        return null;
    }

    private static QuestionDifficulty? TryParseDifficulty(string lower)
    {
        if (ContainsAny(lower, "toàn bộ là khó", "toan bo la kho", "all hard", "all difficult", "toàn khó", "toan kho")
            || (ContainsAny(lower, "harder", "khó hơn", "kho hon") && !ContainsAny(lower, "easier", "dễ hơn")))
            return QuestionDifficulty.Hard;
        if (ContainsAny(lower, " easier", "dễ hơn", "de hon", "all easy", "toàn dễ"))
            return QuestionDifficulty.Easy;
        if (ContainsAny(lower, "medium", "trung bình", "trung binh"))
            return QuestionDifficulty.Medium;
        if (Regex.IsMatch(lower, @"\b(hard|khó|kho)\b") && !ContainsAny(lower, "harder"))
            return QuestionDifficulty.Hard;
        if (Regex.IsMatch(lower, @"\b(easy|dễ|de)\b"))
            return QuestionDifficulty.Easy;
        return null;
    }

    private static bool IsTechnicalOnly(string lower)
        => ContainsAny(lower,
            "technical only", "chỉ technical", "chi technical",
            "toàn về kỹ thuật", "toan ve ky thuat", "toàn về kĩ thuật", "toan ve ki thuat",
            "toàn hỏi về kỹ thuật", "toan hoi ve ky thuat", "toàn hỏi kỹ thuật", "toan hoi ky thuat",
            "chỉ kỹ thuật", "chi ky thuat", "chỉ kĩ thuật", "chi ki thuat",
            "phần kỹ thuật", "phan ky thuat", "phần kĩ thuật", "phan ki thuat",
            "kỹ thuật thôi", "ky thuat thoi", "kĩ thuật thôi", "ki thuat thoi",
            "code thôi", "code thoi", "chỉ code", "chi code",
            "toàn về tech", "toan ve tech", "chỉ tech", "chi tech",
            "toàn technical", "toan technical", "all technical");

    private static List<string>? TryParseQuestionTypes(string lower)
    {
        var found = new List<string>();
        void Add(string t)
        {
            if (!found.Contains(t, StringComparer.Ordinal))
                found.Add(t);
        }

        if (ContainsAny(lower, "system design", "system_design", "system-design", "thiết kế hệ thống", "thiet ke he thong"))
            Add("system_design");
        if (ContainsAny(lower, "problem solving", "problem_solving", "problem-solving", "giải quyết vấn đề", "giai quyet van de"))
            Add("problem_solving");
        if (ContainsAny(lower, "behavioral", "hành vi", "hanh vi", "soft skill"))
            Add("behavioral");
        if (ContainsAny(lower, "situational", "tình huống", "tinh huong"))
            Add("situational");
        if (ContainsAny(lower, "technical", "kỹ thuật", "ky thuat", "kĩ thuật", "ki thuat")
            || IsTechnicalOnly(lower))
            Add("technical");

        // "chỉ X" / "only X" — nếu chỉ có 1 loại rõ ràng
        if (found.Count == 0) return null;

        if (IsTechnicalOnly(lower))
            return ["technical"];

        // Chỉ behavioral / chỉ system design
        if (ContainsAny(lower, "chỉ behavioral", "chi behavioral", "only behavioral", "toàn behavioral")
            && found.Contains("behavioral"))
            return ["behavioral"];
        if (ContainsAny(lower, "chỉ system design", "chi system design", "only system design")
            && found.Contains("system_design"))
            return ["system_design"];

        return found;
    }

    private static string? TryParseTone(string lower)
    {
        if (ContainsAny(lower, "friendly", "thân thiện", "than thien", "casual"))
            return "Friendly";
        if (ContainsAny(lower, "strict", "nghiêm khắc", "nghiem khac", "formal harsh"))
            return "Strict";
        if (ContainsAny(lower, "professional", "chuyên nghiệp", "chuyen nghiep", "văn phong chuyên nghiệp"))
            return "Professional";
        if (ContainsAny(lower, "văn phong", "van phong", "tone", "question tone"))
            return "Professional";
        return null;
    }

    private static string? TryParseOutputFormat(string lower)
    {
        if (ContainsAny(lower, "markdown"))
            return "Markdown";
        if (ContainsAny(lower, "structured interview kit", "structuredinterviewkit", "interview kit"))
            return "StructuredInterviewKit";
        if (ContainsAny(lower, "json format", "output json"))
            return "Json";
        if (ContainsAny(lower, "định dạng", "dinh dang", "output format", "format output"))
            return "StructuredInterviewKit";
        return null;
    }

    private static string? TryParseLanguage(string lower)
    {
        if (ContainsAny(lower, "tiếng anh", "tieng anh", "in english", "english", "output language=english", "language english"))
            return StudioOutputLanguage.English;
        if (ContainsAny(lower, "tiếng việt", "tieng viet", "vietnamese", "in vietnamese", "language vietnamese"))
            return StudioOutputLanguage.Vietnamese;
        return null;
    }

    private static bool? TryParseSampleAnswers(string lower)
    {
        if (ContainsAny(lower, "không sample", "khong sample", "no sample answer", "without sample", "tắt sample", "tat sample", "exclude sample"))
            return false;
        if (ContainsAny(lower, "có sample", "co sample", "sample answer", "include sample", "kèm đáp án mẫu", "kem dap an mau"))
            return true;
        return null;
    }

    private static bool? TryParseScoringRubric(string lower)
    {
        if (ContainsAny(lower, "không rubric", "khong rubric", "no rubric", "without rubric", "tắt rubric", "tat rubric", "exclude rubric"))
            return false;
        if (ContainsAny(lower, "có rubric", "co rubric", "scoring rubric", "include rubric", "kèm rubric", "kem rubric"))
            return true;
        return null;
    }

    private static List<string> ExtractFocusHints(string original, string lower)
    {
        var hints = new List<string>();
        // Token kỹ thuật thường gặp sau "về / theo / focus / về kỹ thuật"
        var m = Regex.Matches(original,
            @"(?:về|ve|theo|focus|với|voi|on)\s+([A-Za-z][A-Za-z0-9.+#/-]{1,40})",
            RegexOptions.IgnoreCase);
        foreach (Match match in m)
        {
            var token = match.Groups[1].Value.Trim().TrimEnd('.', ',', ';');
            var tLower = token.ToLowerInvariant();
            if (tLower is "kỹ" or "ky" or "kĩ" or "ki" or "câu" or "cau" or "hỏi" or "hoi"
                or "technical" or "tech" or "thuật" or "thuat")
                continue;
            if (token.Length >= 2 && !hints.Contains(token, StringComparer.OrdinalIgnoreCase))
                hints.Add(token);
        }

        // Từ khóa skill đơn lẻ phổ biến nếu xuất hiện như noun
        foreach (var skill in new[] { "git", "docker", "kubernetes", "k8s", "redis", "kafka", "graphql", "grpc",
                     "ef core", "entity framework", "asp.net", "react", "next.js", "typescript", "postgresql", "postgres" })
        {
            if (lower.Contains(skill, StringComparison.Ordinal) &&
                !hints.Exists(h => h.Equals(skill, StringComparison.OrdinalIgnoreCase)))
                hints.Add(skill);
        }

        return hints.Take(8).ToList();
    }

    private static int? TryParseExplicitCount(string lower)
    {
        var m = Regex.Match(lower, @"(\d+)\s*(câu\s*hỏi|cau\s*hoi|câu|cau|questions?|qs)\b");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n1))
            return n1;

        m = Regex.Match(lower, @"(?:tầm|tam|khoảng|khoang|about|around|~)\s*(\d+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n2))
            return n2;

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

    private static bool DetectExclusiveFocus(string lower, IReadOnlyList<string> focusHints, bool technicalOnly)
    {
        if (!HasExclusiveMarker(lower))
            return false;
        // "chỉ technical" = đổi loại câu, không phải exclusive topic Git
        if (technicalOnly && !HasMeaningfulTopicFocus(focusHints))
            return false;
        if (ContainsAny(lower, "chỉ behavioral", "chi behavioral", "only behavioral",
                "chỉ system design", "chi system design", "only system design")
            && !HasMeaningfulTopicFocus(focusHints))
            return false;
        return HasMeaningfulTopicFocus(focusHints)
               || ContainsAny(lower, "syntax", "thao tác", "thao tac", "theo tài liệu", "theo tai lieu");
    }

    private static bool HasExclusiveMarker(string lower)
        => ContainsAny(lower,
            "chỉ ", "chi ", " only", "only ",
            "cụ thể", "cu the", "cụ the",
            "toàn về", "toan ve", "toàn hỏi", "toan hoi",
            "riêng ", "rieng ", "duy nhất", "duy nhat",
            "không hỏi gì khác", "khong hoi gi khac");

    private static bool HasMeaningfulTopicFocus(IReadOnlyList<string> focusHints)
    {
        if (focusHints is null || focusHints.Count == 0) return false;
        foreach (var h in focusHints)
        {
            var t = h.Trim().ToLowerInvariant();
            if (t is "technical" or "tech" or "behavioral" or "situational"
                or "system_design" or "system-design" or "problem_solving" or "problem-solving")
                continue;
            if (t.Length >= 2) return true;
        }
        return false;
    }

    private static bool HasTopicChangeKeywords(string instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return false;
        var lower = instruction.ToLowerInvariant();
        return ContainsAny(lower,
            "theo tài liệu", "theo tai lieu", "tài liệu đã", "tai lieu da",
            "coverage", "focus area", "thêm section", "them section", "bỏ section", "bo section",
            "outline", "trọng tâm", "trong tam", "skill ", "kỹ năng", "ky nang",
            "rag", "selected", "upload", "nguồn", "nguon");
    }

    private static bool ContainsAny(string haystack, params string[] needles)
        => needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));
}
