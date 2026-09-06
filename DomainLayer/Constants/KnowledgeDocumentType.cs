namespace DomainLayer.Constants;

/// <summary>
/// SCRUM-442: Loại tài liệu Knowledge — lưu vào cột section (reuse, không migration mới).
/// HR bắt buộc chọn khi upload; Admin tùy chọn.
/// </summary>
public static class KnowledgeDocumentType
{
    public const string Policy = "Policy";
    public const string InternalStack = "InternalStack";
    public const string Rubric = "Rubric";
    public const string RolePack = "RolePack";
    /// <summary>Doc cũ / Admin không gắn loại.</summary>
    public const string Unclassified = "Unclassified";

    public static readonly IReadOnlyList<string> All =
    [
        Policy,
        InternalStack,
        Rubric,
        RolePack,
        Unclassified
    ];

    /// <summary>Loại HR được chọn lúc upload (không gồm Unclassified).</summary>
    public static readonly IReadOnlyList<string> HrUploadTypes =
    [
        Policy,
        InternalStack,
        Rubric,
        RolePack
    ];

    public static bool IsHrUploadType(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && HrUploadTypes.Any(t => string.Equals(t, value.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsKnownType(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && All.Any(t => string.Equals(t, value.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Chuẩn hóa section DB → documentType API (null/empty → Unclassified).</summary>
    public static string FromSection(string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
            return Unclassified;

        var trimmed = section.Trim();
        foreach (var t in All)
        {
            if (string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return Unclassified;
    }

    /// <summary>Chuẩn hóa input upload/PATCH → giá trị lưu Section.</summary>
    public static string NormalizeForStorage(string? value, bool requireHrType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (requireHrType)
                throw new DomainLayer.Exceptions.BadRequestException(
                    "HR bắt buộc chọn loại tài liệu: Policy, InternalStack, Rubric hoặc RolePack.");
            return Unclassified;
        }

        var trimmed = value.Trim();
        foreach (var t in All)
        {
            if (string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                if (requireHrType && string.Equals(t, Unclassified, StringComparison.OrdinalIgnoreCase))
                    throw new DomainLayer.Exceptions.BadRequestException(
                        "HR bắt buộc chọn loại tài liệu: Policy, InternalStack, Rubric hoặc RolePack.");
                return t;
            }
        }

        throw new DomainLayer.Exceptions.BadRequestException(
            $"Loại tài liệu không hợp lệ: '{value}'. Cho phép: {string.Join(", ", requireHrType ? HrUploadTypes : All)}.");
    }
}
