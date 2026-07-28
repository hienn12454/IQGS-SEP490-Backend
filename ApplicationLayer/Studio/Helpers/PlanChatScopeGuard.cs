using DomainLayer.Studio;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// SCRUM-368: Chat Studio chỉ được dùng để tinh chỉnh interview plan.
/// Chặn câu hỏi ngoài phạm vi (thời tiết, code help, chat xã giao…).
/// </summary>
public static class PlanChatScopeGuard
{
    private static readonly string[] AllowHints =
    [
        "plan", "interview", "question", "questions", "section", "sections",
        "difficulty", "harder", "easier", "easy", "medium", "hard",
        "behavioral", "technical", "system design", "system_design", "coding",
        "focus", "skill", "skills", "coverage", "seniority", "junior", "senior", "mid",
        "duration", "minutes", "length", "tone", "language", "rubric",
        "add", "remove", "more", "less", "reduce", "increase", "decrease",
        "refine", "adjust", "change", "update", "outline", "structure",
        // tiếng Việt thường dùng
        "câu hỏi", "độ khó", "khó hơn", "dễ hơn", "thêm", "bớt", "giảm", "tăng",
        "phỏng vấn", "kỹ năng", "kỹ thuật", "kĩ thuật", "thời lượng", "phút", "chỉnh", "sửa plan", "tinh chỉnh",
        "apply_studio_settings", "apply studio", "áp dụng", "ap dung"
    ];

    private static readonly string[] DenyHints =
    [
        "weather", "thời tiết", "joke", "hài", "tell me a story",
        "write code", "debug", "stack overflow", "who are you",
        "capital of", "recipe", "nấu ăn", "bóng đá", "crypto",
        "love advice", "tình yêu", "homework unrelated"
    ];

    public static void EnsurePlanRelated(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
            throw new StudioBusinessException(
                "PLAN_REFINE_INSTRUCTION_REQUIRED",
                StatusCodes.Status400BadRequest,
                "Cần nội dung chỉnh sửa plan.");

        var text = instruction.Trim();
        if (text.Length < 3)
            throw new StudioBusinessException(
                "PLAN_REFINE_INSTRUCTION_TOO_SHORT",
                StatusCodes.Status400BadRequest,
                "Instruction quá ngắn.");

        var lower = text.ToLowerInvariant();

        if (DenyHints.Any(d => lower.Contains(d)))
            throw OffTopic();

        if (AllowHints.Any(a => lower.Contains(a)))
            return;

        // Không khớp allow → coi là ngoài phạm vi (an toàn hơn mở rộng)
        throw OffTopic();
    }

    private static StudioBusinessException OffTopic()
        => new(
            "PLAN_CHAT_OFF_TOPIC",
            StatusCodes.Status422UnprocessableEntity,
            "Chỉ hỗ trợ chỉnh sửa interview plan (số câu, độ khó, section, focus, thời lượng…). Không trả lời câu hỏi ngoài plan.");
}
