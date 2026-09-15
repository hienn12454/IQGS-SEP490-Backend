using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface ICompetencyFrameworkRepository
{
    Task<CompetencyFramework?> GetByRoleAndLevelAsync(string roleKey, string targetLevel);
    Task<CompetencyFramework?> GetByIdWithSkillsAsync(Guid id);
    Task<CompetencyScoringPolicy> GetPolicyAsync();
    /// <summary>SCRUM-453: admin tinh chỉnh scoring policy toàn cục (không gắn role).</summary>
    Task SavePolicyAsync(CompetencyScoringPolicy policy);

    /// <summary>SCRUM-453: catalog framework Active — resolver/catalog API dùng, không lọc theo stack.</summary>
    Task<List<CompetencyFramework>> ListActiveWithSkillsAsync();
    /// <summary>Gồm cả Draft — dành cho admin quản lý import.</summary>
    Task<List<CompetencyFramework>> ListAllWithSkillsAsync();
    /// <summary>Alias vai trò (data) để resolve target role tự do của ứng viên.</summary>
    Task<List<CompetencyRoleAlias>> ListAliasesAsync();
    /// <summary>Upsert idempotent theo (RoleKey, TargetLevel) khi import curated data.</summary>
    Task<Guid> UpsertFrameworkAsync(
        CompetencyFramework framework,
        IReadOnlyList<CompetencyFrameworkSkill> skills);
    /// <summary>Thay toàn bộ alias của một RoleKey (import là nguồn sự thật).</summary>
    Task ReplaceAliasesAsync(string roleKey, IReadOnlyList<CompetencyRoleAlias> aliases);
    Task UpdateStatusAsync(Guid frameworkId, string status);
}

/// <summary>SCRUM-457: Role Family + alias — domain SE configurable, không hardcode.</summary>
public interface ICompetencyRoleFamilyRepository
{
    Task<List<CompetencyRoleFamily>> ListActiveAsync();
    Task<List<CompetencyRoleFamilyAlias>> ListAliasesAsync();
    Task<List<CompetencyRoleFamily>> ListAllAsync();
    Task UpsertFamilyAsync(CompetencyRoleFamily family, IReadOnlyList<CompetencyRoleFamilyAlias> aliases);
}

/// <summary>SCRUM-454: luật level là config toàn cục — không lọc theo role/framework.</summary>
public interface ICompetencyLevelRuleRepository
{
    Task<List<CompetencyLevelRule>> ListAsync();
    /// <summary>Upsert theo Level — luật vẫn global, không có RoleKey/FrameworkId.</summary>
    Task UpsertRangeAsync(IReadOnlyList<CompetencyLevelRule> rules);
}

public interface ICandidateAssessmentRepository
{
    Task AddAsync(CandidateAssessment assessment);
    Task UpdateAsync(CandidateAssessment assessment);
    /// <summary>
    /// Ghi trạng thái Scored + thay thế SkillResults an toàn (tránh DbUpdateConcurrencyException
    /// khi DbSet.Update mark child mới thành Modified).
    /// </summary>
    Task SaveScoredAssessmentAsync(
        CandidateAssessment assessment,
        IReadOnlyList<CandidateAssessmentSkillResult> newSkillResults);
    Task UpdateReadinessAsync(
        Guid assessmentId,
        double? overallReadiness,
        string? readinessStatus,
        string? explanationJson);
    Task<CandidateAssessment?> GetByIdAsync(Guid id);
    Task<CandidateAssessment?> GetLatestScoredAsync(Guid candidateUserId);
    Task<CandidateAssessment?> GetByQuestionSetIdAsync(Guid questionSetId);
    Task<CandidateAssessment?> GetByJobIdAsync(Guid jobId);
    Task<List<CandidateAssessment>> ListByCandidateAsync(Guid candidateUserId);
}

public interface ICandidateRoadmapRepository
{
    Task AddRangeAsync(IEnumerable<CandidateRoadmap> roadmaps);
    Task UpdateAsync(CandidateRoadmap roadmap);
    /// <summary>Archive IsActive=true bằng ExecuteUpdate — tránh concurrency trên graph Items.</summary>
    Task ArchiveActiveByCandidateAsync(Guid candidateUserId);
    Task RestoreActiveAsync(IEnumerable<Guid> roadmapIds);
    Task<List<CandidateRoadmap>> ListByCandidateAsync(Guid candidateUserId);
    /// <summary>Mọi roadmap của user kể cả IsActive=false — dùng khi archive thế hệ cũ.</summary>
    Task<List<CandidateRoadmap>> ListAllByCandidateAsync(Guid candidateUserId);
    Task<CandidateRoadmap?> GetByIdAsync(Guid id);
}

public interface IRoadmapNodeRepository
{
    Task<List<RoadmapNode>> ListBySkillAsync(string roleKey, string level, string skill);
    Task<List<RoadmapNode>> ListAllAsync();
    Task UpsertRangeAsync(IReadOnlyList<RoadmapNode> nodes);
    Task DeleteAsync(Guid id);
    Task<HashSet<string>> ListKnownRoleKeysAsync();
}
