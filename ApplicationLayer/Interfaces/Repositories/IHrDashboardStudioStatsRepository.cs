using ApplicationLayer.DTOs.Hr;
using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Interfaces.Repositories;

/// <summary>Dữ liệu generate Studio cho dashboard (thay V1 jobs).</summary>
public interface IHrDashboardStudioStatsRepository
{
    Task<IReadOnlyList<HrStudioProjectStatRow>> GetProjectStatsByOwnerAsync(Guid ownerId);
}

public sealed class HrStudioProjectStatRow
{
    public Guid ProjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public InterviewProjectStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int QuestionCount { get; init; }
    public string? RoleTitle { get; init; }
    public IReadOnlyList<HrStudioQuestionTypeCount> QuestionTypeCounts { get; init; }
        = Array.Empty<HrStudioQuestionTypeCount>();
}

public sealed class HrStudioQuestionTypeCount
{
    public string Type { get; init; } = string.Empty;
    public int Count { get; init; }
}
