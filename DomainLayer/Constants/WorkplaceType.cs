namespace DomainLayer.Constants;

/// <summary>SCRUM-468: hình thức làm việc trên tin tuyển (bộ Tuyển).</summary>
public static class WorkplaceType
{
    public const string AtOffice = "AtOffice";
    public const string Hybrid = "Hybrid";
    public const string Remote = "Remote";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        AtOffice, Hybrid, Remote
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        foreach (var known in All)
        {
            if (string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase))
                return known;
        }
        return null;
    }
}
