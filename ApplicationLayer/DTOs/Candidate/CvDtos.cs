namespace ApplicationLayer.DTOs.Candidate;

/// <summary>Response chung cho POST (upload) và GET (trạng thái) CV — cùng schema theo SCRUM-300.</summary>
public class CvEvaluationResponseDto
{
    public string CvFileName { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public string? Summary { get; set; }
    public List<string> TechStack { get; set; } = new();
    public DateTime? ParsedAt { get; set; }
    public DateTime? UploadedAt { get; set; }
    public string? DownloadUrl { get; set; }

    /// <summary>Trạng thái cài đặt tự đồng bộ thông tin cá nhân từ CV vào profile tại thời điểm này.</summary>
    public bool AutoSyncProfileFromCv { get; set; }

    /// <summary>
    /// Tên các field profile vừa được cập nhật từ CV ở lần upload này (vd "FullName", "PhoneNumber").
    /// Rỗng nếu tắt AutoSyncProfileFromCv, hoặc CV không trích xuất được field cá nhân nào, hoặc đây là response của GET.
    /// </summary>
    public List<string> ProfileFieldsSynced { get; set; } = new();
}

/// <summary>Cài đặt bật/tắt tự động đồng bộ thông tin cá nhân (họ tên, SĐT, địa chỉ, GitHub, LinkedIn) từ CV vào profile.</summary>
public class CvSyncSettingsDto
{
    /// <summary>Mặc định true — tắt để giữ nguyên profile tự chỉnh tay qua các lần upload CV sau.</summary>
    public bool AutoSyncProfileFromCv { get; set; } = true;
}
