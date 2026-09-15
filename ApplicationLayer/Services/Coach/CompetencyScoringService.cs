using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-447: công thức competency deterministic — LLM chỉ cung cấp dimension scores.
/// </summary>
public static class CompetencyScoringService
{
    public sealed record AnswerInput(
        string Skill,
        string Difficulty,
        double? Correctness,
        double? Relevance,
        double? Clarity,
        bool EvaluationSucceeded);

    public sealed record SkillScoreResult(
        string Skill,
        double SkillScore,
        double TargetScore,
        double Gap,
        double ImportanceWeight,
        string? DemonstratedDifficulty,
        IReadOnlyList<object> Evidence);

    public sealed record OverallResult(
        double OverallReadiness,
        string ReadinessStatus,
        IReadOnlyList<SkillScoreResult> Skills,
        bool MeetsJuniorReadyRule);

    public static double ComputeAnswerScore(
        CompetencyScoringPolicy policy,
        double correctness,
        double relevance,
        double clarity)
    {
        var raw =
            Clamp01to100(correctness) * policy.CorrectnessWeight
            + Clamp01to100(relevance) * policy.RelevanceWeight
            + Clamp01to100(clarity) * policy.ClarityWeight;
        return Math.Round(raw, 2);
    }

    public static double GetDifficultyWeight(CompetencyScoringPolicy policy, string? difficulty)
    {
        var d = (difficulty ?? "").Trim().ToLowerInvariant();
        return d switch
        {
            QuestionDifficultyLevel.Easy => policy.EasyDifficultyWeight,
            QuestionDifficultyLevel.Hard => policy.HardDifficultyWeight,
            _ => policy.MediumDifficultyWeight
        };
    }

    public static int DifficultyRank(string? difficulty)
    {
        var d = (difficulty ?? "").Trim().ToLowerInvariant();
        return d switch
        {
            QuestionDifficultyLevel.Easy => 1,
            QuestionDifficultyLevel.Hard => 3,
            _ => 2
        };
    }

    public static string? MaxDemonstratedDifficulty(IEnumerable<string?> difficulties)
    {
        string? best = null;
        var bestRank = 0;
        foreach (var d in difficulties)
        {
            var rank = DifficultyRank(d);
            if (rank > bestRank)
            {
                bestRank = rank;
                best = (d ?? QuestionDifficultyLevel.Medium).Trim().ToLowerInvariant();
            }
        }
        return best;
    }

    public static double ComputeSkillScore(
        CompetencyScoringPolicy policy,
        IEnumerable<(double AnswerScore, string Difficulty)> answers)
    {
        double num = 0;
        double den = 0;
        foreach (var (answerScore, difficulty) in answers)
        {
            var w = GetDifficultyWeight(policy, difficulty);
            num += answerScore * w;
            den += w;
        }
        if (den <= 0) return 0;
        return Math.Round(num / den, 2);
    }

    public static string ResolveReadinessStatus(CompetencyScoringPolicy policy, double overall)
    {
        if (overall < policy.DevelopingMaxExclusive) return CompetencyReadinessStatus.Developing;
        if (overall < policy.NearTargetMaxExclusive) return CompetencyReadinessStatus.NearTarget;
        if (overall < policy.ReadyMaxExclusive) return CompetencyReadinessStatus.Ready;
        return CompetencyReadinessStatus.Strong;
    }

    public static OverallResult ComputeOverall(
        CompetencyScoringPolicy policy,
        IReadOnlyList<CompetencyFrameworkSkill> frameworkSkills,
        IReadOnlyList<AnswerInput> answers)
    {
        var bySkill = answers
            .Where(a => !string.IsNullOrWhiteSpace(a.Skill))
            .GroupBy(a => NormalizeSkill(a.Skill));

        var results = new List<SkillScoreResult>();
        foreach (var fw in frameworkSkills.OrderBy(s => s.SortOrder))
        {
            var key = NormalizeSkill(fw.Skill);
            var group = bySkill.FirstOrDefault(g => g.Key == key);
            var scored = new List<(double Score, string Diff)>();
            var evidence = new List<object>();
            var diffs = new List<string?>();

            if (group is not null)
            {
                foreach (var a in group)
                {
                    double answerScore;
                    if (!a.EvaluationSucceeded)
                    {
                        answerScore = 0;
                    }
                    else
                    {
                        answerScore = ComputeAnswerScore(
                            policy,
                            a.Correctness ?? 0,
                            a.Relevance ?? 0,
                            a.Clarity ?? 0);
                    }
                    scored.Add((answerScore, a.Difficulty));
                    diffs.Add(a.Difficulty);
                    evidence.Add(new
                    {
                        difficulty = a.Difficulty,
                        answerScore,
                        correctness = a.Correctness,
                        relevance = a.Relevance,
                        clarity = a.Clarity,
                        succeeded = a.EvaluationSucceeded
                    });
                }
            }

            var skillScore = scored.Count == 0 ? 0 : ComputeSkillScore(policy, scored);
            var gap = Math.Round(fw.TargetScore - skillScore, 2);
            results.Add(new SkillScoreResult(
                fw.Skill,
                skillScore,
                fw.TargetScore,
                gap,
                fw.ImportanceWeight,
                MaxDemonstratedDifficulty(diffs),
                evidence));
        }

        var weightSum = frameworkSkills.Sum(s => s.ImportanceWeight);
        if (weightSum <= 0) weightSum = 1;
        var overall = Math.Round(
            results.Sum(r => r.SkillScore * r.ImportanceWeight) / weightSum, 2);

        var meetsJunior = overall >= policy.OverallReadyThreshold
            && frameworkSkills.Count > 0
            && results.Count(r => r.SkillScore >= r.TargetScore)
               >= Math.Ceiling(frameworkSkills.Count * policy.JuniorReadyCoreSkillRatio);

        // RequiredDifficulty evidence: Junior Ready cần ≥ ratio skills đạt target VÀ demonstrated ≥ required
        var withEvidence = 0;
        foreach (var fw in frameworkSkills)
        {
            var r = results.FirstOrDefault(x => NormalizeSkill(x.Skill) == NormalizeSkill(fw.Skill));
            if (r is null) continue;
            if (r.SkillScore < fw.TargetScore) continue;
            if (DifficultyRank(r.DemonstratedDifficulty) >= DifficultyRank(fw.RequiredDifficulty))
                withEvidence++;
        }
        var meetsEvidence = frameworkSkills.Count == 0
            || withEvidence >= Math.Ceiling(frameworkSkills.Count * policy.JuniorReadyCoreSkillRatio);

        return new OverallResult(
            overall,
            ResolveReadinessStatus(policy, overall),
            results,
            meetsJunior && meetsEvidence);
    }

    public static Dictionary<string, double>? ExtractDimensions(Dictionary<string, double>? raw)
    {
        if (raw is null || raw.Count == 0) return null;
        double? Pick(params string[] keys)
        {
            foreach (var k in keys)
            {
                var hit = raw.FirstOrDefault(kv =>
                    string.Equals(kv.Key, k, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(hit.Key)) return hit.Value;
            }
            return null;
        }

        var correctness = Pick("correctness", "accuracy", "Correctness", "Accuracy");
        var relevance = Pick("relevance", "Relevance");
        var clarity = Pick("clarity", "Clarity");
        if (correctness is null && relevance is null && clarity is null)
            return null;

        return new Dictionary<string, double>
        {
            ["correctness"] = correctness ?? 0,
            ["relevance"] = relevance ?? 0,
            ["clarity"] = clarity ?? 0
        };
    }

    private static double Clamp01to100(double v) => Math.Clamp(v, 0, 100);

    public static string NormalizeSkill(string skill)
        => (skill ?? "").Trim().ToLowerInvariant();
}
