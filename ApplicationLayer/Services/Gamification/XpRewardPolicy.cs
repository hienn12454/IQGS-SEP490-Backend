using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Settings;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services.Gamification;

public class XpRewardPolicy : IXpRewardPolicy
{
    private readonly GamificationOptions _options;
    private readonly List<ScoreBonusTier> _tiersDesc;

    public XpRewardPolicy(IOptions<GamificationOptions> options)
    {
        _options = options.Value;
        // Sắp giảm dần theo MinScoreInclusive — tier đầu tiên score đạt được là tier áp dụng.
        _tiersDesc = _options.ScoreBonusTiers
            .OrderByDescending(t => t.MinScoreInclusive)
            .ToList();
    }

    public int QuestionCompletedXp => _options.QuestionCompletedXp;
    public int QuestionSetCompletedXp => _options.QuestionSetCompletedXp;
    public int ImprovementBonusXp => _options.ImprovementBonusXp;

    public int GetScoreBonus(double score)
    {
        if (score < 0 || score > 100)
            throw new ArgumentOutOfRangeException(nameof(score), score, "Score phải trong khoảng [0, 100].");

        foreach (var tier in _tiersDesc)
        {
            if (score >= tier.MinScoreInclusive)
                return tier.Xp;
        }

        return 0;
    }

    public bool IsImprovementEligible(double previousScore, double currentScore)
        => currentScore - previousScore >= _options.ImprovementBonusMinDeltaPoints;

    public bool TryGetStreakMilestoneXp(int streakDays, out int xp)
        => _options.StreakMilestoneXp.TryGetValue(streakDays, out xp);
}
