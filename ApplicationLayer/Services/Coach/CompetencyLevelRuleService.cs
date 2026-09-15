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

    public sealed record LevelResolution(
        string? AchievedLevel,
        double TargetMetRatio,
        double RequiredDifficultyRatio,
        double HardEvidenceRatio,
        string Explanation);

    /// <summary>
    /// Level cao nhất thoả ĐỒNG THỜI 4 điều kiện của rule: overall, tỉ lệ skill đạt target,
    /// tỉ lệ skill có evidence >= required difficulty, tỉ lệ skill có evidence mức hard.
    /// Không rule nào thoả -> null (chưa đạt level thấp nhất).
    /// </summary>
    public static LevelResolution Resolve(
        double overall,
        IReadOnlyList<ProfileSkill> profileSkills,
        IReadOnlyList<CompetencyFrameworkSkill> frameworkSkills,
        IReadOnlyList<CompetencyLevelRule> rules)
    {
        if (frameworkSkills.Count == 0)
            return new LevelResolution(null, 0, 0, 0, "Framework không có skill nào để đánh giá.");

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

        var achieved = rules
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.SortOrder)
            .FirstOrDefault(r =>
                overall >= r.OverallThreshold
                && targetRatio >= r.TargetMetRatio
                && requiredRatio >= r.RequiredDifficultyRatio
                && hardRatio >= r.HardEvidenceRatio);

        var explanation = achieved is null
            ? $"Chưa đạt level thấp nhất: overall {overall:0.#}, đạt target {targetRatio:P0}, "
              + $"evidence đúng mức yêu cầu {requiredRatio:P0}, evidence mức hard {hardRatio:P0}."
            : $"Đạt {achieved.Level}: overall {overall:0.#} ≥ {achieved.OverallThreshold:0.#}, "
              + $"đạt target {targetRatio:P0} ≥ {achieved.TargetMetRatio:P0}, "
              + $"evidence đúng mức yêu cầu {requiredRatio:P0} ≥ {achieved.RequiredDifficultyRatio:P0}, "
              + $"evidence mức hard {hardRatio:P0} ≥ {achieved.HardEvidenceRatio:P0}.";

        return new LevelResolution(achieved?.Level, targetRatio, requiredRatio, hardRatio, explanation);
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
