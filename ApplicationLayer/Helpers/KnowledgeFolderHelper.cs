using System.Text.RegularExpressions;

namespace ApplicationLayer.Helpers;

/// <summary>SCRUM-450: sanitize tên folder Knowledge (UI grouping, không đổi Blob path).</summary>
public static class KnowledgeFolderHelper
{
    public const string UnsortedKey = "unsorted";
    private static readonly Regex Valid = new(@"^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.Compiled);

    /// <summary>
    /// Chuẩn hóa input → folder lưu DB. Null/empty → null (unsorted).
    /// Chỉ a-z, 0-9, _, -; lower-case; max 64.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var s = input.Trim().ToLowerInvariant().Replace(' ', '-');
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '-');

        s = Regex.Replace(s, @"[^a-z0-9_-]+", "-");
        s = Regex.Replace(s, @"-+", "-").Trim('-', '_');
        if (s.Length > 64)
            s = s[..64].TrimEnd('-', '_');

        if (string.IsNullOrEmpty(s) || s == UnsortedKey)
            return null;

        if (!Valid.IsMatch(s))
            throw new DomainLayer.Exceptions.BadRequestException(
                "Folder không hợp lệ. Dùng chữ thường, số, gạch ngang/dưới (vd. swe, dotnet-junior).");

        return s;
    }

    public static string DisplayName(string? folder)
        => string.IsNullOrWhiteSpace(folder) ? UnsortedKey : folder;
}
