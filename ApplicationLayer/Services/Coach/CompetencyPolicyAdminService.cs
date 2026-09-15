using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-453/454: admin đọc/ghi scoring policy + level rules toàn cục.
/// Không chứa số liệu framework; chỉ ngưỡng vận hành để hệ thống chạy được.
/// </summary>
public interface ICompetencyPolicyAdminService
{
    Task<CompetencyScoringPolicyDto> GetScoringPolicyAsync();
    Task<CompetencyScoringPolicyDto> UpdateScoringPolicyAsync(CompetencyScoringPolicyDto dto);
    Task<List<CompetencyLevelRuleDto>> ListLevelRulesAsync();
    Task<List<CompetencyLevelRuleDto>> UpdateLevelRulesAsync(IReadOnlyList<CompetencyLevelRuleDto> rules);
}

public class CompetencyPolicyAdminService : ICompetencyPolicyAdminService
{
    private readonly ICompetencyFrameworkRepository _frameworks;
    private readonly ICompetencyLevelRuleRepository _rules;

    public CompetencyPolicyAdminService(
        ICompetencyFrameworkRepository frameworks,
        ICompetencyLevelRuleRepository rules)
    {
        _frameworks = frameworks;
        _rules = rules;
    }

    public async Task<CompetencyScoringPolicyDto> GetScoringPolicyAsync()
        => MapPolicy(await _frameworks.GetPolicyAsync());

    public async Task<CompetencyScoringPolicyDto> UpdateScoringPolicyAsync(CompetencyScoringPolicyDto dto)
    {
        var dimSum = dto.CorrectnessWeight + dto.RelevanceWeight + dto.ClarityWeight;
        if (Math.Abs(dimSum - 1.0) > 0.01)
            throw new BadRequestException(
                $"Tổng correctness/relevance/clarity = {dimSum:0.###}, phải bằng 1.0 (±0.01).");
        if (dto.EasyDifficultyWeight <= 0 || dto.MediumDifficultyWeight <= 0 || dto.HardDifficultyWeight <= 0)
            throw new BadRequestException("Difficulty weight phải > 0.");
        if (dto.DevelopingMaxExclusive >= dto.NearTargetMaxExclusive
            || dto.NearTargetMaxExclusive >= dto.ReadyMaxExclusive)
            throw new BadRequestException("Ngưỡng readiness phải tăng dần: Developing < NearTarget < Ready.");

        if (!string.IsNullOrWhiteSpace(dto.TargetScoreByLevelJson))
            ValidateTargetScoreJson(dto.TargetScoreByLevelJson);

        var entity = new CompetencyScoringPolicy
        {
            CorrectnessWeight = dto.CorrectnessWeight,
            RelevanceWeight = dto.RelevanceWeight,
            ClarityWeight = dto.ClarityWeight,
            EasyDifficultyWeight = dto.EasyDifficultyWeight,
            MediumDifficultyWeight = dto.MediumDifficultyWeight,
            HardDifficultyWeight = dto.HardDifficultyWeight,
            DevelopingMaxExclusive = dto.DevelopingMaxExclusive,
            NearTargetMaxExclusive = dto.NearTargetMaxExclusive,
            ReadyMaxExclusive = dto.ReadyMaxExclusive,
            JuniorReadyCoreSkillRatio = dto.JuniorReadyCoreSkillRatio,
            OverallReadyThreshold = dto.OverallReadyThreshold,
            TargetScoreByLevelJson = string.IsNullOrWhiteSpace(dto.TargetScoreByLevelJson)
                ? null
                : dto.TargetScoreByLevelJson.Trim()
        };
        await _frameworks.SavePolicyAsync(entity);
        return MapPolicy(await _frameworks.GetPolicyAsync());
    }

    public async Task<List<CompetencyLevelRuleDto>> ListLevelRulesAsync()
    {
        var rows = await _rules.ListAsync();
        if (rows.Count == 0)
            rows = CompetencyLevelRuleService.DefaultRules();
        return rows.Select(MapRule).ToList();
    }

    public async Task<List<CompetencyLevelRuleDto>> UpdateLevelRulesAsync(IReadOnlyList<CompetencyLevelRuleDto> rules)
    {
        if (rules.Count == 0)
            throw new BadRequestException("Cần ít nhất 1 level rule.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entities = new List<CompetencyLevelRule>();
        foreach (var r in rules)
        {
            var level = (r.Level ?? "").Trim();
            if (!CoachSeniorityLevel.IsValid(level))
                throw new BadRequestException($"level \"{level}\" không hợp lệ.");
            if (!seen.Add(level))
                throw new BadRequestException($"level \"{level}\" bị trùng.");
            if (r.TargetMetRatio < 0 || r.TargetMetRatio > 1
                || r.RequiredDifficultyRatio < 0 || r.RequiredDifficultyRatio > 1
                || r.HardEvidenceRatio < 0 || r.HardEvidenceRatio > 1)
                throw new BadRequestException($"level \"{level}\": các tỉ lệ phải trong [0, 1].");

            entities.Add(new CompetencyLevelRule
            {
                Level = CoachSeniorityLevel.All.First(l =>
                    string.Equals(l, level, StringComparison.OrdinalIgnoreCase)),
                OverallThreshold = r.OverallThreshold,
                TargetMetRatio = r.TargetMetRatio,
                RequiredDifficultyRatio = r.RequiredDifficultyRatio,
                HardEvidenceRatio = r.HardEvidenceRatio,
                SortOrder = r.SortOrder
            });
        }

        await _rules.UpsertRangeAsync(entities);
        return await ListLevelRulesAsync();
    }

    private static void ValidateTargetScoreJson(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                throw new BadRequestException("TargetScoreByLevelJson phải là object { level: score }.");
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!prop.Value.TryGetDouble(out var v) || v < 0 || v > 100)
                    throw new BadRequestException($"TargetScoreByLevelJson.{prop.Name} phải là số 0–100.");
            }
        }
        catch (System.Text.Json.JsonException)
        {
            throw new BadRequestException("TargetScoreByLevelJson không phải JSON hợp lệ.");
        }
    }

    private static CompetencyScoringPolicyDto MapPolicy(CompetencyScoringPolicy p) => new()
    {
        CorrectnessWeight = p.CorrectnessWeight,
        RelevanceWeight = p.RelevanceWeight,
        ClarityWeight = p.ClarityWeight,
        EasyDifficultyWeight = p.EasyDifficultyWeight,
        MediumDifficultyWeight = p.MediumDifficultyWeight,
        HardDifficultyWeight = p.HardDifficultyWeight,
        DevelopingMaxExclusive = p.DevelopingMaxExclusive,
        NearTargetMaxExclusive = p.NearTargetMaxExclusive,
        ReadyMaxExclusive = p.ReadyMaxExclusive,
        JuniorReadyCoreSkillRatio = p.JuniorReadyCoreSkillRatio,
        OverallReadyThreshold = p.OverallReadyThreshold,
        TargetScoreByLevelJson = p.TargetScoreByLevelJson
    };

    private static CompetencyLevelRuleDto MapRule(CompetencyLevelRule r) => new()
    {
        Id = r.Id,
        Level = r.Level,
        OverallThreshold = r.OverallThreshold,
        TargetMetRatio = r.TargetMetRatio,
        RequiredDifficultyRatio = r.RequiredDifficultyRatio,
        HardEvidenceRatio = r.HardEvidenceRatio,
        SortOrder = r.SortOrder
    };
}
