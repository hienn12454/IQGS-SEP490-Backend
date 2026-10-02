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

        // Client cũ / payload thiếu drill → giữ default entity thay vì 0 khiến validate fail hoặc ghi đè mất config
        ApplyDrillDefaultsIfMissing(dto);
        ValidateDrillPolicy(dto);
        ApplyDiagnosticDefaultsIfMissing(dto);
        ValidateDiagnosticPolicy(dto);

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
                : dto.TargetScoreByLevelJson.Trim(),
            // SCRUM-488: persist drill config — trước đây FE gửi nhưng BE bỏ qua → Admin “lưu” không có hiệu lực
            DrillPassScoreExclusiveMin = dto.DrillPassScoreExclusiveMin,
            DrillQuestionCountWeak = dto.DrillQuestionCountWeak,
            DrillQuestionCountMid = dto.DrillQuestionCountMid,
            DrillQuestionCountStrong = dto.DrillQuestionCountStrong,
            DrillWeakBandRatio = dto.DrillWeakBandRatio,
            DrillRemixEnabled = dto.DrillRemixEnabled,
            DrillRemixRatio = dto.DrillRemixRatio,
            DrillWeakAnswerScoreMaxExclusive = dto.DrillWeakAnswerScoreMaxExclusive,
            DiagnosticQuestionsPerSkill = dto.DiagnosticQuestionsPerSkill,
            DiagnosticMinSkills = dto.DiagnosticMinSkills,
            DiagnosticMaxSkills = dto.DiagnosticMaxSkills,
            DiagnosticMaxAdaptiveSkills = dto.DiagnosticMaxAdaptiveSkills,
            DiagnosticMinTotalQuestions = dto.DiagnosticMinTotalQuestions,
            ScreeningEnabled = dto.ScreeningEnabled,
            ScreeningQuestionsPerSkill = dto.ScreeningQuestionsPerSkill,
            ScreeningMaxSkills = dto.ScreeningMaxSkills,
            ReassessmentQuestionsPerSkill = dto.ReassessmentQuestionsPerSkill
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

    /// <summary>SCRUM-488: khớp ràng buộc UI Admin (pass 1–99, count 5–40, ratio 0–1).</summary>
    private static void ValidateDrillPolicy(CompetencyScoringPolicyDto dto)
    {
        if (dto.DrillPassScoreExclusiveMin is < 1 or > 99)
            throw new BadRequestException("DrillPassScoreExclusiveMin phải trong [1, 99].");
        if (dto.DrillQuestionCountWeak is < 5 or > 40
            || dto.DrillQuestionCountMid is < 5 or > 40
            || dto.DrillQuestionCountStrong is < 5 or > 40)
            throw new BadRequestException("Số câu drill theo band phải trong [5, 40].");
        if (dto.DrillWeakBandRatio is < 0 or > 1)
            throw new BadRequestException("DrillWeakBandRatio phải trong [0, 1].");
        if (dto.DrillRemixRatio is < 0 or > 1)
            throw new BadRequestException("DrillRemixRatio phải trong [0, 1].");
        if (dto.DrillWeakAnswerScoreMaxExclusive is < 1 or > 99)
            throw new BadRequestException("DrillWeakAnswerScoreMaxExclusive phải trong [1, 99].");
    }

    /// <summary>
    /// JSON omit / client cũ để 0 — không được ghi đè DB bằng 0.
    /// bool DrillRemixEnabled mặc định true khi omit (default của bool là false → cần heuristic).
    /// </summary>
    private static void ApplyDrillDefaultsIfMissing(CompetencyScoringPolicyDto dto)
    {
        if (dto.DrillPassScoreExclusiveMin <= 0) dto.DrillPassScoreExclusiveMin = 70;
        if (dto.DrillQuestionCountWeak <= 0) dto.DrillQuestionCountWeak = 20;
        if (dto.DrillQuestionCountMid <= 0) dto.DrillQuestionCountMid = 15;
        if (dto.DrillQuestionCountStrong <= 0) dto.DrillQuestionCountStrong = 10;
        if (dto.DrillWeakBandRatio <= 0) dto.DrillWeakBandRatio = 0.6;
        if (dto.DrillRemixRatio < 0) dto.DrillRemixRatio = 0.35;
        if (dto.DrillWeakAnswerScoreMaxExclusive <= 0) dto.DrillWeakAnswerScoreMaxExclusive = 50;
        // DrillRemixEnabled: false là giá trị hợp lệ — không ép true ở đây
    }

    /// <summary>JSON omit / client cũ để 0 — không ghi đè DB bằng 0.</summary>
    private static void ApplyDiagnosticDefaultsIfMissing(CompetencyScoringPolicyDto dto)
    {
        if (dto.DiagnosticQuestionsPerSkill <= 0) dto.DiagnosticQuestionsPerSkill = 3;
        if (dto.DiagnosticMinSkills <= 0) dto.DiagnosticMinSkills = 3;
        if (dto.DiagnosticMaxSkills <= 0) dto.DiagnosticMaxSkills = 5;
        if (dto.DiagnosticMaxAdaptiveSkills <= 0) dto.DiagnosticMaxAdaptiveSkills = 8;
        if (dto.DiagnosticMinTotalQuestions < 0) dto.DiagnosticMinTotalQuestions = 0;
        if (dto.ScreeningQuestionsPerSkill <= 0) dto.ScreeningQuestionsPerSkill = 1;
        if (dto.ScreeningMaxSkills <= 0) dto.ScreeningMaxSkills = 12;
        if (dto.ReassessmentQuestionsPerSkill <= 0) dto.ReassessmentQuestionsPerSkill = 3;
        // ScreeningEnabled: false là giá trị hợp lệ — JSON omit dùng default true trên DTO.
    }

    private static void ValidateDiagnosticPolicy(CompetencyScoringPolicyDto dto)
    {
        if (dto.DiagnosticQuestionsPerSkill is < 2 or > 6)
            throw new BadRequestException("DiagnosticQuestionsPerSkill phải trong [2, 6].");
        if (dto.DiagnosticMinSkills is < 1 or > 12
            || dto.DiagnosticMaxSkills is < 1 or > 12)
            throw new BadRequestException("Số skill chẩn đoán (min/max) phải trong [1, 12].");
        if (dto.DiagnosticMinSkills > dto.DiagnosticMaxSkills)
            throw new BadRequestException("DiagnosticMinSkills không được lớn hơn DiagnosticMaxSkills.");
        if (dto.DiagnosticMaxAdaptiveSkills is < 1 or > 20)
            throw new BadRequestException("DiagnosticMaxAdaptiveSkills phải trong [1, 20].");
        if (dto.DiagnosticMinTotalQuestions is < 0 or > 60)
            throw new BadRequestException("DiagnosticMinTotalQuestions phải trong [0, 60] (0 = tắt).");
        if (dto.ScreeningQuestionsPerSkill is < 1 or > 3)
            throw new BadRequestException("ScreeningQuestionsPerSkill phải trong [1, 3].");
        if (dto.ScreeningMaxSkills is < 1 or > 30)
            throw new BadRequestException("ScreeningMaxSkills phải trong [1, 30].");
        if (dto.ReassessmentQuestionsPerSkill is < 2 or > 10)
            throw new BadRequestException("ReassessmentQuestionsPerSkill phải trong [2, 10].");
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
        TargetScoreByLevelJson = p.TargetScoreByLevelJson,
        DrillPassScoreExclusiveMin = p.DrillPassScoreExclusiveMin,
        DrillQuestionCountWeak = p.DrillQuestionCountWeak,
        DrillQuestionCountMid = p.DrillQuestionCountMid,
        DrillQuestionCountStrong = p.DrillQuestionCountStrong,
        DrillWeakBandRatio = p.DrillWeakBandRatio,
        DrillRemixEnabled = p.DrillRemixEnabled,
        DrillRemixRatio = p.DrillRemixRatio,
        DrillWeakAnswerScoreMaxExclusive = p.DrillWeakAnswerScoreMaxExclusive,
        DiagnosticQuestionsPerSkill = CoachDiagnosticPolicy.QuestionsPerSkill(p),
        DiagnosticMinSkills = CoachDiagnosticPolicy.MinSkills(p),
        DiagnosticMaxSkills = CoachDiagnosticPolicy.MaxSkills(p),
        DiagnosticMaxAdaptiveSkills = CoachDiagnosticPolicy.MaxAdaptiveSkills(p),
        DiagnosticMinTotalQuestions = CoachDiagnosticPolicy.MinTotalQuestions(p),
        ScreeningEnabled = CoachDiagnosticPolicy.ScreeningEnabled(p),
        ScreeningQuestionsPerSkill = CoachDiagnosticPolicy.ScreeningQuestionsPerSkill(p),
        ScreeningMaxSkills = CoachDiagnosticPolicy.ScreeningMaxSkills(p),
        ReassessmentQuestionsPerSkill = CoachDiagnosticPolicy.ReassessmentQuestionsPerSkill(p)
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
