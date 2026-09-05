namespace ApplicationLayer.Helpers;

/// <summary>
/// SCRUM-439: Soft-select câu đưa lên marketplace (IsActive) — tách helper để unit test.
/// </summary>
public static class PublishQuestionSelectionHelper
{
    /// <summary>
    /// Đặt IsActive=true cho id trong selected; false cho các câu còn lại trong set.
    /// Trả về số câu được bật active.
    /// </summary>
    public static int ApplySelection(
        IEnumerable<(Guid Id, bool IsActive)> questions,
        IReadOnlyCollection<Guid> selectedIds,
        Action<Guid, bool> setActive)
    {
        var selected = selectedIds.Where(id => id != Guid.Empty).ToHashSet();
        if (selected.Count == 0)
            return questions.Count(q => q.IsActive);

        var owned = questions.Select(q => q.Id).ToHashSet();
        var unknown = selected.Where(id => !owned.Contains(id)).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException("Một số QuestionIds không thuộc bộ câu hỏi này.", nameof(selectedIds));

        var activeCount = 0;
        foreach (var q in questions)
        {
            var shouldActive = selected.Contains(q.Id);
            if (q.IsActive != shouldActive)
                setActive(q.Id, shouldActive);
            if (shouldActive)
                activeCount++;
        }

        return activeCount;
    }

    /// <summary>
    /// Map InterviewQuestion (Studio) → QuestionSetQuestion id theo Content rồi Order.
    /// </summary>
    public static List<Guid> MapInterviewSelectionToSetQuestionIds(
        IReadOnlyList<(Guid InterviewId, string Content, int OrderIndex)> selectedInterview,
        IReadOnlyList<(Guid SetQuestionId, string Question, int Order)> setQuestions)
    {
        var result = new List<Guid>();
        var used = new HashSet<Guid>();

        foreach (var iq in selectedInterview)
        {
            var content = (iq.Content ?? string.Empty).Trim();
            var byContent = setQuestions.FirstOrDefault(sq =>
                !used.Contains(sq.SetQuestionId)
                && string.Equals((sq.Question ?? string.Empty).Trim(), content, StringComparison.OrdinalIgnoreCase));

            var match = byContent.SetQuestionId != Guid.Empty
                ? byContent
                : setQuestions.FirstOrDefault(sq =>
                    !used.Contains(sq.SetQuestionId) && sq.Order == iq.OrderIndex);

            if (match.SetQuestionId == Guid.Empty)
                continue;

            used.Add(match.SetQuestionId);
            result.Add(match.SetQuestionId);
        }

        return result;
    }
}
