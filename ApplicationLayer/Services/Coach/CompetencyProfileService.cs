using System.Text.Json;
using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-454: gộp kết quả một lần đo vào Competency Profile tích luỹ của ứng viên.
/// Lý do tồn tại: re-assessment chỉ đo 1 skill, nếu lấy trực tiếp kết quả lần đo đó làm
/// competency thì Overall/Level bị tính trên 1 skill và các skill khác biến mất khỏi report.
/// </summary>
public interface ICompetencyProfileService
{
    Task<ProfileMergeResult> MergeAsync(
        Guid candidateUserId,
        CandidateAssessment assessment,
        CompetencyFramework? framework,
        CompetencyBlueprint? blueprint = null);

    Task<CandidateSkillPlan?> GetProfileAsync(Guid candidateUserId);

    /// <summary>Level + giải thích tính trên profile hiện tại (dùng cho report).</summary>
    Task<CompetencyLevelRuleService.LevelResolution> ResolveLevelAsync(
        CandidateSkillPlan profile,
        IReadOnlyList<CompetencyFrameworkSkill> scoringSkills);
}

public sealed record SkillDelta(string Skill, double? PreviousScore, double NewScore, double? Delta);

/// <summary>
/// Kết quả merge assessment vào profile. CoverageRatio = tỉ lệ skill framework đã có dữ liệu đo.
/// </summary>
public sealed record ProfileMergeResult(
    CandidateSkillPlan Profile,
    double? PreviousOverall,
    double? OverallDelta,
    List<SkillDelta> SkillDeltas,
    string? AchievedLevel,
    string LevelExplanation,
    double CoverageRatio);

public class CompetencyProfileService : ICompetencyProfileService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ICandidateSkillPlanRepository _plans;
    private readonly ICompetencyFrameworkRepository _frameworks;
    private readonly ICompetencyLevelRuleRepository _levelRules;

    public CompetencyProfileService(
        ICandidateSkillPlanRepository plans,
        ICompetencyFrameworkRepository frameworks,
        ICompetencyLevelRuleRepository levelRules)
    {
        _plans = plans;
        _frameworks = frameworks;
        _levelRules = levelRules;
    }

    public Task<CandidateSkillPlan?> GetProfileAsync(Guid candidateUserId)
        => _plans.GetByCandidateUserIdAsync(candidateUserId);

    public async Task<ProfileMergeResult> MergeAsync(
        Guid candidateUserId,
        CandidateAssessment assessment,
        CompetencyFramework? framework,
        CompetencyBlueprint? blueprint = null)
    {
        blueprint ??= CompetencyBlueprintJson.Deserialize(assessment.BlueprintJson);
        var scoringSkills = ResolveScoringSkills(framework, blueprint);
        var plan = await _plans.GetByCandidateUserIdAsync(candidateUserId);
        var isNew = plan is null;
        plan ??= new CandidateSkillPlan
        {
            CandidateUserId = candidateUserId,
            Status = CandidateSkillPlanStatus.Active
        };

        var previousOverall = plan.OverallReadiness;
        var previousScores = plan.Items
            .GroupBy(i => CompetencyScoringService.NormalizeSkill(i.Skill), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().CurrentScore, StringComparer.Ordinal);

        var nextMode = assessment.ResolutionMode;
        if (string.IsNullOrWhiteSpace(nextMode))
            nextMode = framework is not null
                ? CompetencyResolutionMode.Framework
                : CompetencyResolutionMode.Adaptive;

        var scaleChanged = (plan.ResolutionMode is not null
                            && !string.Equals(plan.ResolutionMode, nextMode, StringComparison.OrdinalIgnoreCase))
                           || (framework is not null && plan.FrameworkId is Guid oldFw && oldFw != framework.Id)
                           || (framework is null && plan.FrameworkId is not null && nextMode == CompetencyResolutionMode.Adaptive);
        if (scaleChanged)
        {
            plan.Items.Clear();
            previousScores.Clear();
            previousOverall = null;
        }

        plan.FrameworkId = framework?.Id;
        plan.ResolutionMode = nextMode;
        plan.RoleFamilyKey = assessment.RoleFamilyKey ?? blueprint?.RoleKey;
        plan.ActiveBlueprintJson = assessment.BlueprintJson ?? (blueprint is null ? plan.ActiveBlueprintJson : CompetencyBlueprintJson.Serialize(blueprint));
        plan.TargetLevel = blueprint?.TargetLevel ?? framework?.TargetLevel ?? plan.TargetLevel;

        if (assessment.Kind == CandidateAssessmentKind.Diagnostic)
            plan.SourceDiagnosticSetId = assessment.QuestionSetId;

        var scope = ParseScopeSkills(assessment.ScopeSkillsJson);
        var deltas = new List<SkillDelta>();

        var resultsToMerge = assessment.SkillResults
            .Where(r => scope.Count == 0 || scope.Contains(CompetencyScoringService.NormalizeSkill(r.Skill)))
            .ToList();
        if (resultsToMerge.Count == 0)
            resultsToMerge = assessment.SkillResults.ToList();

        foreach (var result in resultsToMerge)
        {
            var key = CompetencyScoringService.NormalizeSkill(result.Skill);

            var fwSkill = scoringSkills.FirstOrDefault(s =>
                CompetencyScoringService.NormalizeSkill(s.Skill) == key);

            var item = plan.Items.FirstOrDefault(i =>
                CompetencyScoringService.NormalizeSkill(i.Skill) == key);
            if (item is null)
            {
                item = new CandidateSkillPlanItem
                {
                    Skill = Truncate(fwSkill?.Skill ?? result.Skill, 200),
                    Status = CandidateSkillPlanItemStatus.Pending
                };
                plan.Items.Add(item);
            }

            previousScores.TryGetValue(key, out var prevScore);
            item.TargetScore = fwSkill?.TargetScore ?? result.TargetScore;
            item.ImportanceWeight = fwSkill?.ImportanceWeight ?? result.ImportanceWeight;
            item.CurrentScore = result.SkillScore;
            item.BaselineScore ??= result.SkillScore;
            item.DemonstratedDifficulty = result.DemonstratedDifficulty;
            item.SourceAssessmentId = assessment.Id;
            item.UpdatedFromKind = assessment.Kind;
            item.SourceMode = nextMode == CompetencyResolutionMode.Adaptive
                ? CompetencySourceMode.Rag
                : CompetencySourceMode.Framework;
            item.LastSessionId = assessment.PracticeSessionId ?? item.LastSessionId;
            item.Status = CandidateSkillPlanService.ResolveStatus(item.CurrentScore, item.TargetScore);
            item.UpdatedAt = DateTime.UtcNow;

            deltas.Add(new SkillDelta(
                item.Skill,
                prevScore,
                result.SkillScore,
                prevScore is null ? null : Math.Round(result.SkillScore - prevScore.Value, 2)));
        }

        var measured = plan.Items.Where(i => i.CurrentScore is not null).ToList();
        var weightSum = measured.Sum(i => i.ImportanceWeight);
        double? overall = null;
        if (measured.Count > 0)
        {
            overall = weightSum > 0
                ? Math.Round(measured.Sum(i => (i.CurrentScore ?? 0) * i.ImportanceWeight) / weightSum, 2)
                : Math.Round(measured.Average(i => i.CurrentScore ?? 0), 2);
        }

        var policy = await _frameworks.GetPolicyAsync();
        plan.OverallReadiness = overall;
        plan.ReadinessStatus = overall is null
            ? null
            : CompetencyScoringService.ResolveReadinessStatus(policy, overall.Value);
        plan.LastAssessmentId = assessment.Id;

        var level = await ResolveLevelAsync(plan, scoringSkills);
        plan.AchievedLevel = level.AchievedLevel;
        plan.UpdatedAt = DateTime.UtcNow;

        if (isNew) await _plans.AddAsync(plan);
        else await _plans.UpdateAsync(plan);

        var denom = scoringSkills.Count == 0 ? measured.Count : scoringSkills.Count;
        var coverage = denom == 0 ? 0 : Math.Round((double)measured.Count / denom, 4);

        return new ProfileMergeResult(
            plan,
            previousOverall,
            previousOverall is null || overall is null ? null : Math.Round(overall.Value - previousOverall.Value, 2),
            deltas,
            level.AchievedLevel,
            level.Explanation,
            coverage);
    }

    public async Task<CompetencyLevelRuleService.LevelResolution> ResolveLevelAsync(
        CandidateSkillPlan profile,
        IReadOnlyList<CompetencyFrameworkSkill> scoringSkills)
    {
        var rules = await _levelRules.ListAsync();
        if (rules.Count == 0) rules = CompetencyLevelRuleService.DefaultRules();

        var profileSkills = profile.Items
            .Where(i => i.CurrentScore is not null)
            .Select(i => new CompetencyLevelRuleService.ProfileSkill(
                i.Skill, i.CurrentScore ?? 0, i.DemonstratedDifficulty))
            .ToList();

        return CompetencyLevelRuleService.Resolve(
            profile.OverallReadiness ?? 0,
            profileSkills,
            scoringSkills.ToList(),
            rules);
    }

    public static List<CompetencyFrameworkSkill> ResolveScoringSkills(
        CompetencyFramework? framework,
        CompetencyBlueprint? blueprint)
    {
        if (blueprint is { Competencies.Count: > 0 })
            return FrameworkBlueprintBuilder.ToScoringSkills(blueprint);
        return framework?.Skills.ToList() ?? new List<CompetencyFrameworkSkill>();
    }

    /// <summary>
    /// Khi blueprint/framework không deserialize được: vẫn chấm theo skill đã hỏi, không bỏ scoring im lặng.
    /// </summary>
    public static List<CompetencyFrameworkSkill> FallbackScoringSkills(
        IEnumerable<string> skillNames,
        CompetencyScoringPolicy policy,
        string? targetLevel)
    {
        var target = CompetencyTargetScorePolicy.Resolve(policy, targetLevel);
        return skillNames
            .Select(CompetencyScoringService.NormalizeSkill)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Select(s => new CompetencyFrameworkSkill
            {
                Skill = s.Length > 200 ? s[..200] : s,
                ImportanceWeight = 1,
                TargetScore = target
            })
            .ToList();
    }

    private static HashSet<string> ParseScopeSkills(string? json)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? new List<string>();
            foreach (var s in list)
                if (!string.IsNullOrWhiteSpace(s))
                    result.Add(CompetencyScoringService.NormalizeSkill(s));
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
        return result;
    }

    private static string Truncate(string value, int max)
        => value.Length > max ? value[..max] : value;
}
