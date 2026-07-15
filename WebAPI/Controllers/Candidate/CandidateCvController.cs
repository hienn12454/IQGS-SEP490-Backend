using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

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

    /// <summary>Tải lên CV — mỗi candidate chỉ giữ 1 CV, tải lên mới sẽ thay thế CV cũ.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] CandidateCvUploadForm form)
    {
        if (form.File is null || form.File.Length == 0)
            return BadRequest(new { Code = 400, Error = "File là bắt buộc." });

        await using var stream = form.File.OpenReadStream();
        var result = await _service.UploadAsync(
            stream, form.File.FileName, form.File.ContentType, form.File.Length, User.GetUserId());

        return SuccessResp.Ok(result);
    }

    /// <summary>Lấy thông tin CV hiện tại kèm link tải xuống (SAS URL có thời hạn).</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _service.GetAsync(User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Xóa CV hiện tại.</summary>
    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        await _service.DeleteAsync(User.GetUserId());
        return SuccessResp.NoContent();
    }
}

public class CandidateCvUploadForm
{
    public IFormFile File { get; set; } = null!;
}
