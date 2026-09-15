using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services.Coach;

public interface ICompetencyRoleFamilyAdminService
{
    Task<List<CompetencyRoleFamilyDto>> ListAsync();
    Task<CompetencyRoleFamilyDto> UpsertAsync(UpsertCompetencyRoleFamilyDto dto);
}

public class CompetencyRoleFamilyAdminService : ICompetencyRoleFamilyAdminService
{
    private readonly ICompetencyRoleFamilyRepository _families;

    public CompetencyRoleFamilyAdminService(ICompetencyRoleFamilyRepository families)
        => _families = families;

    public async Task<List<CompetencyRoleFamilyDto>> ListAsync()
    {
        var rows = await _families.ListAllAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<CompetencyRoleFamilyDto> UpsertAsync(UpsertCompetencyRoleFamilyDto dto)
    {
        var key = (dto.FamilyKey ?? "").Trim().ToLowerInvariant();
        if (key.Length == 0)
            throw new BadRequestException("FamilyKey bắt buộc.");
        if (string.IsNullOrWhiteSpace(dto.DisplayName))
            throw new BadRequestException("DisplayName bắt buộc.");

        var status = string.IsNullOrWhiteSpace(dto.Status)
            ? CompetencyFrameworkStatus.Active
            : dto.Status.Trim();
        if (status is not (CompetencyFrameworkStatus.Active or CompetencyFrameworkStatus.Draft))
            throw new BadRequestException("Status phải là Active hoặc Draft.");

        var aliases = new List<CompetencyRoleFamilyAlias>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = 1;
        foreach (var a in dto.Aliases)
        {
            var alias = (a.Alias ?? "").Trim().ToLowerInvariant();
            if (alias.Length == 0 || !seen.Add(alias)) continue;
            var kind = (a.MatchKind ?? CompetencyRoleAliasMatchKind.Contains).Trim().ToLowerInvariant();
            if (kind is not (CompetencyRoleAliasMatchKind.Exact or CompetencyRoleAliasMatchKind.Contains))
                kind = CompetencyRoleAliasMatchKind.Contains;
            aliases.Add(new CompetencyRoleFamilyAlias
            {
                FamilyKey = key,
                Alias = alias,
                MatchKind = kind,
                SortOrder = a.SortOrder > 0 ? a.SortOrder : order++
            });
        }

        var family = new CompetencyRoleFamily
        {
            FamilyKey = key,
            DisplayName = dto.DisplayName.Trim(),
            Status = status,
            SortOrder = dto.SortOrder,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim()
        };
        await _families.UpsertFamilyAsync(family, aliases);
        var all = await _families.ListAllAsync();
        return Map(all.First(f => string.Equals(f.FamilyKey, key, StringComparison.OrdinalIgnoreCase)));
    }

    private static CompetencyRoleFamilyDto Map(CompetencyRoleFamily f) => new()
    {
        Id = f.Id,
        FamilyKey = f.FamilyKey,
        DisplayName = f.DisplayName,
        Status = f.Status,
        SortOrder = f.SortOrder,
        Description = f.Description,
        Aliases = f.Aliases
            .OrderBy(a => a.SortOrder)
            .Select(a => new CompetencyRoleFamilyAliasDto
            {
                Id = a.Id,
                Alias = a.Alias,
                MatchKind = a.MatchKind,
                SortOrder = a.SortOrder
            })
            .ToList()
    };
}
