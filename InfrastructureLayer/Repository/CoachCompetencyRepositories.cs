using ApplicationLayer.Interfaces.Repositories;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repository;

public class CompetencyFrameworkRepository : ICompetencyFrameworkRepository
{
    private readonly Database.AppDbContext _db;

    public CompetencyFrameworkRepository(Database.AppDbContext db) => _db = db;

    public Task<CompetencyFramework?> GetByRoleAndLevelAsync(string roleKey, string targetLevel)
        => _db.CompetencyFrameworks
            .Include(f => f.Skills)
            .FirstOrDefaultAsync(f => f.RoleKey == roleKey && f.TargetLevel == targetLevel && f.IsActive);

    public Task<CompetencyFramework?> GetByIdWithSkillsAsync(Guid id)
        => _db.CompetencyFrameworks
            .Include(f => f.Skills)
            .FirstOrDefaultAsync(f => f.Id == id && f.IsActive);

    // SCRUM-453: bỏ hoàn toàn logic match theo tên stack (.NET) khỏi repository.
    // Việc chọn framework từ target role tự do do CompetencyFrameworkResolver làm, dựa trên
    // bảng alias + catalog, nên thêm role/stack mới không cần sửa data access.
    public Task<List<CompetencyFramework>> ListActiveWithSkillsAsync()
        => _db.CompetencyFrameworks
            .Include(f => f.Skills)
            .Where(f => f.IsActive && f.Status == DomainLayer.Constants.CompetencyFrameworkStatus.Active)
            .OrderBy(f => f.RoleKey).ThenBy(f => f.TargetLevel)
            .ToListAsync();

    public Task<List<CompetencyFramework>> ListAllWithSkillsAsync()
        => _db.CompetencyFrameworks
            .Include(f => f.Skills)
            .OrderBy(f => f.RoleKey).ThenBy(f => f.TargetLevel)
            .ToListAsync();

    public Task<List<CompetencyRoleAlias>> ListAliasesAsync()
        => _db.CompetencyRoleAliases
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ToListAsync();

    public async Task<Guid> UpsertFrameworkAsync(
        CompetencyFramework framework,
        IReadOnlyList<CompetencyFrameworkSkill> skills)
    {
        var existing = await _db.CompetencyFrameworks
            .Include(f => f.Skills)
            .FirstOrDefaultAsync(f => f.RoleKey == framework.RoleKey && f.TargetLevel == framework.TargetLevel);

        if (existing is null)
        {
            framework.Skills = skills.ToList();
            await _db.CompetencyFrameworks.AddAsync(framework);
            await _db.SaveChangesAsync();
            return framework.Id;
        }

        existing.DisplayRole = framework.DisplayRole;
        existing.Status = framework.Status;
        existing.Description = framework.Description;
        existing.Technology = framework.Technology;
        existing.StackJson = framework.StackJson;
        existing.Provenance = framework.Provenance;
        existing.SourceRef = framework.SourceRef;
        existing.SourceVersion = framework.SourceVersion;
        existing.IsActive = true;
        existing.UpdatedAt = DateTime.UtcNow;

        // Import là nguồn sự thật cho bộ skill: xoá skill không còn trong file, thêm/cập nhật phần còn lại.
        _db.CompetencyFrameworkSkills.RemoveRange(existing.Skills);
        foreach (var skill in skills)
        {
            skill.FrameworkId = existing.Id;
            await _db.CompetencyFrameworkSkills.AddAsync(skill);
        }

        await _db.SaveChangesAsync();
        return existing.Id;
    }

    public async Task ReplaceAliasesAsync(string roleKey, IReadOnlyList<CompetencyRoleAlias> aliases)
    {
        var old = await _db.CompetencyRoleAliases.Where(a => a.RoleKey == roleKey).ToListAsync();
        _db.CompetencyRoleAliases.RemoveRange(old);
        foreach (var alias in aliases)
            await _db.CompetencyRoleAliases.AddAsync(alias);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(Guid frameworkId, string status)
    {
        var fw = await _db.CompetencyFrameworks.FirstOrDefaultAsync(f => f.Id == frameworkId);
        if (fw is null) return;
        fw.Status = status;
        fw.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<CompetencyScoringPolicy> GetPolicyAsync()
    {
        var policy = await _db.CompetencyScoringPolicies
            .FirstOrDefaultAsync(p => p.Id == CompetencyScoringPolicy.SingletonId && p.IsActive);
        return policy ?? new CompetencyScoringPolicy { Id = CompetencyScoringPolicy.SingletonId };
    }

    public async Task SavePolicyAsync(CompetencyScoringPolicy policy)
    {
        policy.Id = CompetencyScoringPolicy.SingletonId;
        var existing = await _db.CompetencyScoringPolicies
            .FirstOrDefaultAsync(p => p.Id == CompetencyScoringPolicy.SingletonId);
        if (existing is null)
        {
            policy.IsActive = true;
            await _db.CompetencyScoringPolicies.AddAsync(policy);
        }
        else
        {
            existing.CorrectnessWeight = policy.CorrectnessWeight;
            existing.RelevanceWeight = policy.RelevanceWeight;
            existing.ClarityWeight = policy.ClarityWeight;
            existing.EasyDifficultyWeight = policy.EasyDifficultyWeight;
            existing.MediumDifficultyWeight = policy.MediumDifficultyWeight;
            existing.HardDifficultyWeight = policy.HardDifficultyWeight;
            existing.DevelopingMaxExclusive = policy.DevelopingMaxExclusive;
            existing.NearTargetMaxExclusive = policy.NearTargetMaxExclusive;
            existing.ReadyMaxExclusive = policy.ReadyMaxExclusive;
            existing.JuniorReadyCoreSkillRatio = policy.JuniorReadyCoreSkillRatio;
            existing.OverallReadyThreshold = policy.OverallReadyThreshold;
            existing.TargetScoreByLevelJson = policy.TargetScoreByLevelJson;
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }
}

public class CompetencyLevelRuleRepository : ICompetencyLevelRuleRepository
{
    private readonly Database.AppDbContext _db;
    public CompetencyLevelRuleRepository(Database.AppDbContext db) => _db = db;

    public Task<List<CompetencyLevelRule>> ListAsync()
        => _db.CompetencyLevelRules
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToListAsync();

    public async Task UpsertRangeAsync(IReadOnlyList<CompetencyLevelRule> rules)
    {
        foreach (var incoming in rules)
        {
            var existing = await _db.CompetencyLevelRules
                .FirstOrDefaultAsync(r => r.Level == incoming.Level);
            if (existing is null)
            {
                incoming.IsActive = true;
                await _db.CompetencyLevelRules.AddAsync(incoming);
                continue;
            }
            existing.OverallThreshold = incoming.OverallThreshold;
            existing.TargetMetRatio = incoming.TargetMetRatio;
            existing.RequiredDifficultyRatio = incoming.RequiredDifficultyRatio;
            existing.HardEvidenceRatio = incoming.HardEvidenceRatio;
            existing.SortOrder = incoming.SortOrder;
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }
}

public class CandidateAssessmentRepository : ICandidateAssessmentRepository
{
    private readonly Database.AppDbContext _db;
    public CandidateAssessmentRepository(Database.AppDbContext db) => _db = db;

    public async Task AddAsync(CandidateAssessment assessment)
    {
        await _db.CandidateAssessments.AddAsync(assessment);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(CandidateAssessment assessment)
    {
        DetachFrameworkGraph(assessment);
        // Chỉ ghi scalar — không SaveChanges graph SkillResults (child mới dễ bị Modified → UPDATE 0 rows).
        var now = DateTime.UtcNow;
        var updated = await _db.CandidateAssessments
            .Where(a => a.Id == assessment.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.OverallReadiness, assessment.OverallReadiness)
                .SetProperty(a => a.ReadinessStatus, assessment.ReadinessStatus)
                .SetProperty(a => a.ExplanationJson, assessment.ExplanationJson)
                .SetProperty(a => a.Status, assessment.Status)
                .SetProperty(a => a.PracticeSessionId, assessment.PracticeSessionId)
                .SetProperty(a => a.QuestionSetId, assessment.QuestionSetId)
                .SetProperty(a => a.ScopeSkillsJson, assessment.ScopeSkillsJson)
                .SetProperty(a => a.PersonalSetJobId, assessment.PersonalSetJobId)
                .SetProperty(a => a.UpdatedAt, now));
        if (updated == 0)
            throw new InvalidOperationException($"Assessment {assessment.Id} không tồn tại để cập nhật.");
        assessment.UpdatedAt = now;
    }

    public async Task SaveScoredAssessmentAsync(
        CandidateAssessment assessment,
        IReadOnlyList<CandidateAssessmentSkillResult> newSkillResults)
    {
        DetachFrameworkGraph(assessment);
        // Tách parent+child khỏi tracker TRƯỚC khi xóa/ghi. Nếu còn tracked, Clear()/fix-up
        // đánh child Deleted rồi SaveChanges DELETE 0 rows → DbUpdateConcurrencyException.
        DetachAssessmentSkillGraph(assessment.Id);

        var now = DateTime.UtcNow;
        var updated = await _db.CandidateAssessments
            .Where(a => a.Id == assessment.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, assessment.Status)
                .SetProperty(a => a.PracticeSessionId, assessment.PracticeSessionId)
                .SetProperty(a => a.QuestionSetId, assessment.QuestionSetId)
                .SetProperty(a => a.ScopeSkillsJson, assessment.ScopeSkillsJson)
                .SetProperty(a => a.UpdatedAt, now));
        if (updated == 0)
            throw new InvalidOperationException($"Assessment {assessment.Id} không tồn tại để chấm điểm.");

        await _db.CandidateAssessmentSkillResults
            .Where(r => r.AssessmentId == assessment.Id)
            .ExecuteDeleteAsync();

        var inserted = newSkillResults.Select(r => new CandidateAssessmentSkillResult
        {
            AssessmentId = assessment.Id,
            Skill = r.Skill,
            SkillScore = r.SkillScore,
            TargetScore = r.TargetScore,
            Gap = r.Gap,
            ImportanceWeight = r.ImportanceWeight,
            DemonstratedDifficulty = r.DemonstratedDifficulty,
            EvidenceJson = string.IsNullOrWhiteSpace(r.EvidenceJson) ? "[]" : r.EvidenceJson
        }).ToList();

        if (inserted.Count > 0)
        {
            await _db.CandidateAssessmentSkillResults.AddRangeAsync(inserted);
            await _db.SaveChangesAsync();
        }

        assessment.UpdatedAt = now;
        assessment.SkillResults.Clear();
        foreach (var row in inserted)
            assessment.SkillResults.Add(row);
    }

    public async Task UpdateReadinessAsync(
        Guid assessmentId,
        double? overallReadiness,
        string? readinessStatus,
        string? explanationJson)
    {
        var updated = await _db.CandidateAssessments
            .Where(a => a.Id == assessmentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.OverallReadiness, overallReadiness)
                .SetProperty(a => a.ReadinessStatus, readinessStatus)
                .SetProperty(a => a.ExplanationJson, explanationJson)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));

        if (updated == 0)
            throw new InvalidOperationException($"Assessment {assessmentId} không tồn tại để cập nhật readiness.");
    }

    private void DetachFrameworkGraph(CandidateAssessment assessment)
    {
        if (assessment.Framework is null) return;
        var fwEntry = _db.Entry(assessment.Framework);
        if (fwEntry.State != EntityState.Detached)
            fwEntry.State = EntityState.Unchanged;
        foreach (var s in assessment.Framework.Skills)
        {
            var se = _db.Entry(s);
            if (se.State != EntityState.Detached)
                se.State = EntityState.Unchanged;
        }
    }

    private void DetachAssessmentSkillGraph(Guid assessmentId)
    {
        foreach (var entry in _db.ChangeTracker.Entries<CandidateAssessmentSkillResult>()
                     .Where(e => e.Entity.AssessmentId == assessmentId
                                 || e.Entity.Assessment?.Id == assessmentId)
                     .ToList())
            entry.State = EntityState.Detached;

        foreach (var entry in _db.ChangeTracker.Entries<CandidateAssessment>()
                     .Where(e => e.Entity.Id == assessmentId)
                     .ToList())
        {
            entry.Collection(a => a.SkillResults).CurrentValue = new List<CandidateAssessmentSkillResult>();
            entry.State = EntityState.Detached;
        }
    }

    public Task<CandidateAssessment?> GetByIdAsync(Guid id)
        => _db.CandidateAssessments
            .Include(a => a.SkillResults)
            .Include(a => a.Framework)!.ThenInclude(f => f!.Skills)
            .FirstOrDefaultAsync(a => a.Id == id);

    public Task<CandidateAssessment?> GetLatestScoredAsync(Guid candidateUserId)
        => _db.CandidateAssessments
            .Include(a => a.SkillResults)
            .Include(a => a.Framework)
            .Where(a => a.CandidateUserId == candidateUserId
                        && a.Status == DomainLayer.Constants.CandidateAssessmentStatus.Scored)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

    public Task<CandidateAssessment?> GetByQuestionSetIdAsync(Guid questionSetId)
        => _db.CandidateAssessments
            .Include(a => a.SkillResults)
            .Include(a => a.Framework)!.ThenInclude(f => f!.Skills)
            .FirstOrDefaultAsync(a => a.QuestionSetId == questionSetId);

    public Task<CandidateAssessment?> GetByJobIdAsync(Guid jobId)
        => _db.CandidateAssessments
            .Include(a => a.SkillResults)
            .Include(a => a.Framework)!.ThenInclude(f => f!.Skills)
            .FirstOrDefaultAsync(a => a.PersonalSetJobId == jobId);

    public Task<List<CandidateAssessment>> ListByCandidateAsync(Guid candidateUserId)
        => _db.CandidateAssessments
            .Include(a => a.SkillResults)
            .Where(a => a.CandidateUserId == candidateUserId && a.IsActive)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
}

public class CandidateRoadmapRepository : ICandidateRoadmapRepository
{
    private readonly Database.AppDbContext _db;
    public CandidateRoadmapRepository(Database.AppDbContext db) => _db = db;

    public async Task AddRangeAsync(IEnumerable<CandidateRoadmap> roadmaps)
    {
        var list = roadmaps.ToList();
        foreach (var roadmap in list)
        {
            // Cắt navigation catalog/assessment — tránh SaveChanges đi theo graph đã ExecuteDelete.
            roadmap.Framework = null;
            roadmap.SourceAssessment = null;
            foreach (var item in roadmap.Items)
                item.RoadmapNode = null;
        }
        await _db.CandidateRoadmaps.AddRangeAsync(list);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(CandidateRoadmap roadmap)
    {
        var now = DateTime.UtcNow;
        roadmap.UpdatedAt = now;

        foreach (var item in roadmap.Items)
        {
            if (item.RoadmapNode is not null)
            {
                var nodeEntry = _db.Entry(item.RoadmapNode);
                if (nodeEntry.State == EntityState.Modified)
                    nodeEntry.State = EntityState.Unchanged;
            }
        }

        var entry = _db.Entry(roadmap);
        if (entry.State == EntityState.Detached)
        {
            var updated = await _db.CandidateRoadmaps
                .Where(r => r.Id == roadmap.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.IsActive, roadmap.IsActive)
                    .SetProperty(r => r.CurrentScore, roadmap.CurrentScore)
                    .SetProperty(r => r.TargetScore, roadmap.TargetScore)
                    .SetProperty(r => r.Gap, roadmap.Gap)
                    .SetProperty(r => r.PriorityScore, roadmap.PriorityScore)
                    .SetProperty(r => r.Kind, roadmap.Kind)
                    .SetProperty(r => r.Priority, roadmap.Priority)
                    .SetProperty(r => r.Status, roadmap.Status)
                    .SetProperty(r => r.SourceAssessmentId, roadmap.SourceAssessmentId)
                    .SetProperty(r => r.ExplanationJson, roadmap.ExplanationJson)
                    .SetProperty(r => r.UpdatedAt, now));
            if (updated == 0)
                throw new InvalidOperationException($"Roadmap {roadmap.Id} không tồn tại để cập nhật.");

            foreach (var item in roadmap.Items)
            {
                await _db.CandidateRoadmapItems
                    .Where(i => i.Id == item.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(i => i.Status, item.Status)
                        .SetProperty(i => i.DrillSessionId, item.DrillSessionId)
                        .SetProperty(i => i.DrillQuestionSetId, item.DrillQuestionSetId)
                        .SetProperty(i => i.DrillScore, item.DrillScore)
                        .SetProperty(i => i.UpdatedAt, now));
            }
            return;
        }

        if (roadmap.Framework is not null)
        {
            var fwEntry = _db.Entry(roadmap.Framework);
            if (fwEntry.State == EntityState.Modified)
                fwEntry.State = EntityState.Unchanged;
        }
        if (roadmap.SourceAssessment is not null)
        {
            var aEntry = _db.Entry(roadmap.SourceAssessment);
            if (aEntry.State == EntityState.Modified)
                aEntry.State = EntityState.Unchanged;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>Archive IsActive=true bằng ExecuteUpdate rồi detach graph khỏi tracker.</summary>
    public async Task ArchiveActiveByCandidateAsync(Guid candidateUserId)
    {
        await _db.CandidateRoadmaps
            .Where(r => r.CandidateUserId == candidateUserId && r.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.IsActive, false)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow));

        var roadmapIds = _db.ChangeTracker.Entries<CandidateRoadmap>()
            .Where(e => e.Entity.CandidateUserId == candidateUserId)
            .Select(e => e.Entity.Id)
            .ToHashSet();

        foreach (var entry in _db.ChangeTracker.Entries<CandidateRoadmapItem>()
                     .Where(e => roadmapIds.Contains(e.Entity.RoadmapId)
                                 || (e.Entity.Roadmap != null && e.Entity.Roadmap.CandidateUserId == candidateUserId))
                     .ToList())
            entry.State = EntityState.Detached;

        foreach (var entry in _db.ChangeTracker.Entries<CandidateRoadmap>()
                     .Where(e => e.Entity.CandidateUserId == candidateUserId)
                     .ToList())
        {
            entry.Collection(r => r.Items).CurrentValue = new List<CandidateRoadmapItem>();
            entry.State = EntityState.Detached;
        }
    }

    public async Task RestoreActiveAsync(IEnumerable<Guid> roadmapIds)
    {
        var ids = roadmapIds.ToList();
        if (ids.Count == 0) return;
        await _db.CandidateRoadmaps
            .Where(r => ids.Contains(r.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.IsActive, true)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow));
    }

    public Task<List<CandidateRoadmap>> ListByCandidateAsync(Guid candidateUserId)
        => _db.CandidateRoadmaps
            .Include(r => r.Items)
                .ThenInclude(i => i.RoadmapNode)
            .Where(r => r.CandidateUserId == candidateUserId && r.IsActive)
            .OrderByDescending(r => r.PriorityScore)
            .ThenByDescending(r => r.Gap)
            .ToListAsync();

    public Task<List<CandidateRoadmap>> ListAllByCandidateAsync(Guid candidateUserId)
        => _db.CandidateRoadmaps
            .Include(r => r.Items)
            .Where(r => r.CandidateUserId == candidateUserId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public Task<CandidateRoadmap?> GetByIdAsync(Guid id)
        => _db.CandidateRoadmaps
            .Include(r => r.Items)
                .ThenInclude(i => i.RoadmapNode)
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
}

public class RoadmapNodeRepository : IRoadmapNodeRepository
{
    private readonly Database.AppDbContext _db;
    public RoadmapNodeRepository(Database.AppDbContext db) => _db = db;

    public Task<List<RoadmapNode>> ListBySkillAsync(string roleKey, string level, string skill)
        => _db.RoadmapNodes
            .Where(n => n.IsActive
                        && n.RoleKey == roleKey
                        && n.Level == level
                        && n.Skill == skill)
            .OrderBy(n => n.SortOrder)
            .ToListAsync();

    public Task<List<RoadmapNode>> ListAllAsync()
        => _db.RoadmapNodes
            .OrderBy(n => n.RoleKey).ThenBy(n => n.Level).ThenBy(n => n.Skill).ThenBy(n => n.SortOrder)
            .ToListAsync();

    public async Task UpsertRangeAsync(IReadOnlyList<RoadmapNode> nodes)
    {
        foreach (var node in nodes)
        {
            var existing = await _db.RoadmapNodes.FirstOrDefaultAsync(n =>
                n.RoleKey == node.RoleKey
                && n.Level == node.Level
                && n.Skill == node.Skill
                && n.Topic == node.Topic);
            if (existing is null)
            {
                await _db.RoadmapNodes.AddAsync(node);
                continue;
            }

            existing.Technology = node.Technology;
            existing.Subtopic = node.Subtopic;
            existing.Importance = node.Importance;
            existing.PrerequisitesJson = node.PrerequisitesJson;
            existing.NextTopicsJson = node.NextTopicsJson;
            existing.SourceTitle = node.SourceTitle;
            existing.SourceUrl = node.SourceUrl;
            existing.SourceVersion = node.SourceVersion;
            existing.KnowledgeDocumentId = node.KnowledgeDocumentId ?? existing.KnowledgeDocumentId;
            existing.SortOrder = node.SortOrder;
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var node = await _db.RoadmapNodes.FirstOrDefaultAsync(n => n.Id == id);
        if (node is null) return;
        _db.RoadmapNodes.Remove(node);
        await _db.SaveChangesAsync();
    }

    public async Task<HashSet<string>> ListKnownRoleKeysAsync()
    {
        var keys = await _db.CompetencyFrameworks
            .Where(f => f.IsActive)
            .Select(f => f.RoleKey)
            .Distinct()
            .ToListAsync();
        return keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

public class CompetencyRoleFamilyRepository : ICompetencyRoleFamilyRepository
{
    private readonly Database.AppDbContext _db;
    public CompetencyRoleFamilyRepository(Database.AppDbContext db) => _db = db;

    public Task<List<CompetencyRoleFamily>> ListActiveAsync()
        => _db.CompetencyRoleFamilies
            .Where(f => f.IsActive && f.Status == DomainLayer.Constants.CompetencyFrameworkStatus.Active)
            .OrderBy(f => f.SortOrder)
            .ToListAsync();

    public Task<List<CompetencyRoleFamilyAlias>> ListAliasesAsync()
        => _db.CompetencyRoleFamilyAliases
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ToListAsync();

    public Task<List<CompetencyRoleFamily>> ListAllAsync()
        => _db.CompetencyRoleFamilies
            .Include(f => f.Aliases)
            .OrderBy(f => f.SortOrder)
            .ToListAsync();

    public async Task UpsertFamilyAsync(CompetencyRoleFamily family, IReadOnlyList<CompetencyRoleFamilyAlias> aliases)
    {
        var existing = await _db.CompetencyRoleFamilies
            .FirstOrDefaultAsync(f => f.FamilyKey == family.FamilyKey);
        if (existing is null)
        {
            await _db.CompetencyRoleFamilies.AddAsync(family);
        }
        else
        {
            existing.DisplayName = family.DisplayName;
            existing.Status = family.Status;
            existing.SortOrder = family.SortOrder;
            existing.Description = family.Description;
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        var old = await _db.CompetencyRoleFamilyAliases
            .Where(a => a.FamilyKey == family.FamilyKey)
            .ToListAsync();
        _db.CompetencyRoleFamilyAliases.RemoveRange(old);
        foreach (var alias in aliases)
            await _db.CompetencyRoleFamilyAliases.AddAsync(alias);
        await _db.SaveChangesAsync();
    }
}
