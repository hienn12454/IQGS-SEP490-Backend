using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;

namespace ApplicationLayer.Services.Coach;

/// <summary>
/// SCRUM-503: hậu kiểm NỘI DUNG câu hỏi sau khi đã khớp blueprint.
/// Lý do: compliance chỉ đọc nhãn skill do LLM tự khai, nên một câu JavaScript vẫn có thể
/// được gán nhãn "Java". Ở đây soi text câu hỏi: nếu không nhắc skill của slot mà lại nhắc
/// một công nghệ dễ nhầm / ngôn ngữ khác thì fail job, không giao đề lạc đề cho ứng viên.
///
/// Luật cố ý hẹp để tránh false positive: câu hỏi Java chỉ nói "Spring Boot" vẫn được chấp nhận,
/// chỉ chặn khi có tín hiệu lệch rõ ràng (vd. nói JavaScript trong đề Java).
/// </summary>
public static class QuestionRelevanceValidator
{
    public sealed record Result(bool Ok, string? Error);

    public static Result Check(IReadOnlyList<RagGeneratedQuestionDto> questions)
    {
        var offTopic = new List<string>();

        foreach (var q in questions)
        {
            if (string.IsNullOrWhiteSpace(q.Skill)) continue;

            var text = $"{q.Question} {q.FocusArea}";
            if (TechSkillMatcher.MentionsSkill(text, q.Skill)) continue;

            var conflicting = TechSkillMatcher.FindConflictingSkill(text, q.Skill);
            if (conflicting is null) continue;

            offTopic.Add($"câu {q.Order ?? 0} (skill \"{q.Skill}\" nhưng nội dung nói về \"{conflicting}\")");
        }

        if (offTopic.Count == 0) return new Result(true, null);

        return new Result(false,
            "Bộ câu hỏi lệch khỏi kỹ năng cần đo: "
            + string.Join("; ", offTopic)
            + ". Hãy thử tạo lại đề.");
    }
}
