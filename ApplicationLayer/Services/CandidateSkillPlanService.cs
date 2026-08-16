using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Services;

public class CandidateSkillPlanService : ICandidateSkillPlanService
{
    private const double DefaultTarget = 70;

    private readonly ICandidateSkillPlanRepository _plans;
    private readonly IAiFeedbackRepository _feedbacks;

    public CandidateSkillPlanService(ICandidateSkillPlanRepository plans, IAiFeedbackRepository feedbacks)
    {
        _plans = plans;
        _feedbacks = feedbacks;
    }

    public async Task<CandidateSkillPlanDto?> GetAsync(Guid candidateUserId)
    {
        var plan = await _plans.GetByCandidateUserIdAsync(candidateUserId);
        return plan is null ? null : Map(plan);
    }

    public async Task UpsertFromSessionAsync(PracticeSession session, CandidatePersonalSetJob job)
    {
        var scores = await _feedbacks.GetSkillAveragesBySessionAsync(session.Id);
        var scoreByNorm = scores
            .Where(s => !string.IsNullOrWhiteSpace(s.Skill))
            .GroupBy(s => SkillFitCalculator.Normalize(s.Skill))
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => g.Average(x => x.AvgScore));

        var plan = await _plans.GetByCandidateUserIdAsync(session.CandidateUserId);
        var isNew = plan is null;
        plan ??= new CandidateSkillPlan
        {
            CandidateUserId = session.CandidateUserId,
            Status = CandidateSkillPlanStatus.Active
        };

        if (job.Purpose == CandidatePersonalSetPurpose.CvDiagnostic)
        {
            plan.SourceDiagnosticSetId = session.QuestionSetId;
            var cvSkills = DeserializeSkills(job.CvSkillsJson);
            foreach (var raw in cvSkills)
            {
                UpsertItem(plan, raw, scoreByNorm, session.Id, createIfMissing: true);
            }
        }
        else if (job.Purpose == CandidatePersonalSetPurpose.CvDrill)
        {
            var focus = DeserializeSkills(job.FocusSkillsJson);
            var skill = focus.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(skill))
                return;
            UpsertItem(plan, skill, scoreByNorm, session.Id, createIfMissing: false);
        }
        else
        {
            return;
        }

        if (isNew)
            await _plans.AddAsync(plan);
        else
            await _plans.UpdateAsync(plan);
    }

    private static void UpsertItem(
        CandidateSkillPlan plan,
        string rawSkill,
        Dictionary<string, double> scoreByNorm,
        Guid sessionId,
        bool createIfMissing)
    {
        var key = SkillFitCalculator.Normalize(rawSkill);
        if (key.Length == 0)
            return;

        var item = plan.Items.FirstOrDefault(i => SkillFitCalculator.Normalize(i.Skill) == key);
        if (item is null)
        {
            if (!createIfMissing)
                return;
            item = new CandidateSkillPlanItem
            {
                Skill = key.Length > 200 ? key[..200] : key,
                TargetScore = DefaultTarget,
                Status = CandidateSkillPlanItemStatus.Pending
            };
            plan.Items.Add(item);
        }

        if (scoreByNorm.TryGetValue(key, out var score))
        {
            item.CurrentScore = Math.Round(score, 1);
            item.BaselineScore ??= item.CurrentScore;
            item.LastSessionId = sessionId;
        }

        item.Status = ResolveStatus(item.CurrentScore, item.TargetScore);
        item.UpdatedAt = DateTime.UtcNow;
    }

    public static string ResolveStatus(double? current, double target)
    {
        if (!current.HasValue)
            return CandidateSkillPlanItemStatus.Pending;
        return current.Value >= target
            ? CandidateSkillPlanItemStatus.Done
            : CandidateSkillPlanItemStatus.InProgress;
    }

    private static List<string> DeserializeSkills(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return new List<string>();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (System.Text.Json.JsonException)
        {
            return new List<string>();
        }
    }

    private static CandidateSkillPlanDto Map(CandidateSkillPlan plan) => new()
    {
        Id = plan.Id,
        SourceDiagnosticSetId = plan.SourceDiagnosticSetId,
        Status = plan.Status,
        Items = plan.Items
            .OrderBy(i => i.Status == CandidateSkillPlanItemStatus.Done ? 1 : 0)
            .ThenBy(i => i.CurrentScore ?? 999)
            .ThenBy(i => i.Skill)
            .Select(i => new CandidateSkillPlanItemDto
            {
                Id = i.Id,
                Skill = i.Skill,
                BaselineScore = i.BaselineScore,
                CurrentScore = i.CurrentScore,
                TargetScore = i.TargetScore,
                Status = i.Status
            })
            .ToList()
    };
}
