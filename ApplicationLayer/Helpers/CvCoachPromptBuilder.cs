using ApplicationLayer.Studio.Helpers;

namespace ApplicationLayer.Helpers;

public static class CvCoachPromptBuilder
{
    public static string BuildSyntheticJd(
        string? targetRole,
        string? seniority,
        string? cvSummary,
        IReadOnlyList<string> skills)
    {
        var role = string.IsNullOrWhiteSpace(targetRole) ? "Software Engineer" : targetRole.Trim();
        var level = string.IsNullOrWhiteSpace(seniority) ? "unspecified" : seniority.Trim();
        var summary = string.IsNullOrWhiteSpace(cvSummary) ? "(no summary)" : cvSummary.Trim();
        var skillLine = string.Join(", ", skills);
        return
            $"Candidate applying for {role} ({level}).\n" +
            $"CV summary: {summary}\n" +
            $"Skills to assess (only these): {skillLine}\n" +
            "Generate interview questions that verify the candidate actually knows these skills from their CV. " +
            "Do not invent job requirements outside this skill list.";
    }

    /// <summary>
    /// Note gửi RAG cho đề coach. Số câu phải bám đúng blueprint của Backend
    /// (trước đây ghi cứng "tối thiểu 10 câu" nên xung đột với blueprint framework).
    /// </summary>
    public static string BlueprintNote(IReadOnlyList<string> skills, int totalQuestions, string? outputLanguage = null)
    {
        var skillLine = string.Join(", ", skills);
        if (IsEnglish(outputLanguage))
            return $"Generate EXACTLY {totalQuestions} questions. Each recommendedQuestionOutline row is one question "
                   + "and must keep that row's skill, topic, and difficulty. Do not merge, add, or drop questions. "
                   + "Only ask about these skills. Do not invent a job description or requirements outside the list: "
                   + skillLine + ".";
        return $"Sinh ĐÚNG {totalQuestions} câu, mỗi dòng recommendedQuestionOutline tương ứng 1 câu và phải giữ nguyên "
               + "skill, topic, difficulty của dòng đó. Không gộp, không thêm, không bớt câu. "
               + "Chỉ hỏi các skill sau, không bịa JD hay yêu cầu ngoài list: "
               + skillLine + ".";
    }

    /// <summary>
    /// SCRUM-503: phạm vi Re-assessment. Nêu rõ topic đã luyện và cấm công nghệ gần tên
    /// (Java vs JavaScript, Go vs Django…) vì đây là nguồn gốc đề lạc chủ đề.
    /// </summary>
    public static string ReassessmentScopeNote(string skill, IReadOnlyList<string> practicedTopics)
    {
        var note = $"\nScope: every question MUST be about \"{skill}\" only.";
        if (practicedTopics.Count > 0)
            note += $"\nTopics the candidate just practised (stay inside them): {string.Join("; ", practicedTopics)}.";
        note += "\nDo NOT ask about any other technology, and do NOT substitute a similarly named one "
                + $"(e.g. JavaScript/TypeScript when the skill is Java). If a question does not test \"{skill}\", drop it.";
        return note;
    }

    public static string DrillHrNote(string skill, string? outputLanguage = null)
        => IsEnglish(outputLanguage)
            ? $"Ask only about the skill \"{skill}\". Generate new questions that test knowledge. Do not repeat old questions or invent a job description."
            : $"Chỉ hỏi skill \"{skill}\". Sinh câu hỏi mới để kiểm tra kiến thức, không lặp đề cũ, không bịa JD.";

    public static string ReassessmentHrNote(string? outputLanguage = null)
        => IsEnglish(outputLanguage)
            ? "Re-assessment: new questions, same skill, do not repeat previous questions."
            : "Re-assessment: câu hỏi mới, cùng skill, không lặp đề cũ.";

    private static bool IsEnglish(string? outputLanguage)
        => StudioOutputLanguage.Normalize(outputLanguage) == StudioOutputLanguage.English;

    public static string BuildCvContext(string? cvSummary, IReadOnlyList<string> skills)
    {
        var summary = string.IsNullOrWhiteSpace(cvSummary) ? "" : cvSummary.Trim();
        var skillLine = string.Join(", ", skills);
        return string.IsNullOrEmpty(summary)
            ? $"Skills to assess: {skillLine}"
            : $"CV summary: {summary}\nSkills to assess: {skillLine}";
    }
}
