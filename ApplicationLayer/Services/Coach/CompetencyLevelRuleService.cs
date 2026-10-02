using ApplicationLayer.Studio.Helpers;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-454: suy ra level đạt được từ competency profile.
/// Luật là GLOBAL (bảng tbl_competency_level_rules không có role/framework), framework chỉ
/// cung cấp TargetScore + RequiredDifficulty của từng skill — nên cùng một engine chạy cho mọi stack.
/// </summary>
public static class CompetencyLevelRuleService
{
    /// <summary>Một skill trong profile: điểm hiện tại + bậc khó đã chứng minh.</summary>
    public sealed record ProfileSkill(string Skill, double Score, string? DemonstratedDifficulty);

    /// <summary>
    /// Kết quả resolve level. SCRUM-509: thêm ngưỡng để FE hiện tiêu chí dễ đọc (không dump công thức).
    /// </summary>
    public sealed record LevelResolution(
        string? AchievedLevel,
        double TargetMetRatio,
        double RequiredDifficultyRatio,
        double HardEvidenceRatio,
        string Explanation,
        double Overall = 0,
        double OverallThreshold = 0,
        double TargetMetThreshold = 0,
        double RequiredDifficultyThreshold = 0,
        double HardEvidenceThreshold = 0);

    /// <summary>
    /// Level cao nhất thoả ĐỒNG THỜI 4 điều kiện của rule: overall, tỉ lệ skill đạt target,
    /// tỉ lệ skill có evidence >= required difficulty, tỉ lệ skill có evidence mức hard.
    /// Không rule nào thoả -> null (chưa đạt level thấp nhất).
    /// </summary>
    public static LevelResolution Resolve(
        double overall,
        IReadOnlyList<ProfileSkill> profileSkills,
        IReadOnlyList<CompetencyFrameworkSkill> frameworkSkills,
        IReadOnlyList<CompetencyLevelRule> rules,
        string? outputLanguage = null)
    {
        var english = StudioOutputLanguage.Normalize(outputLanguage) == StudioOutputLanguage.English;
        if (frameworkSkills.Count == 0)
            return new LevelResolution(
                null, 0, 0, 0,
                english ? "The framework has no skills to assess." : "Framework không có skill nào để đánh giá.",
                overall);

        var bySkill = profileSkills.ToDictionary(
            s => CompetencyScoringService.NormalizeSkill(s.Skill),
            s => s,
            StringComparer.Ordinal);

        var targetMet = 0;
        var requiredMet = 0;
        var hardEvidence = 0;
        foreach (var fw in frameworkSkills)
        {
            if (!bySkill.TryGetValue(CompetencyScoringService.NormalizeSkill(fw.Skill), out var p))
                continue; // skill chưa đo -> không tính là đạt

            if (p.Score >= fw.TargetScore) targetMet++;
            if (CompetencyScoringService.DifficultyRank(p.DemonstratedDifficulty)
                >= CompetencyScoringService.DifficultyRank(fw.RequiredDifficulty))
                requiredMet++;
            if (CompetencyScoringService.DifficultyRank(p.DemonstratedDifficulty)
                >= CompetencyScoringService.DifficultyRank(QuestionDifficultyLevel.Hard))
                hardEvidence++;
        }

        var total = (double)frameworkSkills.Count;
        var targetRatio = Math.Round(targetMet / total, 4);
        var requiredRatio = Math.Round(requiredMet / total, 4);
        var hardRatio = Math.Round(hardEvidence / total, 4);

        var activeRules = rules
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.SortOrder)
            .ToList();

        var achieved = activeRules.FirstOrDefault(r =>
            overall >= r.OverallThreshold
            && targetRatio >= r.TargetMetRatio
            && requiredRatio >= r.RequiredDifficultyRatio
            && hardRatio >= r.HardEvidenceRatio);

        // Ngưỡng hiển thị: rule đã đạt, hoặc rule thấp nhất nếu chưa đạt.
        var thresholdRule = achieved
            ?? activeRules.OrderBy(r => r.SortOrder).FirstOrDefault()
            ?? DefaultRules().OrderBy(r => r.SortOrder).First();

        var explanation = FormatExplanation(
            achieved?.Level,
            overall,
            thresholdRule.OverallThreshold,
            targetRatio,
            thresholdRule.TargetMetRatio,
            requiredRatio,
            thresholdRule.RequiredDifficultyRatio,
            hardRatio,
            thresholdRule.HardEvidenceRatio,
            outputLanguage);

        return new LevelResolution(
            achieved?.Level,
            targetRatio,
            requiredRatio,
            hardRatio,
            explanation,
            overall,
            thresholdRule.OverallThreshold,
            thresholdRule.TargetMetRatio,
            thresholdRule.RequiredDifficultyRatio,
            thresholdRule.HardEvidenceRatio);
    }

    /// <summary>Câu giải thích level cho báo cáo, theo ngôn ngữ user chọn ở bước CV.</summary>
    public static string FormatExplanation(
        string? achievedLevel,
        double overall,
        double overallThreshold,
        double targetRatio,
        double targetThreshold,
        double requiredRatio,
        double requiredThreshold,
        double hardRatio,
        double hardThreshold,
        string? outputLanguage)
    {
        var english = StudioOutputLanguage.Normalize(outputLanguage) == StudioOutputLanguage.English;
        if (string.IsNullOrWhiteSpace(achievedLevel))
        {
            return english
                ? $"Below the lowest level: overall {overall:0.#}, target met {targetRatio:P0}, "
                  + $"required-difficulty evidence {requiredRatio:P0}, hard evidence {hardRatio:P0}."
                : $"Chưa đạt level thấp nhất: overall {overall:0.#}, đạt target {targetRatio:P0}, "
                  + $"evidence đúng mức yêu cầu {requiredRatio:P0}, evidence mức hard {hardRatio:P0}.";
        }

        return english
            ? $"Reached {achievedLevel}: overall {overall:0.#} ≥ {overallThreshold:0.#}, "
              + $"target met {targetRatio:P0} ≥ {targetThreshold:P0}, "
              + $"required-difficulty evidence {requiredRatio:P0} ≥ {requiredThreshold:P0}, "
              + $"hard evidence {hardRatio:P0} ≥ {hardThreshold:P0}."
            : $"Đạt {achievedLevel}: overall {overall:0.#} ≥ {overallThreshold:0.#}, "
              + $"đạt target {targetRatio:P0} ≥ {targetThreshold:P0}, "
              + $"evidence đúng mức yêu cầu {requiredRatio:P0} ≥ {requiredThreshold:P0}, "
              + $"evidence mức hard {hardRatio:P0} ≥ {hardThreshold:P0}.";
    }

    /// <summary>
    /// Rule mặc định dùng khi DB chưa có bản ghi (vd. môi trường test).
    /// Giá trị trùng với seed của migration để hành vi không lệch giữa test và runtime.
    /// </summary>
    public static List<CompetencyLevelRule> DefaultRules() =>
    [
        new() { Level = CoachSeniorityLevel.Fresher, OverallThreshold = 40, TargetMetRatio = 0.4, RequiredDifficultyRatio = 0.3, HardEvidenceRatio = 0.0, SortOrder = 1 },
        new() { Level = CoachSeniorityLevel.Junior, OverallThreshold = 60, TargetMetRatio = 0.6, RequiredDifficultyRatio = 0.6, HardEvidenceRatio = 0.0, SortOrder = 2 },
        new() { Level = CoachSeniorityLevel.Middle, OverallThreshold = 75, TargetMetRatio = 0.8, RequiredDifficultyRatio = 0.8, HardEvidenceRatio = 0.5, SortOrder = 3 },
        new() { Level = CoachSeniorityLevel.Senior, OverallThreshold = 85, TargetMetRatio = 0.9, RequiredDifficultyRatio = 0.9, HardEvidenceRatio = 0.8, SortOrder = 4 }
    ];
}
