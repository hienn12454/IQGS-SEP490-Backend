using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Admin;

[ApiController]
[Route("api/admin/rag")]
[Authorize(Roles = "Admin")]
public class AdminRagStatusController : ControllerBase
{
    private readonly IRagService _ragService;

    public AdminRagStatusController(IRagService ragService)
    {
        _ragService = ragService;
    }

    /// <summary>Kiểm tra tình trạng dịch vụ RAG (kết nối, cấu hình, database vector).</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var status = await _ragService.GetHealthStatusAsync(ct);
        return new JsonResult(new GenericResp<object>
        {
            Code = RespCode.OK,
            Message = status.WrapperMessage,
            Data = status
        })
        { StatusCode = RespCode.OK };
    }
}
