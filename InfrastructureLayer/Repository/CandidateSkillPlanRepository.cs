using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class CandidateSkillPlanRepository : ICandidateSkillPlanRepository
{
    private readonly Database.AppDbContext _db;

    public CandidateSkillPlanRepository(Database.AppDbContext db)
    {
        _db = db;
    }

    public Task<CandidateSkillPlan?> GetByCandidateUserIdAsync(Guid candidateUserId)
        => _db.CandidateSkillPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.CandidateUserId == candidateUserId && p.IsActive);

    public async Task AddAsync(CandidateSkillPlan plan)
    {
        await _db.CandidateSkillPlans.AddAsync(plan);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(CandidateSkillPlan plan)
    {
        // Luôn reload + thay items bằng ExecuteDelete rồi Add lại.
        // Tránh DbUpdateConcurrencyException từ Items.Clear() / Update(graph).
        var desired = plan.Items.Select(CloneItem).ToList();

        var tracked = await _db.CandidateSkillPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == plan.Id);

        if (tracked is null)
        {
            plan.Items = desired;
            await _db.CandidateSkillPlans.AddAsync(plan);
            await _db.SaveChangesAsync();
            return;
        }

        tracked.FrameworkId = plan.FrameworkId;
        tracked.ResolutionMode = plan.ResolutionMode;
        tracked.RoleFamilyKey = plan.RoleFamilyKey;
        tracked.ActiveBlueprintJson = plan.ActiveBlueprintJson;
        tracked.TargetLevel = plan.TargetLevel;
        tracked.SourceDiagnosticSetId = plan.SourceDiagnosticSetId;
        tracked.OverallReadiness = plan.OverallReadiness;
        tracked.ReadinessStatus = plan.ReadinessStatus;
        tracked.AchievedLevel = plan.AchievedLevel;
        tracked.LastAssessmentId = plan.LastAssessmentId;
        tracked.Status = plan.Status;
        tracked.UpdatedAt = DateTime.UtcNow;

        await _db.CandidateSkillPlanItems
            .Where(i => i.PlanId == tracked.Id)
            .ExecuteDeleteAsync();

        foreach (var entry in _db.ChangeTracker.Entries<CandidateSkillPlanItem>()
                     .Where(e => e.Entity.PlanId == tracked.Id)
                     .ToList())
            entry.State = EntityState.Detached;
        tracked.Items.Clear();

        foreach (var item in desired)
        {
            item.PlanId = tracked.Id;
            tracked.Items.Add(item);
        }

        await _db.SaveChangesAsync();

        if (!ReferenceEquals(plan, tracked))
        {
            plan.Items.Clear();
            foreach (var saved in tracked.Items)
                plan.Items.Add(saved);
            plan.OverallReadiness = tracked.OverallReadiness;
            plan.ReadinessStatus = tracked.ReadinessStatus;
            plan.AchievedLevel = tracked.AchievedLevel;
            plan.UpdatedAt = tracked.UpdatedAt;
        }
    }

    private static CandidateSkillPlanItem CloneItem(CandidateSkillPlanItem i) => new()
    {
        Skill = i.Skill,
        BaselineScore = i.BaselineScore,
        CurrentScore = i.CurrentScore,
        TargetScore = i.TargetScore,
        Status = i.Status,
        LastSessionId = i.LastSessionId,
        DemonstratedDifficulty = i.DemonstratedDifficulty,
        ImportanceWeight = i.ImportanceWeight,
        SourceAssessmentId = i.SourceAssessmentId,
        UpdatedFromKind = i.UpdatedFromKind,
        SourceMode = i.SourceMode
    };
}
