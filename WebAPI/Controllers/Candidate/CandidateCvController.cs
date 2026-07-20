using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

/// <summary>CV của Candidate — upload (PDF/DOCX/JPG/JPEG/PNG) để AI trích kỹ năng (skills) và tự động cập nhật TechStack trên hồ sơ. Mỗi Candidate chỉ giữ đúng 1 CV.</summary>
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
}

public class CandidateCvUploadForm
{
    /// <summary>File CV, định dạng PDF, DOCX, JPG, JPEG hoặc PNG.</summary>
    public IFormFile File { get; set; } = null!;
}
