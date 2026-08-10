using ApplicationLayer.DTOs.Candidate;

namespace ApplicationLayer.Interfaces.Services;

public interface IQuestionSetFeedbackService
{
    /// <summary>Candidate gửi/sửa feedback cho question set — yêu cầu đã có phiên luyện tập COMPLETED trên bộ này. Upsert theo (CandidateUserId, QuestionSetId).</summary>
    Task<QuestionSetFeedbackDto> SubmitAsync(Guid candidateUserId, Guid questionSetId, SubmitQuestionSetFeedbackDto dto);

    /// <summary>Feedback candidate hiện tại đã gửi cho question set — null nếu chưa gửi. Dùng để prefill form sửa.</summary>
    Task<QuestionSetFeedbackDto?> GetMineAsync(Guid candidateUserId, Guid questionSetId);

    /// <summary>Danh sách feedback công khai + rating trung bình — public, dùng cho trang chi tiết marketplace.</summary>
    Task<QuestionSetFeedbackSummaryDto> GetPublicAsync(Guid questionSetId, QuestionSetFeedbackQueryDto query);

    /// <summary>Danh sách feedback cho HR chủ sở hữu xem — chỉ HR sở hữu question set mới xem được.</summary>
    Task<QuestionSetFeedbackSummaryDto> GetForOwnerAsync(Guid questionSetId, Guid hrOwnerId, QuestionSetFeedbackQueryDto query);
}
