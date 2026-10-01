using System.Text;
using System.Text.RegularExpressions;

namespace ApplicationLayer.Helpers;

/// <summary>
/// SCRUM-491/492/493: format hybrid + chặn non-IT + sanitize skill từ CV.
/// Free-text IT OK nếu đúng charset; catalog chỉ dùng gợi ý FE.
/// </summary>
public static class CoachSkillFormat
{
    public const int MinLen = 2;
    public const int MaxLen = 40;

    public const string NonItReject =
        "Chỉ thêm công nghệ / kỹ năng IT. Marketing, sales, kế toán… không được hỗ trợ.";

    public const string CharsetReject =
        "Chỉ dùng chữ, số và các ký tự . # + / - ( ) (không emoji / ký tự đặc biệt).";

    /// <summary>Charset sau sanitize — gồm ngoặc cho acronym (SDUI).</summary>
    private static readonly Regex AllowedChars = new(
        @"^[A-Za-z0-9 .#+/\-()]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MultiSpace = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Exact match sau normalize — tránh "sales" khớp "Salesforce".</summary>
    private static readonly string[] NonItExact =
    [
        "marketing", "marketting", "sales", "seo", "finance", "accounting", "accountant",
        "bookkeeper", "auditor", "lawyer", "teacher", "nurse", "cashier", "receptionist",
        "communication", "leadership", "teamwork", "collaboration", "excel", "hr",
        "recruiting", "recruitment",
    ];

    private static readonly string[] NonItPhrases =
    [
        "digitalmarketing", "contentmarketing", "socialmedia", "seospecialist",
        "salesexecutive", "salesmanager", "accountexecutive", "businessdevelopment",
        "ketoan", "kiemtoan", "luatsu", "legalcounsel", "phapche",
        "giaovien", "giangvien", "nhanvienyte", "bacsi", "dieuduong",
        "nhahang", "restaurant", "phache", "bartender", "khachsan", "hotelreceptionist",
        "batdongsan", "realestate", "moigioi", "nhanvienbanhang", "khovan", "warehousepicker",
        "fashiondesigner", "thietkethoitang", "makeupartist",
    ];

    /// <summary>
    /// SCRUM-493: chuẩn hóa skill từ CV/LLM trước khi persist.
    /// null = bỏ (rỗng / non-IT / không còn chữ sau strip).
    /// </summary>
    public static string? Sanitize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var s = raw.Trim();
        // En/em/minus dash → hyphen ASCII
        s = s.Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('\u2212', '-')
            .Replace('\u00AD', '-');
        s = MultiSpace.Replace(s, " ");

        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c)
                || c is ' ' or '.' or '#' or '+' or '/' or '-' or '(' or ')')
            {
                sb.Append(c);
            }
        }

        s = MultiSpace.Replace(sb.ToString(), " ").Trim();
        s = Regex.Replace(s, @"\(\s*\)", "").Trim();
        s = MultiSpace.Replace(s, " ").Trim();

        if (s.Length > MaxLen)
            s = s[..MaxLen].TrimEnd();

        if (s.Length < MinLen) return null;
        if (!s.Any(char.IsLetter)) return null;
        if (IsNonIt(s)) return null;
        if (!AllowedChars.IsMatch(s)) return null;
        return s;
    }

    /// <summary>Sanitize + dedupe case-insensitive (giữ casing đầu).</summary>
    public static List<string> SanitizeList(IEnumerable<string>? skills)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var raw in skills ?? Enumerable.Empty<string>())
        {
            var s = Sanitize(raw);
            if (s is null) continue;
            if (!seen.Add(s)) continue;
            list.Add(s);
        }
        return list;
    }

    /// <summary>null = hợp lệ; ngược lại message lỗi tiếng Việt.</summary>
    public static string? Validate(string? raw)
    {
        // Validate trên bản đã sanitize — tránh reject skill CV chỉ vì en-dash.
        var sanitized = Sanitize(raw);
        if (sanitized is null)
        {
            var trimmed = (raw ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return "Tên công nghệ không được để trống.";
            if (IsNonIt(trimmed) || IsNonIt(SanitizeKeepNonItCheck(trimmed)))
                return NonItReject;
            if (trimmed.Length < MinLen)
                return $"Tên công nghệ cần ít nhất {MinLen} ký tự.";
            return CharsetReject;
        }

        // raw khác sanitized vẫn OK nếu Sanitize ra giá trị hợp lệ (UpdateSkills sẽ lưu bản sanitize).
        return null;
    }

    public static bool IsValid(string? raw) => Validate(raw) is null;

    public static bool IsNonIt(string? raw)
    {
        var key = NormalizeKey(raw);
        if (key.Length == 0) return false;
        if (key.Contains("marketing", StringComparison.Ordinal)
            || key.Contains("marketting", StringComparison.Ordinal))
            return true;
        foreach (var needle in NonItExact)
        {
            if (key == NormalizeKey(needle))
                return true;
        }
        foreach (var phrase in NonItPhrases)
        {
            if (key.Contains(phrase, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Where(c => c is not (' ' or '_' or '-'))
            .ToArray();
        return new string(chars);
    }

    /// <summary>Giữ text sau strip charset để IsNonIt khi Sanitize trả null vì non-IT.</summary>
    private static string SanitizeKeepNonItCheck(string raw)
    {
        var s = raw.Trim()
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('\u2212', '-');
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) || c is ' ' or '.' or '#' or '+' or '/' or '-' or '(' or ')')
                sb.Append(c);
        }
        return MultiSpace.Replace(sb.ToString(), " ").Trim();
    }
}
