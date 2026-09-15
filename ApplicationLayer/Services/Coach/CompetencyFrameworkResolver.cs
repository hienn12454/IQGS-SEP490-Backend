using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-453: chọn competency framework từ target role tự do của ứng viên bằng DATA
/// (RoleKey + bảng alias), không có bất kỳ nhánh if theo stack cụ thể.
/// Không match được -> trả null; tuyệt đối không fallback sang framework role khác
/// (React Frontend không được chấm bằng framework .NET).
/// </summary>
public interface ICompetencyFrameworkResolver
{
    Task<FrameworkResolution> ResolveAsync(string? targetRole, string? targetLevel);
    Task<List<FrameworkCatalogEntry>> GetCatalogAsync();
}

/// <param name="Framework">null = chưa hỗ trợ role này.</param>
/// <param name="MatchedRoleKey">RoleKey đã resolve (kể cả khi thiếu level).</param>
/// <param name="RequestedLevel">Level ứng viên nhắm tới.</param>
/// <param name="UsedLevelFallback">true khi role có framework nhưng không có đúng level yêu cầu.</param>
public sealed record FrameworkResolution(
    CompetencyFramework? Framework,
    string? MatchedRoleKey,
    string RequestedLevel,
    bool UsedLevelFallback)
{
    public bool Matched => Framework is not null;
}

public sealed record FrameworkCatalogEntry(
    string RoleKey,
    string DisplayRole,
    string? Technology,
    List<string> Levels,
    string Provenance);

public class CompetencyFrameworkResolver : ICompetencyFrameworkResolver
{
    private readonly ICompetencyFrameworkRepository _frameworks;

    public CompetencyFrameworkResolver(ICompetencyFrameworkRepository frameworks)
        => _frameworks = frameworks;

    public async Task<FrameworkResolution> ResolveAsync(string? targetRole, string? targetLevel)
    {
        var level = CoachSeniorityLevel.IsValid(targetLevel)
            ? targetLevel!.Trim()
            : CoachSeniorityLevel.Junior;

        var all = await _frameworks.ListActiveWithSkillsAsync();
        if (all.Count == 0)
            return new FrameworkResolution(null, null, level, false);

        var aliases = await _frameworks.ListAliasesAsync();
        var roleKey = MatchRoleKey(targetRole, all, aliases);
        if (roleKey is null)
            return new FrameworkResolution(null, null, level, false);

        var sameRole = all
            .Where(f => string.Equals(f.RoleKey, roleKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var exact = sameRole.FirstOrDefault(f =>
            string.Equals(f.TargetLevel, level, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return new FrameworkResolution(exact, roleKey, level, false);

        // Role có framework nhưng thiếu level yêu cầu: chọn level gần nhất, ưu tiên level thấp hơn
        // (đo ở thang thấp hơn an toàn hơn là đo ở thang cao hơn năng lực thực tế).
        var nearest = sameRole
            .OrderBy(f => Math.Abs(LevelIndex(f.TargetLevel) - LevelIndex(level)))
            .ThenBy(f => LevelIndex(f.TargetLevel))
            .FirstOrDefault();

        return nearest is null
            ? new FrameworkResolution(null, roleKey, level, false)
            : new FrameworkResolution(nearest, roleKey, level, true);
    }

    public async Task<List<FrameworkCatalogEntry>> GetCatalogAsync()
    {
        var all = await _frameworks.ListActiveWithSkillsAsync();
        return all
            .GroupBy(f => f.RoleKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => new FrameworkCatalogEntry(
                g.Key,
                g.First().DisplayRole,
                g.First().Technology,
                g.Select(f => f.TargetLevel)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(LevelIndex)
                    .ToList(),
                g.Any(f => f.Provenance == CompetencyFrameworkProvenance.Curated)
                    ? CompetencyFrameworkProvenance.Curated
                    : CompetencyFrameworkProvenance.Provisional))
            .OrderBy(e => e.DisplayRole)
            .ToList();
    }

    /// <summary>
    /// Thứ tự match (deterministic): RoleKey exact -> alias exact -> alias contains (alias dài nhất thắng).
    /// Alias là data nên thêm cách gọi mới của một role chỉ là thêm row.
    /// </summary>
    public static string? MatchRoleKey(
        string? targetRole,
        IReadOnlyList<CompetencyFramework> frameworks,
        IReadOnlyList<CompetencyRoleAlias> aliases)
    {
        var role = Normalize(targetRole);
        if (role.Length == 0) return null;

        var roleKeys = frameworks
            .Select(f => f.RoleKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var key in roleKeys)
            if (Normalize(key) == role)
                return key;

        bool HasFramework(string roleKey)
            => roleKeys.Any(k => string.Equals(k, roleKey, StringComparison.OrdinalIgnoreCase));

        var exactAlias = aliases
            .Where(a => a.MatchKind == CompetencyRoleAliasMatchKind.Exact
                        && Normalize(a.Alias) == role
                        && HasFramework(a.RoleKey))
            .OrderBy(a => a.SortOrder)
            .FirstOrDefault();
        if (exactAlias is not null) return exactAlias.RoleKey;

        var containsAlias = aliases
            .Where(a => a.MatchKind == CompetencyRoleAliasMatchKind.Contains
                        && Normalize(a.Alias).Length > 0
                        && role.Contains(Normalize(a.Alias), StringComparison.Ordinal)
                        && HasFramework(a.RoleKey))
            // Alias dài nhất thắng: "asp.net core" ưu tiên hơn ".net" khi cả hai đều khớp.
            .OrderByDescending(a => Normalize(a.Alias).Length)
            .ThenBy(a => a.SortOrder)
            .FirstOrDefault();

        return containsAlias?.RoleKey;
    }

    /// <summary>Thứ tự level toàn cục (Fresher &lt; Junior &lt; Middle &lt; Senior) — không gắn role nào.</summary>
    public static int LevelIndex(string? level)
    {
        var normalized = (level ?? "").Trim();
        var idx = Array.FindIndex(
            CoachSeniorityLevel.All,
            l => string.Equals(l, normalized, StringComparison.OrdinalIgnoreCase));
        return idx < 0 ? 1 : idx; // không nhận dạng được -> coi như Junior
    }

    private static string Normalize(string? value)
        => (value ?? "").Trim().ToLowerInvariant();
}
