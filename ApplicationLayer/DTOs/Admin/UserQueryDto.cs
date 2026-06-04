namespace ApplicationLayer.DTOs.Admin;

/// <summary>SCRUM-163 AC-01/02: Query params cho danh sách user.</summary>
public class UserQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    /// <summary>Tìm theo tên hoặc email.</summary>
    public string? Search { get; set; }

    /// <summary>Lọc theo vai trò: Admin | HRManager | JobSeeker.</summary>
    public string? Role { get; set; }

    /// <summary>Lọc theo trạng thái. null = tất cả.</summary>
    public bool? IsActive { get; set; }
}
