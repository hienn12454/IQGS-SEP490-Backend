namespace DomainLayer.Constants;

public static class UserRole
{
    // ── Role names (dùng trong JWT claim, display) ─────
    public const string Admin = "Admin";
    public const string HR = "HR";
    public const string Candidate = "Candidate";

    // ── Role IDs trong bảng Roles (seed cố định) ──────
    public const int AdminId = 1;
    public const int HRId = 2;
    public const int CandidateId = 3;

    /// <summary>Map RoleId → tên role.</summary>
    public static string GetNameById(int roleId) => roleId switch
    {
        AdminId => Admin,
        HRId => HR,
        CandidateId => Candidate,
        _ => string.Empty
    };

    /// <summary>Map tên role → RoleId.</summary>
    public static int GetIdByName(string name) => name switch
    {
        Admin => AdminId,
        HR => HRId,
        Candidate => CandidateId,
        _ => CandidateId // default
    };
}
