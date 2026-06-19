using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers.Internal;

/// <summary>
/// <para><b>⚠️ NỘI BỘ — KHÔNG DÙNG / KHÔNG TEST TRÊN SWAGGER.</b></para>
/// <para>Endpoint callback cho RAG service sau khi ingest tài liệu knowledge base.</para>
/// <para>HR, Admin và Frontend <b>không</b> gọi API này — dùng <c>api/admin/knowledge-documents</c> hoặc <c>api/hr/knowledge-documents</c> để upload/reingest.</para>
/// <para>Auth: header <c>X-Internal-Api-Key</c> (không phải JWT Bearer trên Swagger).</para>
/// </summary>
[ApiController]
[Route("internal/knowledge-documents")]
[Tags("Internal — RAG callback (không dùng)")]
public class KnowledgeDocumentsInternalController : ControllerBase
{
    private readonly IKnowledgeDocumentInternalService _service;

    public KnowledgeDocumentsInternalController(IKnowledgeDocumentInternalService service)
    {
        _service = service;
    }

    /// <summary>
    /// RAG gọi ngược Backend để cập nhật <c>knowledge_documents.status</c> sau ingest.
    /// <para>Luồng: <c>PROCESSING</c> → <c>COMPLETED</c> (kèm chunkCount) hoặc <c>FAILED</c> (kèm errorMessage).</para>
    /// <para><b>Không gọi thủ công.</b> Muốn xem trạng thái document → GET qua API Admin/HR.</para>
    /// </summary>
    /// <param name="id">Document ID do Backend tạo khi upload.</param>
    /// <param name="dto">Status do RAG gửi — chỉ PROCESSING, COMPLETED hoặc FAILED.</param>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateKnowledgeDocumentStatusDto dto)
    {
        var result = await _service.UpdateStatusAsync(id, dto);
        return SuccessResp.Ok(result);
    }
}
