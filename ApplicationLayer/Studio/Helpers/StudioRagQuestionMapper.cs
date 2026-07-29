using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Studio.Helpers;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;

namespace ApplicationLayer.Studio.Helpers;

/// <summary>SCRUM-367: Map câu hỏi RAG → InterviewQuestion Studio.</summary>
public static class StudioRagQuestionMapper
{
    public static List<InterviewQuestion> Map(
        IReadOnlyList<RagGeneratedQuestionDto> ragQuestions,
        Guid projectId,
        Guid planId,
        Guid runId,
        IReadOnlyList<PlanSection> sections,
        bool includeSampleAnswers,
        bool includeScoringRubric)
    {
        var list = new List<InterviewQuestion>();
        var order = 1;
        foreach (var q in ragQuestions.OrderBy(x => x.Order ?? int.MaxValue))
        {
            if (string.IsNullOrWhiteSpace(q.Question)) continue;

            var type = StudioRagPlanMapper.MapQuestionType(q.QuestionType);
            var difficulty = StudioRagPlanMapper.MapDifficulty(q.Difficulty);
            var sectionId = ResolveSectionId(sections, type, q.FocusArea);

            var rubric = includeScoringRubric
                ? (q.EvaluationCriteria is { Count: > 0 }
                    ? string.Join("; ", q.EvaluationCriteria)
                    : q.Rationale)
                : null;

            list.Add(new InterviewQuestion
            {
                ProjectId = projectId,
                InterviewPlanId = planId,
                PlanSectionId = sectionId,
                GenerationRunId = runId,
                Content = q.Question.Trim(),
                Type = type,
                Difficulty = difficulty,
                OrderIndex = q.Order is > 0 ? q.Order.Value : order,
                EstimatedMinutes = 5,
                ExpectedAnswer = includeSampleAnswers ? q.SampleAnswer : null,
                ScoringRubric = rubric,
                GeneratedByModelName = "RAG",
                TagsJson = q.Skill is null ? null : $"[\"{Escape(q.Skill)}\"]"
            });
            order++;
        }
        return list;
    }

    private static Guid ResolveSectionId(IReadOnlyList<PlanSection> sections, QuestionType type, string? focusArea)
    {
        if (sections.Count == 0) return Guid.Empty;

        if (!string.IsNullOrWhiteSpace(focusArea))
        {
            var byFocus = sections.FirstOrDefault(s =>
                s.Name.Contains(focusArea, StringComparison.OrdinalIgnoreCase)
                || (focusArea?.Contains(s.Name, StringComparison.OrdinalIgnoreCase) ?? false));
            if (byFocus is not null) return byFocus.Id;
        }

        var typeName = type.ToString();
        var byType = sections.FirstOrDefault(s =>
            s.Name.Contains(typeName, StringComparison.OrdinalIgnoreCase)
            || typeName.Contains(s.Name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
        return byType?.Id ?? sections[0].Id;
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
