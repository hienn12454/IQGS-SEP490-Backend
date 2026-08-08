using DomainLayer.Studio;
using Microsoft.AspNetCore.Http;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>
/// SCRUM-368 / SCRUM-388 / SCRUM-389: Chat Studio tinh chỉnh interview plan + settings + tài liệu RAG.
/// Cho phép câu ngắn kiểu "Chỉ git syntax thôi" (exclusive topic).
/// </summary>
public static class PlanChatScopeGuard
{
    private static readonly string[] AllowHints =
    [
        "plan", "interview", "question", "questions", "section", "sections",
        "difficulty", "harder", "easier", "easy", "medium", "hard",
        "behavioral", "technical", "system design", "system_design", "coding",
        "focus", "skill", "skills", "coverage", "seniority", "junior", "senior", "mid",
        "duration", "minutes", "length", "tone", "language", "rubric", "sample",
        "add", "remove", "more", "less", "reduce", "increase", "decrease",
        "refine", "adjust", "change", "update", "outline", "structure",
        // tài liệu RAG / knowledge
        "tài liệu", "tai lieu", "document", "documents", "knowledge", "file", "files",
        "rag", "upload", "đã upload", "da upload", "selected", "nguồn", "nguon",
        "markdown", "english", "vietnamese", "tiếng anh", "tiếng việt",
        // tiếng Việt thường dùng
        "câu hỏi", "câu ", " cau ", "độ khó", "khó hơn", "dễ hơn", "thêm", "bớt", "giảm", "tăng",
        "phỏng vấn", "kỹ năng", "kỹ thuật", "kĩ thuật", "thời lượng", "phút", "chỉnh", "sửa plan", "tinh chỉnh",
        "apply_studio_settings", "apply studio", "áp dụng", "ap dung",
        "văn phong", "định dạng", "scoring", "đáp án mẫu",
        // exclusive / thu hẹp topic
        "chỉ ", "chi ", " only", "only ", "cụ thể", "cu the", "toàn về", "toan ve",
        "toàn hỏi", "toan hoi", "riêng ", "rieng ", "thôi", "thoi",
        "hỏi ", "hoi ", "syntax", "thao tác", "thao tac"
    ];

    /// <summary>Skill/topic phổ biến — đủ để coi là chỉnh focus plan (vd. "Chỉ git thôi").</summary>
    private static readonly string[] SkillHints =
    [
        "git", "docker", "kubernetes", "k8s", "redis", "kafka", "graphql", "grpc",
        "ef core", "entity framework", "asp.net", "dotnet", ".net", "csharp", "c#",
        "react", "next.js", "nextjs", "typescript", "javascript", "nodejs", "node.js",
        "postgresql", "postgres", "sql server", "mysql", "mongodb",
        "oauth", "jwt", "rest api", "microservices", "ci/cd", "devops",
        "linux", "nginx", "aws", "azure"
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

        // "Chỉ git syntax thôi" — skill token đủ coi là chỉnh focus plan
        if (HasSkillHint(lower))
            return;

        if (RegexHasQuestionIntent(lower))
            return;

        throw OffTopic();
    }

    private static bool HasSkillHint(string lower)
        => SkillHints.Any(s => lower.Contains(s, StringComparison.Ordinal));

    private static bool RegexHasQuestionIntent(string lower)
        => System.Text.RegularExpressions.Regex.IsMatch(lower, @"\d+\s*(câu|cau|question)")
           || lower.Contains("phỏng vấn", StringComparison.Ordinal)
           || lower.Contains("interview", StringComparison.Ordinal);

    private static StudioBusinessException OffTopic()
        => new(
            "PLAN_CHAT_OFF_TOPIC",
            StatusCodes.Status422UnprocessableEntity,
            "Chỉ hỗ trợ chỉnh sửa interview plan (số câu, độ khó, section, focus, thời lượng, tài liệu RAG…). Không trả lời câu hỏi ngoài plan.");
}
