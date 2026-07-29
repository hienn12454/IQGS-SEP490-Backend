using ApplicationLayer.DTOs.Recommendation;

namespace ApplicationLayer.Interfaces.Services;

public interface ICandidateOfferService
{
    /// <summary>[HR] Gửi email đề nghị phỏng vấn cho candidate từ 1 recommendation HR sở hữu. Không status-gate.
    /// 409 nếu lần gửi gần nhất cho recommendation này đã ACCEPTED.</summary>
    Task<SendCandidateOfferResponseDto> SendOfferAsync(Guid recommendationId, Guid hrUserId, SendCandidateOfferRequestDto dto);

    /// <summary>[Public][GET] Chỉ đọc — trả HTML trang xác nhận (reuse nội dung offer đã lưu).
    /// Throws NotFoundException/BadRequestException cho token sai/hết hạn.</summary>
    Task<string> RenderConfirmPageAsync(string rawToken);

    /// <summary>[Public][POST] Mutate: Status→ACCEPTED + gửi email báo HR. Idempotent nếu đã ACCEPTED.</summary>
    Task<string> CommitAcceptAsync(string rawToken);
}
