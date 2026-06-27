using ApplicationLayer.DTOs.QuestionSet;

namespace ApplicationLayer.Interfaces.Repositories;

/// <summary>Đọc dữ liệu question_sets phía Candidate (marketplace) — tách khỏi IQuestionSetRepository (phía HR quản lý/ghi).</summary>
public interface ICandidateMarketplaceRepository
{
    Task<(IReadOnlyList<PublishedQuestionSetRow> Items, int TotalCount)> ListPublishedAsync(
        int page, int pageSize, string? keyword, Guid? companyId, string? difficulty, IReadOnlyList<string>? skills);

    Task<PublishedQuestionSetDetail?> GetPublishedByIdAsync(Guid id);

    Task<bool> IsPublishedAsync(Guid id);

    /// <summary>Snapshot câu hỏi của 1 bộ — KHÔNG lọc theo Status (dùng khi session đã tạo, set có thể đã bị unpublish sau đó). Cố tình không có SampleAnswer/EvaluationCriteria.</summary>
    Task<IReadOnlyList<PublishedQuestionRow>> GetQuestionsSnapshotAsync(Guid questionSetId);

    Task<bool> QuestionBelongsToSetAsync(Guid questionId, Guid questionSetId);

    Task<IReadOnlyList<PublishedQuestionSetRow>> ListBookmarkedAsync(Guid candidateUserId);
}
