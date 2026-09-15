namespace ApplicationLayer.Helpers;

/// <summary>
/// Lọc HrNote trước khi hiện mô tả Marketplace — marker nội bộ Studio không được lộ ra UI.
/// </summary>
public static class MarketplaceDescriptionHelper
{
    public const int MaxLength = 2000;

    /// <summary>Prefix marker Save từ Studio — không dùng làm mô tả public.</summary>
    public const string StudioSavePrefix = "STUDIO_SAVE";

    /// <summary>Prefix marker job mirror từ Studio History — không dùng làm mô tả public.</summary>
    public const string StudioMirrorPrefix = "STUDIO_MIRROR";

    /// <summary>True nếu HrNote là marker kỹ thuật (STUDIO_SAVE / STUDIO_MIRROR), không phải mô tả người viết.</summary>
    public static bool IsInternalMarker(string? hrNote)
    {
        if (string.IsNullOrWhiteSpace(hrNote))
            return false;

        var trimmed = hrNote.TrimStart();
        return trimmed.StartsWith(StudioSavePrefix, StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith(StudioMirrorPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Chuẩn hóa mô tả public: trim, cắt max length; marker/rỗng → null.</summary>
    public static string? ToPublic(string? hrNote)
    {
        if (string.IsNullOrWhiteSpace(hrNote) || IsInternalMarker(hrNote))
            return null;

        var trimmed = hrNote.Trim();
        return trimmed.Length <= MaxLength ? trimmed : trimmed[..MaxLength];
    }

    /// <summary>
    /// Khi Studio Save/Publish: ưu tiên mô tả project; không có thì giữ HrNote cũ nếu đó là mô tả người
    /// (không phải marker). Không bao giờ ghi STUDIO_SAVE vào HrNote.
    /// </summary>
    public static string? ResolveForStudioSave(string? projectDescription, string? existingHrNote)
    {
        var fromProject = ToPublic(projectDescription);
        if (fromProject is not null)
            return fromProject;

        return ToPublic(existingHrNote);
    }
}
