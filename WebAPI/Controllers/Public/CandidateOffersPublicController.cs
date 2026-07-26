using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using DomainLayer.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Public;

/// <summary>Luồng công khai (không auth) để candidate xác nhận lời đề nghị phỏng vấn nhận qua email.
/// GET chỉ đọc (mở link trong email an toàn dù mail client tự động quét/prefetch link), POST mới thực sự
/// mutate + gửi email báo HR — tránh trường hợp bot prescan link vô tình trigger accept thay candidate.</summary>
[ApiController]
[Route("api/public/candidate-offers")]
public class CandidateOffersPublicController : ControllerBase
{
    private readonly ICandidateOfferService _service;

    public CandidateOffersPublicController(ICandidateOfferService service)
    {
        _service = service;
    }

    /// <summary>Mở link "Xem &amp; Xác nhận" từ email — chỉ đọc, hiển thị lại nội dung lời đề nghị kèm nút xác nhận.
    /// Trả HTML "không hợp lệ/hết hạn" (200, không phải lỗi JSON) nếu token sai hoặc quá hạn — vì đây là trang
    /// candidate mở trực tiếp từ email, không phải API call của FE.</summary>
    [AllowAnonymous]
    [HttpGet("{token}")]
    public async Task<IActionResult> Confirm(string token)
    {
        try
        {
            return SuccessResp.Content(await _service.RenderConfirmPageAsync(token));
        }
        catch (Exception ex) when (ex is NotFoundException or BadRequestException)
        {
            return SuccessResp.Content(CandidateOfferPageHtml.BuildInvalidOrExpiredPage());
        }
    }

    /// <summary>Candidate bấm nút xác nhận trên trang xem lại — mutate Status→ACCEPTED + gửi email báo HR
    /// (kèm tên/email/tech stack candidate). Idempotent nếu bấm lại — không gửi lại email báo lần 2.</summary>
    [AllowAnonymous]
    [HttpPost("{token}/accept")]
    public async Task<IActionResult> Accept(string token)
    {
        try
        {
            return SuccessResp.Content(await _service.CommitAcceptAsync(token));
        }
        catch (Exception ex) when (ex is NotFoundException or BadRequestException)
        {
            return SuccessResp.Content(CandidateOfferPageHtml.BuildInvalidOrExpiredPage());
        }
    }
}
