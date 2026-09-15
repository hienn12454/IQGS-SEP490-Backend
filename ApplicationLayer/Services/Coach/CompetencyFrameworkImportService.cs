using System.Text.Json;
using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-453: nạp competency framework từ curated data (JSON) — đường import DUY NHẤT
/// cho mọi role/technology. Thêm Java/Python/React chỉ là thêm file JSON, không sửa code.
/// </summary>
public interface ICompetencyFrameworkImportService
{
    /// <summary>Dry-run: chỉ validate, không ghi DB.</summary>
    Task<CompetencyFrameworkImportResultDto> ValidateAsync(CompetencyFrameworkImportDto payload);
    Task<CompetencyFrameworkImportResultDto> ImportAsync(CompetencyFrameworkImportDto payload);
    Task<List<CompetencyFrameworkAdminDto>> ListAsync();
    Task UpdateStatusAsync(Guid frameworkId, string status);
}

public class CompetencyFrameworkImportService : ICompetencyFrameworkImportService
{
    /// <summary>Tổng ImportanceWeight của một level phải bằng 1.0 (sai số cho phép do làm tròn).</summary>
    private const double WeightSumTolerance = 0.01;
    private const int MinSkillsPerLevel = 3;

    private readonly ICompetencyFrameworkRepository _frameworks;

    public CompetencyFrameworkImportService(ICompetencyFrameworkRepository frameworks)
        => _frameworks = frameworks;

    public async Task<CompetencyFrameworkImportResultDto> ValidateAsync(CompetencyFrameworkImportDto payload)
    {
        var result = await ValidateInternalAsync(payload);
        result.DryRun = true;
        result.Success = result.Errors.Count == 0;
        if (result.Success)
        {
            result.Imported = payload.Levels.Select(l => new CompetencyFrameworkImportedLevelDto
            {
                RoleKey = NormalizeRoleKey(payload.RoleKey),
                Level = l.Level.Trim(),
                SkillCount = l.Skills.Count
            }).ToList();
            result.AliasCount = payload.Aliases.Count;
        }
        return result;
    }

    public async Task<CompetencyFrameworkImportResultDto> ImportAsync(CompetencyFrameworkImportDto payload)
    {
        var result = await ValidateInternalAsync(payload);
        if (result.Errors.Count > 0)
        {
            result.Success = false;
            return result;
        }

        var roleKey = NormalizeRoleKey(payload.RoleKey);
        var stackJson = JsonSerializer.Serialize(
            payload.Stack.Select(s => s.Trim()).Where(s => s.Length > 0).ToList());

        foreach (var level in payload.Levels)
        {
            var framework = new CompetencyFramework
            {
                RoleKey = roleKey,
                DisplayRole = payload.DisplayRole.Trim(),
                TargetLevel = NormalizeLevel(level.Level),
                Status = string.IsNullOrWhiteSpace(level.Status)
                    ? CompetencyFrameworkStatus.Active
                    : level.Status!.Trim(),
                Description = payload.Description?.Trim(),
                Technology = string.IsNullOrWhiteSpace(payload.Technology) ? null : payload.Technology!.Trim(),
                StackJson = stackJson,
                // Đã qua importer nghĩa là số liệu đến từ bộ curated data có sourceRef/sourceVersion.
                Provenance = CompetencyFrameworkProvenance.Curated,
                SourceRef = payload.SourceRef.Trim(),
                SourceVersion = payload.SourceVersion.Trim()
            };

            var skills = level.Skills.Select((s, i) => new CompetencyFrameworkSkill
            {
                Skill = s.Skill.Trim(),
                ImportanceWeight = s.ImportanceWeight,
                TargetScore = s.TargetScore,
                RequiredDifficulty = DiagnosticBlueprintBuilder.NormalizeDifficulty(s.RequiredDifficulty),
                TopicsJson = CompetencyTopicParser.Serialize(
                    s.Topics.Select(t => new CompetencyTopicParser.TopicNode(
                        t.Topic.Trim(),
                        t.Subtopics.Select(x => x.Trim()).Where(x => x.Length > 0).ToList()))),
                SortOrder = i + 1
            }).ToList();

            var id = await _frameworks.UpsertFrameworkAsync(framework, skills);
            result.Imported.Add(new CompetencyFrameworkImportedLevelDto
            {
                FrameworkId = id,
                RoleKey = roleKey,
                Level = framework.TargetLevel,
                SkillCount = skills.Count
            });
        }

        // Alias: RoleKey luôn là alias exact của chính nó để resolve không phụ thuộc file import.
        var aliases = new List<CompetencyRoleAlias>
        {
            new()
            {
                RoleKey = roleKey,
                Alias = roleKey,
                MatchKind = CompetencyRoleAliasMatchKind.Exact,
                SortOrder = 0
            }
        };
        var order = 1;
        foreach (var alias in payload.Aliases)
        {
            var normalized = alias.Alias.Trim().ToLowerInvariant();
            if (normalized.Length == 0 || normalized == roleKey) continue;
            aliases.Add(new CompetencyRoleAlias
            {
                RoleKey = roleKey,
                Alias = normalized,
                MatchKind = string.Equals(alias.MatchKind, CompetencyRoleAliasMatchKind.Exact, StringComparison.OrdinalIgnoreCase)
                    ? CompetencyRoleAliasMatchKind.Exact
                    : CompetencyRoleAliasMatchKind.Contains,
                SortOrder = order++
            });
        }
        await _frameworks.ReplaceAliasesAsync(roleKey, aliases);
        result.AliasCount = aliases.Count;
        result.Success = true;
        return result;
    }

    public async Task<List<CompetencyFrameworkAdminDto>> ListAsync()
    {
        var all = await _frameworks.ListAllWithSkillsAsync();
        return all.Select(f => new CompetencyFrameworkAdminDto
        {
            Id = f.Id,
            RoleKey = f.RoleKey,
            DisplayRole = f.DisplayRole,
            Technology = f.Technology,
            TargetLevel = f.TargetLevel,
            Status = f.Status,
            Provenance = f.Provenance,
            SourceRef = f.SourceRef,
            SourceVersion = f.SourceVersion,
            SkillCount = f.Skills.Count,
            WeightSum = Math.Round(f.Skills.Sum(s => s.ImportanceWeight), 4),
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        }).ToList();
    }

    public Task UpdateStatusAsync(Guid frameworkId, string status)
    {
        var normalized = string.Equals(status, CompetencyFrameworkStatus.Draft, StringComparison.OrdinalIgnoreCase)
            ? CompetencyFrameworkStatus.Draft
            : CompetencyFrameworkStatus.Active;
        return _frameworks.UpdateStatusAsync(frameworkId, normalized);
    }

    /// <summary>
    /// Validate ràng buộc dữ liệu framework. Đây là hàng rào duy nhất ngăn số liệu "tự nghĩ"
    /// hoặc sai lệch vào DB, nên chạy cả ở dry-run và import thật.
    /// </summary>
    private async Task<CompetencyFrameworkImportResultDto> ValidateInternalAsync(CompetencyFrameworkImportDto payload)
    {
        var result = new CompetencyFrameworkImportResultDto();
        var errors = result.Errors;

        var roleKey = NormalizeRoleKey(payload.RoleKey);
        if (roleKey.Length == 0) errors.Add("roleKey không được rỗng.");
        if (string.IsNullOrWhiteSpace(payload.DisplayRole)) errors.Add("displayRole không được rỗng.");
        if (string.IsNullOrWhiteSpace(payload.SourceRef)) errors.Add("sourceRef bắt buộc — cần truy vết bộ curated data.");
        if (string.IsNullOrWhiteSpace(payload.SourceVersion)) errors.Add("sourceVersion bắt buộc.");
        if (payload.Levels.Count == 0) errors.Add("Cần ít nhất 1 level.");

        var seenLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var level in payload.Levels)
        {
            var levelName = (level.Level ?? "").Trim();
            if (!CoachSeniorityLevel.IsValid(levelName))
                errors.Add($"level \"{levelName}\" không hợp lệ (chỉ {string.Join("/", CoachSeniorityLevel.All)}).");
            else if (!seenLevels.Add(levelName))
                errors.Add($"level \"{levelName}\" bị khai báo trùng trong file.");

            if (level.Skills.Count < MinSkillsPerLevel)
            {
                errors.Add($"level \"{levelName}\" cần tối thiểu {MinSkillsPerLevel} skill (đang {level.Skills.Count}).");
            }

            var seenSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var skill in level.Skills)
            {
                var name = (skill.Skill ?? "").Trim();
                if (name.Length == 0)
                {
                    errors.Add($"level \"{levelName}\": có skill thiếu tên.");
                    continue;
                }
                if (!seenSkills.Add(name))
                    errors.Add($"level \"{levelName}\": skill \"{name}\" bị trùng.");
                if (skill.ImportanceWeight <= 0 || skill.ImportanceWeight > 1)
                    errors.Add($"level \"{levelName}\" / skill \"{name}\": importanceWeight phải trong (0, 1].");
                if (skill.TargetScore < 0 || skill.TargetScore > 100)
                    errors.Add($"level \"{levelName}\" / skill \"{name}\": targetScore phải trong [0, 100].");
                var difficulty = (skill.RequiredDifficulty ?? "").Trim().ToLowerInvariant();
                if (difficulty != QuestionDifficultyLevel.Easy
                    && difficulty != QuestionDifficultyLevel.Medium
                    && difficulty != QuestionDifficultyLevel.Hard)
                    errors.Add($"level \"{levelName}\" / skill \"{name}\": requiredDifficulty phải là easy/medium/hard.");
                if (skill.Topics.Count == 0 || skill.Topics.All(t => string.IsNullOrWhiteSpace(t.Topic)))
                    errors.Add($"level \"{levelName}\" / skill \"{name}\": cần ít nhất 1 topic.");
            }

            var weightSum = level.Skills.Sum(s => s.ImportanceWeight);
            if (level.Skills.Count > 0 && Math.Abs(weightSum - 1.0) > WeightSumTolerance)
                errors.Add($"level \"{levelName}\": tổng importanceWeight = {weightSum:0.###}, phải bằng 1.0 (±{WeightSumTolerance}).");
        }

        // Alias không được trùng với alias của role khác — nếu trùng thì resolve sẽ nhập nhằng.
        var existingAliases = await _frameworks.ListAliasesAsync();
        var payloadAliases = payload.Aliases
            .Select(a => (a.Alias ?? "").Trim().ToLowerInvariant())
            .Where(a => a.Length > 0)
            .ToList();
        if (payloadAliases.Count != payloadAliases.Distinct().Count())
            errors.Add("aliases bị trùng trong cùng file.");
        foreach (var alias in payloadAliases.Distinct())
        {
            var clash = existingAliases.FirstOrDefault(a =>
                string.Equals(a.Alias, alias, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(a.RoleKey, roleKey, StringComparison.OrdinalIgnoreCase));
            if (clash is not null)
                errors.Add($"alias \"{alias}\" đã thuộc role \"{clash.RoleKey}\".");
        }

        if (payloadAliases.Count == 0)
            result.Warnings.Add("Không có alias: chỉ resolve được khi ứng viên nhập đúng roleKey/displayRole.");

        var missingLevels = CoachSeniorityLevel.All
            .Where(l => !seenLevels.Contains(l))
            .ToList();
        if (missingLevels.Count > 0)
            result.Warnings.Add($"Chưa có framework cho level: {string.Join(", ", missingLevels)} (resolver sẽ dùng level gần nhất).");

        result.Success = errors.Count == 0;
        return result;
    }

    private static string NormalizeRoleKey(string? roleKey)
        => (roleKey ?? "").Trim().ToLowerInvariant();

    private static string NormalizeLevel(string level)
        => CoachSeniorityLevel.All.First(l => string.Equals(l, level.Trim(), StringComparison.OrdinalIgnoreCase));
}
