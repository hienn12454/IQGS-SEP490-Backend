using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.Hr;
using ApplicationLayer.DTOs.QuestionSet;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IPracticeSessionRepository
{
    Task AddAsync(PracticeSession session);
    Task<PracticeSession?> GetByIdAsync(Guid id);
    Task<PracticeSession?> GetInProgressByQuestionSetAsync(Guid candidateUserId, Guid questionSetId);
    Task UpdateAsync(PracticeSession session);

    /// <summary>Candidate đã có ít nhất 1 phiên COMPLETED trên question set này — điều kiện để được gửi feedback bộ câu hỏi.</summary>
    Task<bool> HasCompletedSessionAsync(Guid candidateUserId, Guid questionSetId);

    /// <summary>SCRUM-408: unpublish — bulk IN_PROGRESS của bộ → ABANDONED. Trả về số phiên đã hủy.</summary>
    Task<int> AbandonInProgressByQuestionSetAsync(Guid questionSetId);

    /// <summary>Giới hạn thời gian làm bài (phút) HR đặt cho bộ câu hỏi — null nếu không giới hạn hoặc bộ không tồn tại.</summary>
    Task<int?> GetTimeLimitMinutesAsync(Guid questionSetId);

    /// <summary>Các phiên IN_PROGRESS thuộc bộ có giới hạn thời gian (kèm QuestionSet) — cho watchdog tự nộp bài khi hết giờ.</summary>
    Task<IReadOnlyList<PracticeSession>> GetInProgressWithTimeLimitAsync();

    Task<(IReadOnlyList<PracticeSessionRow> Items, int TotalCount)> ListAsync(
        Guid candidateUserId, string? status, Guid? questionSetId, string? keyword,
        DateTime? fromDate, DateTime? toDate, int page, int pageSize);

    Task<PracticeSessionStatsDto> GetStatsAsync(Guid candidateUserId, DateTime? fromDate, DateTime? toDate);

    /// <summary>SCRUM-326: candidate đã practice 1 bộ câu hỏi (mọi status), sắp giảm dần theo StartedAt — dùng cho HR xem engagement.</summary>
    Task<IReadOnlyList<QuestionSetPractitionerRow>> ListPractitionersByQuestionSetAsync(Guid questionSetId);

    /// <summary>SCRUM-398: candidate đã từng có session trên bất kỳ set nào của HR (mọi status, IsActive).</summary>
    Task<bool> HasAnySessionOnHrOwnedSetsAsync(Guid candidateUserId, Guid hrOwnerId);

    /// <summary>SCRUM-398: danh sách session của candidate trên set thuộc HR — order StartedAt desc.</summary>
    Task<IReadOnlyList<HrCandidatePracticeOnMySetRow>> ListSessionsOnHrOwnedSetsAsync(Guid candidateUserId, Guid hrOwnerId);

    /// <summary>
    /// SCRUM-401: lấy CompletedAt + StartedAt của session COMPLETED từ fromUtc trở đi
    /// (aggregate heatmap ở service — tránh phụ thuộc pageSize ListAsync).
    /// </summary>
    Task<IReadOnlyList<PracticeSessionHeatmapRawRow>> ListCompletedTimestampsForHeatmapAsync(
        Guid candidateUserId, DateTime fromUtc);
}
