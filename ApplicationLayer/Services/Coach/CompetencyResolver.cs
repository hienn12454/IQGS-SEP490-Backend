using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

public sealed record CompetencyResolution(
    string ResolutionMode,
    string? RoleKey,
    string? FrameworkKey,
    Guid? FrameworkId,
    string NormalizedRole,
    string TargetLevel,
    string? RoleFamilyKey,
    string? RoleFamilyDisplay,
    List<string> DetectedSkills,
    double Confidence,
    string Reason,
    CompetencyFramework? Framework,
    bool UsedLevelFallback,
    List<string> SupportedRoles)
{
    public bool IsFramework => ResolutionMode == CompetencyResolutionMode.Framework;
    public bool IsAdaptive => ResolutionMode == CompetencyResolutionMode.Adaptive;
    public bool IsUnsupported => ResolutionMode == CompetencyResolutionMode.Unsupported;
}

public interface ICompetencyResolver
{
    Task<CompetencyResolution> ResolveAsync(string? targetRole, string? targetLevel, IReadOnlyList<string> cvSkills);
}

/// <summary>
/// SCRUM-457: FRAMEWORK nếu khớp catalog; ADAPTIVE nếu thuộc Role Family SE nhưng chưa có framework;
/// UNSUPPORTED nếu không thuộc domain SE (family table).
/// </summary>
public class CompetencyResolver : ICompetencyResolver
{
    private readonly ICompetencyFrameworkResolver _frameworkResolver;
    private readonly ICompetencyFrameworkRepository _frameworks;
    private readonly ICompetencyRoleFamilyRepository _families;

    public CompetencyResolver(
        ICompetencyFrameworkResolver frameworkResolver,
        ICompetencyFrameworkRepository frameworks,
        ICompetencyRoleFamilyRepository families)
    {
        _frameworkResolver = frameworkResolver;
        _frameworks = frameworks;
        _families = families;
    }

    public async Task<CompetencyResolution> ResolveAsync(
        string? targetRole, string? targetLevel, IReadOnlyList<string> cvSkills)
    {
        var level = CoachSeniorityLevel.IsValid(targetLevel)
            ? targetLevel!.Trim()
            : CoachSeniorityLevel.Junior;
        var detected = (cvSkills ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var displayRole = string.IsNullOrWhiteSpace(targetRole) ? "" : targetRole.Trim();

        var families = await _families.ListActiveAsync();
        var familyAliases = await _families.ListAliasesAsync();
        var supported = families.OrderBy(f => f.SortOrder).Select(f => f.DisplayName).ToList();

        var fwResolution = await _frameworkResolver.ResolveAsync(targetRole, level);
        var familyKey = MatchFamilyKey(targetRole, families, familyAliases);
        if (familyKey is null && !string.IsNullOrWhiteSpace(fwResolution.Framework?.RoleFamilyKey))
            familyKey = fwResolution.Framework!.RoleFamilyKey;

        var family = families.FirstOrDefault(f =>
            string.Equals(f.FamilyKey, familyKey, StringComparison.OrdinalIgnoreCase));

        if (fwResolution.Framework is not null)
        {
            var fw = fwResolution.Framework;
            return new CompetencyResolution(
                CompetencyResolutionMode.Framework,
                fw.RoleKey,
                fw.RoleKey,
                fw.Id,
                fw.DisplayRole,
                fw.TargetLevel,
                fw.RoleFamilyKey ?? familyKey,
                family?.DisplayName,
                detected,
                fwResolution.UsedLevelFallback ? 0.9 : 0.97,
                fwResolution.UsedLevelFallback
                    ? "Exact supported framework matched (nearest level)."
                    : "Exact supported framework matched.",
                fw,
                fwResolution.UsedLevelFallback,
                supported);
        }

        if (family is not null)
        {
            var overlapped = await TryMatchFrameworkBySkillOverlapAsync(family.FamilyKey, detected, level);
            if (overlapped is not null)
            {
                return new CompetencyResolution(
                    CompetencyResolutionMode.Framework,
                    overlapped.RoleKey,
                    overlapped.RoleKey,
                    overlapped.Id,
                    overlapped.DisplayRole,
                    overlapped.TargetLevel,
                    family.FamilyKey,
                    family.DisplayName,
                    detected,
                    0.9,
                    "Family matched and CV skills overlap a predefined framework.",
                    overlapped,
                    false,
                    supported);
            }

            return new CompetencyResolution(
                CompetencyResolutionMode.Adaptive,
                family.FamilyKey,
                null,
                null,
                family.DisplayName,
                level,
                family.FamilyKey,
                family.DisplayName,
                detected,
                0.88,
                "Role is supported but no exact predefined framework exists.",
                null,
                false,
                supported);
        }

        return new CompetencyResolution(
            CompetencyResolutionMode.Unsupported,
            null,
            null,
            null,
            displayRole.Length > 0 ? displayRole : "(chưa chọn)",
            level,
            null,
            null,
            detected,
            0,
            "This target role is currently outside IQGS's supported software engineering domain.",
            null,
            false,
            supported);
    }

    /// <summary>Match family từ alias table — deterministic, không nhánh theo tên family trong code.</summary>
    public static string? MatchFamilyKey(
        string? targetRole,
        IReadOnlyList<CompetencyRoleFamily> families,
        IReadOnlyList<CompetencyRoleFamilyAlias> aliases)
    {
        var role = Normalize(targetRole);
        if (role.Length == 0) return null;

        var keys = families.Select(f => f.FamilyKey).ToList();
        foreach (var key in keys)
            if (Normalize(key) == role)
                return key;

        bool Known(string familyKey)
            => keys.Any(k => string.Equals(k, familyKey, StringComparison.OrdinalIgnoreCase));

        var exact = aliases
            .Where(a => a.MatchKind == CompetencyRoleAliasMatchKind.Exact
                        && Normalize(a.Alias) == role
                        && Known(a.FamilyKey))
            .OrderBy(a => a.SortOrder)
            .FirstOrDefault();
        if (exact is not null) return exact.FamilyKey;

        var contains = aliases
            .Where(a => a.MatchKind == CompetencyRoleAliasMatchKind.Contains
                        && Normalize(a.Alias).Length > 0
                        && role.Contains(Normalize(a.Alias), StringComparison.Ordinal)
                        && Known(a.FamilyKey))
            .OrderByDescending(a => Normalize(a.Alias).Length)
            .ThenBy(a => a.SortOrder)
            .FirstOrDefault();
        return contains?.FamilyKey;
    }

    private async Task<CompetencyFramework?> TryMatchFrameworkBySkillOverlapAsync(
        string familyKey, IReadOnlyList<string> cvSkills, string level)
    {
        if (cvSkills.Count == 0) return null;
        var all = await _frameworks.ListActiveWithSkillsAsync();
        var inFamily = all
            .Where(f => string.Equals(f.RoleFamilyKey, familyKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (inFamily.Count == 0) return null;

        var cvNorm = cvSkills.Select(CompetencyScoringService.NormalizeSkill).ToHashSet();
        CompetencyFramework? best = null;
        var bestHits = 0;
        foreach (var fw in inFamily)
        {
            var hits = fw.Skills.Count(sk => cvNorm.Contains(CompetencyScoringService.NormalizeSkill(sk.Skill)));
            if (!string.IsNullOrWhiteSpace(fw.Technology))
            {
                var tech = CompetencyScoringService.NormalizeSkill(fw.Technology);
                if (cvNorm.Any(s => s.Contains(tech, StringComparison.Ordinal) || tech.Contains(s, StringComparison.Ordinal)))
                    hits += 2;
            }
            if (hits > bestHits)
            {
                bestHits = hits;
                best = fw;
            }
        }

        if (best is null || bestHits < 2) return null;
        return inFamily.FirstOrDefault(f =>
                   string.Equals(f.RoleKey, best.RoleKey, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(f.TargetLevel, level, StringComparison.OrdinalIgnoreCase))
               ?? best;
    }

    private static string Normalize(string? value)
        => (value ?? "").Trim().ToLowerInvariant();
}
