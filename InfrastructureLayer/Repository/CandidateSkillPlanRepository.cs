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
        // ExecuteUpdate/ExecuteDelete + INSERT — không Clear()/SaveChanges trên graph tracked.
        // Clear() sau ExecuteDelete đánh child Deleted → DELETE 0 rows → DbUpdateConcurrencyException.
        var desired = plan.Items.Select(CloneItem).ToList();
        DetachPlanGraph(plan);

        var now = DateTime.UtcNow;
        var updated = await _db.CandidateSkillPlans
            .Where(p => p.Id == plan.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.FrameworkId, plan.FrameworkId)
                .SetProperty(p => p.ResolutionMode, plan.ResolutionMode)
                .SetProperty(p => p.RoleFamilyKey, plan.RoleFamilyKey)
                .SetProperty(p => p.ActiveBlueprintJson, plan.ActiveBlueprintJson)
                .SetProperty(p => p.TargetLevel, plan.TargetLevel)
                .SetProperty(p => p.SourceDiagnosticSetId, plan.SourceDiagnosticSetId)
                .SetProperty(p => p.OverallReadiness, plan.OverallReadiness)
                .SetProperty(p => p.ReadinessStatus, plan.ReadinessStatus)
                .SetProperty(p => p.AchievedLevel, plan.AchievedLevel)
                .SetProperty(p => p.LastAssessmentId, plan.LastAssessmentId)
                .SetProperty(p => p.Status, plan.Status)
                .SetProperty(p => p.UpdatedAt, now));

        if (updated == 0)
        {
            plan.Items = desired;
            await _db.CandidateSkillPlans.AddAsync(plan);
            await _db.SaveChangesAsync();
            return;
        }

        await _db.CandidateSkillPlanItems
            .Where(i => i.PlanId == plan.Id)
            .ExecuteDeleteAsync();

        foreach (var item in desired)
            item.PlanId = plan.Id;

        if (desired.Count > 0)
        {
            await _db.CandidateSkillPlanItems.AddRangeAsync(desired);
            await _db.SaveChangesAsync();
        }

        plan.Items.Clear();
        foreach (var item in desired)
            plan.Items.Add(item);
        plan.UpdatedAt = now;
    }

    private void DetachPlanGraph(CandidateSkillPlan plan)
    {
        foreach (var entry in _db.ChangeTracker.Entries<CandidateSkillPlanItem>()
                     .Where(e => e.Entity.PlanId == plan.Id
                                 || e.Entity.Plan?.Id == plan.Id
                                 || ReferenceEquals(e.Entity.Plan, plan))
                     .ToList())
            entry.State = EntityState.Detached;

        foreach (var entry in _db.ChangeTracker.Entries<CandidateSkillPlan>()
                     .Where(e => e.Entity.Id == plan.Id)
                     .ToList())
        {
            entry.Collection(p => p.Items).CurrentValue = new List<CandidateSkillPlanItem>();
            entry.State = EntityState.Detached;
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
