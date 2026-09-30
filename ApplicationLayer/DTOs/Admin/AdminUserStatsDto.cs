namespace ApplicationLayer.DTOs.Admin;

/// <summary>SCRUM-480: thống kê user toàn hệ thống theo role + status (Admin).</summary>
public class AdminUserStatsDto
{
    public int TotalUsers { get; set; }

    public AdminUserStatsByRoleDto ByRole { get; set; } = new();

    public AdminUserStatsByStatusDto ByStatus { get; set; } = new();
}

public class AdminUserStatsByRoleDto
{
    public int Admin { get; set; }
    public int HR { get; set; }
    public int Candidate { get; set; }
}

/// <summary>
/// Active = IsActive &amp;&amp; IsEmailVerified;
/// Pending = IsActive &amp;&amp; !IsEmailVerified;
/// Suspended = !IsActive.
/// </summary>
public class AdminUserStatsByStatusDto
{
    public int Active { get; set; }
    public int Pending { get; set; }
    public int Suspended { get; set; }
}
