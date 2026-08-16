using ApplicationLayer.DTOs.Candidate;

namespace ApplicationLayer.Interfaces.Services;

public interface ICandidatePersonalSetService
{
    Task<CandidatePersonalSetJobDto> CreateFromTextAsync(Guid candidateUserId, CreatePersonalSetFromTextDto dto, CancellationToken ct = default);
    Task<CandidatePersonalSetJobDto> CreateFromFileAsync(Guid candidateUserId, Stream file, string fileName, int numberOfQuestions, CancellationToken ct = default);
    Task<CandidatePersonalSetJobDto> StartCvDiagnosticAsync(Guid candidateUserId, CancellationToken ct = default);
    Task<CandidatePersonalSetJobDto> StartCvDrillAsync(Guid candidateUserId, string skill, CancellationToken ct = default);
    Task<CandidatePersonalSetJobDto> GetJobAsync(Guid jobId, Guid candidateUserId);
    Task<CandidatePersonalSetJobDto?> GetLatestPendingCoachJobAsync(Guid candidateUserId);
    Task<IReadOnlyList<CandidatePersonalSetListItemDto>> ListMineAsync(Guid candidateUserId);
    Task ExecuteGenerationAsync(Guid jobId, CancellationToken ct = default);
}
