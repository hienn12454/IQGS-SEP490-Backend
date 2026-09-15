using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.Coach;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Services;

public interface ICoachCompetencyService
{
    Task<CoachContextDto> GetContextAsync(Guid candidateUserId);
    /// <summary>SCRUM-453: catalog Target Role đang có framework — FE dùng cho combobox Target Role.</summary>
    Task<List<CoachFrameworkOptionDto>> ListFrameworkCatalogAsync();
    Task<CoachContextDto> UpdateContextAsync(Guid candidateUserId, UpdateCoachContextDto dto);
    Task<CandidatePersonalSetJobDto> StartDiagnosticAsync(Guid candidateUserId, CancellationToken ct = default);
    Task<CandidatePersonalSetJobDto> StartDrillForRoadmapItemAsync(Guid candidateUserId, Guid roadmapId, Guid itemId, CancellationToken ct = default);
    Task<CandidatePersonalSetJobDto> StartReassessmentAsync(Guid candidateUserId, Guid roadmapId, CancellationToken ct = default);
    Task<CoachAssessmentDto?> GetLatestReportAsync(Guid candidateUserId);
    Task<IReadOnlyList<CoachAssessmentDto>> GetHistoryAsync(Guid candidateUserId);
    Task<CoachAssessmentDto?> GetAssessmentAsync(Guid candidateUserId, Guid assessmentId);
    Task<IReadOnlyList<CoachRoadmapDto>> ListRoadmapsAsync(Guid candidateUserId);
    Task<CoachRoadmapDto> StartRoadmapAsync(Guid candidateUserId, Guid roadmapId);
    Task<CoachRoadmapDto> GetRoadmapAsync(Guid candidateUserId, Guid roadmapId);
    /// <summary>Chấm diagnostic/reassessment từ session. Trả SkipReason thay vì return im lặng.</summary>
    Task<CoachScoreResult> ScoreAssessmentFromSessionAsync(PracticeSession session);
    Task HandleDrillSessionCompletedAsync(PracticeSession session);

    /// <summary>Huỷ job Coach đang Queued/Generating — reset assessment/roadmap item kẹt.</summary>
    Task<CandidatePersonalSetJobDto> CancelActiveJobAsync(Guid candidateUserId, Guid jobId);

    /// <summary>Khi RAG/Hangfire fail: đánh dấu assessment Failed + trả roadmap item về Pending.</summary>
    Task MarkGenerationFailedAsync(Guid jobId);
}
