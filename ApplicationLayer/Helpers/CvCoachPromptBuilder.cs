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

    public static string DiagnosticHrNote(IReadOnlyList<string> skills)
        => "Bộ kiểm tra kiến thức CV: tối thiểu 10 câu, độ khó tăng dần (easy → medium → hard). "
           + "Chỉ hỏi các skill sau, không bịa JD hay yêu cầu ngoài list: "
           + string.Join(", ", skills) + ".";

    public static string DrillHrNote(string skill)
        => $"Chỉ hỏi skill \"{skill}\". Sinh câu hỏi mới để kiểm tra kiến thức, không lặp đề cũ, không bịa JD.";

    public static string BuildCvContext(string? cvSummary, IReadOnlyList<string> skills)
    {
        var summary = string.IsNullOrWhiteSpace(cvSummary) ? "" : cvSummary.Trim();
        var skillLine = string.Join(", ", skills);
        return string.IsNullOrEmpty(summary)
            ? $"Skills to assess: {skillLine}"
            : $"CV summary: {summary}\nSkills to assess: {skillLine}";
    }
}
