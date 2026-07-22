using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

/// <summary>
/// CV của Candidate — upload (PDF/DOCX/JPG/JPEG/PNG) để AI trích kỹ năng (skills) và tự động cập nhật TechStack trên hồ sơ.
/// Mỗi Candidate chỉ giữ đúng 1 CV. Khi bật AutoSyncProfileFromCv (mặc định bật), CV được ưu tiên cao nhất: mỗi lần
/// upload sẽ tự ghi đè họ tên/SĐT/địa chỉ/GitHub/LinkedIn trên profile bằng dữ liệu CV trích xuất được — TRỪ field nào
/// candidate đã từng tự tay chỉnh qua PUT profile (field đó bị khóa vĩnh viễn khỏi CV sync, không bao giờ bị ghi đè lại).
/// </summary>
[ApiController]
[Route("api/candidate/cv")]
[Authorize(Roles = "Candidate")]
public class CandidateCvController : ControllerBase
{
    private readonly ICandidateCvService _service;

    public CandidateCvController(ICandidateCvService service)
    {
        _service = service;
    }

    /// <summary>Tải lên CV (PDF/DOCX/JPG/JPEG/PNG, tối đa theo cấu hình Cv:MaxFileSizeMb) — lưu file, gọi AI phân tích kỹ năng, tự động ghi đè TechStack trên hồ sơ. Upload mới sẽ thay thế CV cũ.</summary>
    /// <remarks>
    /// 400 nếu sai định dạng/quá dung lượng. Nếu bước phân tích AI lỗi/timeout, request trả lỗi rõ ràng nhưng file CV đã lưu vẫn được giữ nguyên, TechStack cũ không bị mất.
    /// Nếu đang bật AutoSyncProfileFromCv (xem GET/PUT sync-settings), response còn trả profileFieldsSynced — tên các field
    /// (họ tên/SĐT/địa chỉ/GitHub/LinkedIn) vừa được ghi đè trên profile từ CV này; field CV không trích được thì giữ nguyên giá trị cũ;
    /// field nào candidate đã từng tự sửa tay trong profile thì nằm trong lockedFromCvSync và sẽ không bao giờ bị CV ghi đè nữa.
    /// </remarks>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] CandidateCvUploadForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest(new { Code = 400, Error = "File là bắt buộc." });

        await using var stream = form.File.OpenReadStream();
        var result = await _service.UploadAsync(
            stream, form.File.FileName, form.File.ContentType, form.File.Length, User.GetUserId(), ct);

        return SuccessResp.Ok(result);
    }

    /// <summary>Trạng thái + kết quả phân tích CV gần nhất: tên file, skills[], summary, techStack hiện tại, thời điểm phân tích, link tải file.</summary>
    /// <remarks>404 nếu Candidate chưa từng upload CV nào.</remarks>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _service.GetAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Xóa CV hiện tại (file trên Blob + toàn bộ thông tin đánh giá) — không tự động xóa TechStack đã cập nhật trước đó trên hồ sơ.</summary>
    /// <remarks>404 nếu chưa có CV nào để xóa.</remarks>
    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        await _service.DeleteAsync(User.GetUserId());
        return SuccessResp.NoContent();
    }

    /// <summary>Xem cài đặt tự đồng bộ thông tin cá nhân (họ tên, SĐT, địa chỉ, GitHub, LinkedIn) từ CV vào profile — mặc định bật (true) nếu chưa từng đổi.</summary>
    [HttpGet("sync-settings")]
    public async Task<IActionResult> GetSyncSettings()
    {
        var result = await _service.GetSyncSettingsAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Bật/tắt tự đồng bộ thông tin cá nhân từ CV vào profile cho các lần upload sau. Tắt: upload CV chỉ cập nhật TechStack như cũ — thông tin candidate đã tự chỉnh trong profile được giữ nguyên qua các lần upload sau.</summary>
    /// <param name="dto">autoSyncProfileFromCv: true/false.</param>
    [HttpPut("sync-settings")]
    public async Task<IActionResult> UpdateSyncSettings([FromBody] CvSyncSettingsDto dto)
    {
        var result = await _service.UpdateSyncSettingsAsync(User.GetUserId(), dto);
        return SuccessResp.Ok(result);
    }
}

public class CandidateCvUploadForm
{
    /// <summary>File CV, định dạng PDF, DOCX, JPG, JPEG hoặc PNG.</summary>
    public IFormFile File { get; set; } = null!;
}
