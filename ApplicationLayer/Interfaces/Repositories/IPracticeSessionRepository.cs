using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.QuestionSet;
using DomainLayer.Entities;

namespace ApplicationLayer.Interfaces.Repositories;

public interface IPracticeSessionRepository
{
    Task AddAsync(PracticeSession session);
    Task<PracticeSession?> GetByIdAsync(Guid id);
    Task<PracticeSession?> GetInProgressByQuestionSetAsync(Guid candidateUserId, Guid questionSetId);
    Task UpdateAsync(PracticeSession session);

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
}
