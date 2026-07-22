using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.ResponseCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;

namespace WebAPI.Controllers.Candidate;

/// <summary>VÃ²ng Ä‘á»i phiÃªn luyá»‡n táº­p phá»ng váº¥n cá»§a Candidate: báº¯t Ä‘áº§u/tiáº¿p tá»¥c, tráº£ lá»i tá»«ng cÃ¢u, hoÃ n thÃ nh, xem lá»‹ch sá»­ + thá»‘ng kÃª + AI feedback.</summary>
[ApiController]
[Route("api/candidate/practice-sessions")]
[Authorize(Roles = "Candidate")]
public class CandidatePracticeSessionsController : ControllerBase
{
    private readonly ICandidatePracticeSessionService _service;

    public CandidatePracticeSessionsController(ICandidatePracticeSessionService service)
    {
        _service = service;
    }

    /// <summary>Báº¯t Ä‘áº§u phiÃªn luyá»‡n táº­p má»›i tá»« 1 bá»™ cÃ¢u há»i PUBLISHED. Náº¿u Ä‘ang cÃ³ phiÃªn IN_PROGRESS dá»Ÿ dang cho cÃ¹ng bá»™ cÃ¢u há»i nÃ y, tráº£ vá» phiÃªn Ä‘Ã³ thay vÃ¬ táº¡o má»›i (resume tá»± Ä‘á»™ng).</summary>
    /// <remarks>404 náº¿u questionSetId khÃ´ng tá»“n táº¡i hoáº·c chÆ°a publish.</remarks>
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartPracticeSessionDto dto)
    {
        var result = await _service.StartAsync(dto.QuestionSetId, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Danh sÃ¡ch phiÃªn luyá»‡n táº­p cá»§a Candidate hiá»‡n táº¡i â€” máº·c Ä‘á»‹nh chá»‰ láº¥y COMPLETED, truyá»n status=IN_PROGRESS Ä‘á»ƒ láº¥y phiÃªn Ä‘ang lÃ m dá»Ÿ. Há»— trá»£ tÃ¬m theo keyword (tÃªn bá»™/cÃ´ng ty) vÃ  lá»c theo ngÃ y.</summary>
    /// <param name="query">status (máº·c Ä‘á»‹nh COMPLETED), questionSetId, keyword, page, pageSize, vÃ  lá»c ngÃ y: fromDate/toDate (khoáº£ng cá»¥ thá»ƒ), hoáº·c year+month (1 thÃ¡ng), hoáº·c chá»‰ year (cáº£ nÄƒm).</param>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PracticeSessionListQueryDto query)
    {
        var result = await _service.ListAsync(User.GetUserId(), query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Thá»‘ng kÃª luyá»‡n táº­p cá»§a Candidate hiá»‡n táº¡i: tá»•ng sá»‘ phiÃªn COMPLETED, Ä‘iá»ƒm trung bÃ¬nh, Ä‘iá»ƒm cao nháº¥t, Ä‘iá»ƒm bÃ i gáº§n nháº¥t, tá»•ng thá»i gian luyá»‡n táº­p.</summary>
    /// <param name="query">Lá»c theo ngÃ y: fromDate/toDate (khoáº£ng cá»¥ thá»ƒ), hoáº·c year+month (1 thÃ¡ng), hoáº·c chá»‰ year (cáº£ nÄƒm) â€” bá» trá»‘ng Ä‘á»ƒ tÃ­nh toÃ n bá»™ thá»i gian.</param>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] PracticeSessionStatsQueryDto query)
    {
        var result = await _service.GetStatsAsync(User.GetUserId(), query);
        return SuccessResp.Ok(result);
    }

    /// <summary>Chi tiáº¿t 1 phiÃªn luyá»‡n táº­p (Ä‘ang cháº¡y hoáº·c Ä‘Ã£ xong) â€” danh sÃ¡ch cÃ¢u há»i kÃ¨m cÃ¢u tráº£ lá»i candidate Ä‘Ã£ submit cho tá»«ng cÃ¢u (null náº¿u chÆ°a tráº£ lá»i).</summary>
    /// <remarks>Chá»‰ chá»§ phiÃªn má»›i xem Ä‘Æ°á»£c â€” 403 náº¿u lÃ  candidate khÃ¡c, 404 náº¿u khÃ´ng tá»“n táº¡i.</remarks>
    /// <param name="id">Id phiÃªn luyá»‡n táº­p.</param>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Láº¥y toÃ n bá»™ AI feedback cá»§a phiÃªn (score tá»«ng cÃ¢u + overall + aiInsight) â€” SCRUM-282 / SCRUM-305.</summary>
    /// <remarks>Chá»‰ chá»§ phiÃªn xem Ä‘Æ°á»£c. FE dÃ¹ng cho mÃ n result. aiInsight gá»“m nháº­n xÃ©t tá»•ng quan + skillsToImprove song ngá»¯.</remarks>
    /// <param name="id">Id phiÃªn luyá»‡n táº­p.</param>
    [HttpGet("{id:guid}/feedback")]
    public async Task<IActionResult> GetFeedback(Guid id)
    {
        var result = await _service.GetFeedbackAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>LÆ°u cÃ¢u tráº£ lá»i cho 1 cÃ¢u há»i, sau Ä‘Ã³ gá»i RAG evaluate sync vÃ  lÆ°u ai_feedbacks (SCRUM-282).</summary>
    /// <remarks>
    /// Upsert answer. CÃ¢u tráº£ lá»i trá»‘ng / quÃ¡ ngáº¯n / "khÃ´ng biáº¿t" / spam â†’ score=0, evaluationStatus=Succeeded, khÃ´ng gá»i AI.
    /// Náº¿u RAG timeout/lá»—i â†’ answer váº«n Ä‘Æ°á»£c lÆ°u, evaluationStatus=Failed (HTTP 200).
    /// questionId pháº£i thuá»™c Ä‘Ãºng bá»™ cÃ¢u há»i cá»§a phiÃªn nÃ y, náº¿u khÃ´ng â†’ 400.
    /// </remarks>
    /// <param name="id">Id phiÃªn luyá»‡n táº­p.</param>
    /// <param name="dto">questionId vÃ  answerText.</param>
    [HttpPost("{id:guid}/answers")]
    public async Task<IActionResult> SubmitAnswer(Guid id, [FromBody] SubmitAnswerDto dto)
    {
        var result = await _service.SubmitAnswerAsync(id, User.GetUserId(), dto);
        return SuccessResp.Ok(result);
    }

    /// <summary>ÄÃ¡nh dáº¥u phiÃªn luyá»‡n táº­p Ä‘Ã£ hoÃ n thÃ nh (IN_PROGRESS â†’ COMPLETED), tÃ­nh overall_score = tá»•ng Ä‘iá»ƒm Succeeded / tá»•ng sá»‘ cÃ¢u trong bá»™ (cÃ¢u chÆ°a lÃ m = 0), sinh aiInsight song ngá»¯ + skillsToImprove.</summary>
    /// <remarks>Chá»‰ hoÃ n thÃ nh Ä‘Æ°á»£c phiÃªn Ä‘ang IN_PROGRESS â€” 400 náº¿u phiÃªn Ä‘Ã£ COMPLETED/ABANDONED. VÃ­ dá»¥ bá»™ 30 cÃ¢u, 1 cÃ¢u 95 Ä‘iá»ƒm â†’ overall â‰ˆ 3.17. RAG insight lá»—i â†’ complete váº«n 200, aiInsight=null.</remarks>
=======
    /// <summary>ÄÃ¡nh dáº¥u phiÃªn luyá»‡n táº­p Ä‘Ã£ hoÃ n thÃ nh (IN_PROGRESS â†’ COMPLETED), tÃ­nh overall_score = tá»•ng Ä‘iá»ƒm Succeeded / tá»•ng sá»‘ cÃ¢u trong bá»™ (cÃ¢u chÆ°a lÃ m = 0), lÃ m trÃ²n 2 chá»¯ sá»‘ tháº­p phÃ¢n.</summary>
    /// <remarks>Chá»‰ hoÃ n thÃ nh Ä‘Æ°á»£c phiÃªn Ä‘ang IN_PROGRESS â€” 400 náº¿u phiÃªn Ä‘Ã£ COMPLETED/ABANDONED. VÃ­ dá»¥ bá»™ 30 cÃ¢u, 1 cÃ¢u 95 Ä‘iá»ƒm â†’ overall â‰ˆ 3.17.</remarks>
>>>>>>> origin/main
    /// <param name="id">Id phiÃªn luyá»‡n táº­p.</param>
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var result = await _service.CompleteAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }

    /// <summary>Candidate chá»§ Ä‘á»™ng huá»· bá» phiÃªn Ä‘ang lÃ m dá»Ÿ (IN_PROGRESS â†’ ABANDONED) â€” dÃ¹ng khi muá»‘n thoÃ¡t mÃ  khÃ´ng tÃ­nh lÃ  Ä‘Ã£ hoÃ n thÃ nh.</summary>
    /// <param name="id">Id phiÃªn luyá»‡n táº­p.</param>
    [HttpPost("{id:guid}/abandon")]
    public async Task<IActionResult> Abandon(Guid id)
    {
        var result = await _service.AbandonAsync(id, User.GetUserId());
        return SuccessResp.Ok(result);
    }
}

