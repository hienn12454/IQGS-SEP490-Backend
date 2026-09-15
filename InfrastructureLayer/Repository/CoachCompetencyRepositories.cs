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
        assessment.UpdatedAt = DateTime.UtcNow;
        DetachFrameworkGraph(assessment);

        var entry = _db.Entry(assessment);
        if (entry.State == EntityState.Detached)
        {
            // Không dùng DbSet.Update(graph): child SkillResults mới bị mark Modified
            // → UPDATE 0 rows → DbUpdateConcurrencyException.
            var tracked = await _db.CandidateAssessments
                .Include(a => a.SkillResults)
                .FirstOrDefaultAsync(a => a.Id == assessment.Id);
            if (tracked is null)
                throw new InvalidOperationException($"Assessment {assessment.Id} không tồn tại để cập nhật.");

            CopyAssessmentScalars(assessment, tracked);
            await _db.SaveChangesAsync();
            return;
        }

        EnsureSkillResultsNotFalseModified(assessment);
        await _db.SaveChangesAsync();
    }

    public async Task SaveScoredAssessmentAsync(
        CandidateAssessment assessment,
        IReadOnlyList<CandidateAssessmentSkillResult> newSkillResults)
    {
        DetachFrameworkGraph(assessment);

        var tracked = await _db.CandidateAssessments
            .Include(a => a.SkillResults)
            .FirstOrDefaultAsync(a => a.Id == assessment.Id)
            ?? throw new InvalidOperationException($"Assessment {assessment.Id} không tồn tại để chấm điểm.");

        // ExecuteDelete không ném concurrency khi 0 rows — an toàn hơn RemoveRange trên tracker.
        await _db.CandidateAssessmentSkillResults
            .Where(r => r.AssessmentId == tracked.Id)
            .ExecuteDeleteAsync();

        foreach (var entry in _db.ChangeTracker.Entries<CandidateAssessmentSkillResult>()
                     .Where(e => e.Entity.AssessmentId == tracked.Id)
                     .ToList())
            entry.State = EntityState.Detached;
        tracked.SkillResults.Clear();

        tracked.Status = assessment.Status;
        tracked.PracticeSessionId = assessment.PracticeSessionId;
        tracked.QuestionSetId = assessment.QuestionSetId;
        tracked.ScopeSkillsJson = assessment.ScopeSkillsJson;
        tracked.OverallReadiness = assessment.OverallReadiness;
        tracked.ReadinessStatus = assessment.ReadinessStatus;
        tracked.ExplanationJson = assessment.ExplanationJson;
        tracked.UpdatedAt = DateTime.UtcNow;

        foreach (var r in newSkillResults)
        {
            tracked.SkillResults.Add(new CandidateAssessmentSkillResult
            {
                AssessmentId = tracked.Id,
                Skill = r.Skill,
                SkillScore = r.SkillScore,
                TargetScore = r.TargetScore,
                Gap = r.Gap,
                ImportanceWeight = r.ImportanceWeight,
                DemonstratedDifficulty = r.DemonstratedDifficulty,
                EvidenceJson = r.EvidenceJson
            });
        }

        await _db.SaveChangesAsync();

        // Đồng bộ collection trên instance caller dùng tiếp (merge/roadmap).
        if (!ReferenceEquals(assessment, tracked))
        {
            assessment.SkillResults.Clear();
            foreach (var saved in tracked.SkillResults)
                assessment.SkillResults.Add(saved);
            assessment.Status = tracked.Status;
            assessment.PracticeSessionId = tracked.PracticeSessionId;
            assessment.QuestionSetId = tracked.QuestionSetId;
            assessment.ScopeSkillsJson = tracked.ScopeSkillsJson;
            assessment.OverallReadiness = tracked.OverallReadiness;
            assessment.ReadinessStatus = tracked.ReadinessStatus;
            assessment.ExplanationJson = tracked.ExplanationJson;
            assessment.UpdatedAt = tracked.UpdatedAt;
        }
        else
        {
            // Cùng instance: SkillResults đã là bản mới sau Clear+Add.
            assessment.UpdatedAt = tracked.UpdatedAt;
        }
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

    private void EnsureSkillResultsNotFalseModified(CandidateAssessment assessment)
    {
        foreach (var sr in assessment.SkillResults)
        {
            var srEntry = _db.Entry(sr);
            if (srEntry.State is EntityState.Added or EntityState.Deleted or EntityState.Detached)
                continue;
            // Id mới (chưa từng persist) mà bị Modified → sửa thành Added.
            if (sr.CreatedAt == default && srEntry.State == EntityState.Modified)
                srEntry.State = EntityState.Added;
        }
    }

    private static void CopyAssessmentScalars(CandidateAssessment from, CandidateAssessment to)
    {
        to.OverallReadiness = from.OverallReadiness;
        to.ReadinessStatus = from.ReadinessStatus;
        to.ExplanationJson = from.ExplanationJson;
        to.Status = from.Status;
        to.PracticeSessionId = from.PracticeSessionId;
        to.QuestionSetId = from.QuestionSetId;
        to.ScopeSkillsJson = from.ScopeSkillsJson;
        to.UpdatedAt = DateTime.UtcNow;
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
        await _db.CandidateRoadmaps.AddRangeAsync(roadmaps);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(CandidateRoadmap roadmap)
    {
        roadmap.UpdatedAt = DateTime.UtcNow;
        var entry = _db.Entry(roadmap);
        if (entry.State == EntityState.Detached)
        {
            // Không Update(graph): Items/RoadmapNode có thể bị mark Modified nhầm.
            var tracked = await _db.CandidateRoadmaps.FirstOrDefaultAsync(r => r.Id == roadmap.Id);
            if (tracked is null)
                throw new InvalidOperationException($"Roadmap {roadmap.Id} không tồn tại để cập nhật.");

            tracked.IsActive = roadmap.IsActive;
            tracked.CurrentScore = roadmap.CurrentScore;
            tracked.TargetScore = roadmap.TargetScore;
            tracked.Gap = roadmap.Gap;
            tracked.PriorityScore = roadmap.PriorityScore;
            tracked.Kind = roadmap.Kind;
            tracked.Priority = roadmap.Priority;
            tracked.Status = roadmap.Status;
            tracked.SourceAssessmentId = roadmap.SourceAssessmentId;
            tracked.ExplanationJson = roadmap.ExplanationJson;
            tracked.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>Archive mọi roadmap active của user bằng ExecuteUpdate — tránh concurrency trên graph Items.</summary>
    public async Task ArchiveActiveByCandidateAsync(Guid candidateUserId)
    {
        await _db.CandidateRoadmaps
            .Where(r => r.CandidateUserId == candidateUserId && r.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.IsActive, false)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow));
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
