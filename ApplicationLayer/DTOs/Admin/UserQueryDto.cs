namespace ApplicationLayer.DTOs.Admin;

/// <summary>SCRUM-163 AC-01/02: Query params cho danh sách user.</summary>
public class UserQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    /// <summary>Tìm theo tên hoặc email (cột họ tên).</summary>
    public string? Search { get; set; }

    /// <summary>Lọc theo vai trò: Admin | HR | Candidate.</summary>
    public string? Role { get; set; }

    /// <summary>Lọc theo trạng thái. null = tất cả.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Lọc email verified (Pending = false + active).</summary>
    public bool? IsEmailVerified { get; set; }

    /// <summary>Lọc gói: Premium | Free (null = tất cả). Admin không có gói được bỏ qua khi Premium.</summary>
    public string? Plan { get; set; }

    /// <summary>Ngày đăng ký từ (UTC date inclusive).</summary>
    public DateTime? CreatedFrom { get; set; }

    /// <summary>Ngày đăng ký đến (UTC date inclusive end-of-day khi chỉ có date).</summary>
    public DateTime? CreatedTo { get; set; }
}
