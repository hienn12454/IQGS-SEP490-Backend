using System.Security;

namespace ApplicationLayer.Services.Mapping;

/// <summary>
/// Đảm bảo company nào cũng có logo hiển thị cho Candidate (marketplace, bookmark, invitation):
/// 1. Company đã upload LogoUrl → dùng luôn.
/// 2. Chưa có logo nhưng có website → lấy logo thật của công ty qua Google favicon service theo domain.
/// 3. Không có cả website → tự vẽ logo SVG (data URI) từ chữ cái đầu tên công ty,
///    màu nền deterministic theo tên nên mỗi công ty luôn giữ đúng 1 màu — không phụ thuộc dịch vụ ngoài.
/// </summary>
public static class CompanyLogoResolver
{
    private static readonly string[] Palette =
    {
        "#4F46E5", "#0891B2", "#059669", "#D97706", "#DC2626",
        "#7C3AED", "#DB2777", "#2563EB", "#65A30D", "#EA580C"
    };

    public static string Resolve(string? logoUrl, string? websiteUrl, string companyName)
    {
        if (!string.IsNullOrWhiteSpace(logoUrl))
            return logoUrl;

        var domain = ExtractDomain(websiteUrl);
        if (domain is not null)
            return $"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(domain)}&sz=128";

        return BuildInitialsSvgDataUri(companyName);
    }

    private static string? ExtractDomain(string? websiteUrl)
    {
        if (string.IsNullOrWhiteSpace(websiteUrl))
            return null;

        var raw = websiteUrl.Trim();
        if (!raw.Contains("://"))
            raw = "https://" + raw;

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            return null;

        var host = uri.Host;
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    private static string BuildInitialsSvgDataUri(string companyName)
    {
        var initials = SecurityElement.Escape(GetInitials(companyName));
        var color = PickColor(companyName);

        var svg =
            "<svg xmlns='http://www.w3.org/2000/svg' width='128' height='128' viewBox='0 0 128 128'>" +
            $"<rect width='128' height='128' rx='24' fill='{color}'/>" +
            "<text x='64' y='64' dy='.36em' text-anchor='middle' " +
            "font-family='Segoe UI, Roboto, Helvetica, Arial, sans-serif' " +
            $"font-size='52' font-weight='700' fill='#FFFFFF'>{initials}</text>" +
            "</svg>";

        return "data:image/svg+xml;utf8," + Uri.EscapeDataString(svg);
    }

    /// <summary>2 chữ cái đầu của 2 từ đầu tên công ty ("FPT Software" → "FS"), tên 1 từ lấy 2 ký tự đầu ("Meta" → "ME").</summary>
    private static string GetInitials(string companyName)
    {
        var words = companyName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
            return "?";

        var initials = words.Length == 1
            ? words[0][..Math.Min(2, words[0].Length)]
            : $"{words[0][0]}{words[1][0]}";

        return initials.ToUpperInvariant();
    }

    /// <summary>Hash cộng dồn tự viết thay vì string.GetHashCode() — GetHashCode bị randomize mỗi lần chạy app, màu sẽ nhảy lung tung.</summary>
    private static string PickColor(string companyName)
    {
        var hash = 0;
        foreach (var c in companyName)
            hash = (hash * 31 + c) & 0x7FFFFFFF;

        return Palette[hash % Palette.Length];
    }
}
