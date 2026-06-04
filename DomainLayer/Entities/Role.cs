namespace DomainLayer.Entities;

/// <summary>
/// Lookup table cho RBAC — Admin | HR | Candidate.
/// Không kế thừa BaseEntity vì là seed data cố định, không cần audit fields.
/// </summary>
public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
}
