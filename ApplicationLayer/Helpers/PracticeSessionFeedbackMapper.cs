using System.Text.Json;
using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.QuestionSet;
using DomainLayer.Constants;
using DomainLayer.Entities;

namespace ApplicationLayer.Helpers;

/// <summary>
/// Map phiên luyện tập → feedback DTO.
/// Candidate: lockTeaser=true (Free chỉ mở 1 câu). HR: lockTeaser=false (xem mọi AI đã persist).
/// </summary>
public static class PracticeSessionFeedbackMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static PracticeSessionFeedbackDto Map(
        PracticeSession session,
        IReadOnlyList<PublishedQuestionRow> questions,
        IReadOnlyList<CandidateAnswer> answers,
        IReadOnlyList<AiFeedback> feedbacks,
        bool lockTeaser)
    {
        var feedbackByAnswerId = feedbacks.ToDictionary(f => f.CandidateAnswerId);
        var answerByQuestionId = answers.ToDictionary(a => a.QuestionSetQuestionId);

        var teaserQuestionIds = new HashSet<Guid>();
        if (lockTeaser)
        {
            foreach (var answer in answers)
            {
                if (feedbackByAnswerId.TryGetValue(answer.Id, out var fb)
                    && fb.EvaluationStatus == AiFeedbackEvaluationStatus.Succeeded
                    && fb.Score.HasValue)
                {
                    teaserQuestionIds.Add(answer.QuestionSetQuestionId);
                }
            }
        }

        var items = new List<PracticeSessionFeedbackItemDto>();
        foreach (var q in questions.OrderBy(x => x.Order))
        {
            var hasAnswer = answerByQuestionId.TryGetValue(q.Id, out var answer);
            var answerText = hasAnswer ? (answer!.AnswerText ?? string.Empty) : string.Empty;
            AiFeedback? feedback = null;
            if (hasAnswer)
                feedbackByAnswerId.TryGetValue(answer!.Id, out feedback);

            var isTeaser = lockTeaser && teaserQuestionIds.Contains(q.Id);
            var isLocked = lockTeaser && !isTeaser;

            if (isLocked)
            {
                items.Add(new PracticeSessionFeedbackItemDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.Question,
                    QuestionType = q.QuestionType,
                    Difficulty = q.Difficulty,
                    AnswerText = answerText,
                    Score = null,
                    Strengths = [],
                    Improvements = [],
                    Suggestion = null,
                    DimensionScores = null,
                    EvaluationStatus = AiFeedbackEvaluationStatus.Pending,
                    IsLocked = true,
                    IsTeaser = false
                });
                continue;
            }

            items.Add(new PracticeSessionFeedbackItemDto
            {
                QuestionId = q.Id,
                QuestionText = q.Question,
                QuestionType = q.QuestionType,
                Difficulty = q.Difficulty,
                AnswerText = answerText,
                Score = feedback?.Score,
                Strengths = DeserializeStringList(feedback?.StrengthsJson),
                Improvements = DeserializeStringList(feedback?.ImprovementsJson),
                Suggestion = feedback?.Suggestion,
                DimensionScores = DeserializeDimensionScores(feedback?.DimensionScoresJson),
                EvaluationStatus = feedback?.EvaluationStatus ?? AiFeedbackEvaluationStatus.Pending,
                IsLocked = false,
                IsTeaser = isTeaser
            });
        }

        double? overall = session.OverallScore;
        if (overall is null && !lockTeaser)
        {
            var succeededScores = items
                .Where(i =>
                    i.EvaluationStatus == AiFeedbackEvaluationStatus.Succeeded
                    && i.Score.HasValue)
                .Select(i => i.Score!.Value);
            overall = PracticeOverallScoreCalculator.Compute(succeededScores, questions.Count);
        }

        return new PracticeSessionFeedbackDto
        {
            SessionId = session.Id,
            OverallScore = overall,
            Status = session.Status,
            AccessLevel = lockTeaser ? PracticeFeedbackAccessLevel.FreeTeaser : PracticeFeedbackAccessLevel.Full,
            AiInsight = lockTeaser ? null : MapAiInsight(session),
            Items = items
        };
    }

    public static PracticeAiInsightDto? MapAiInsight(PracticeSession session)
    {
        if (string.IsNullOrWhiteSpace(session.AiInsightVi) || string.IsNullOrWhiteSpace(session.AiInsightEn))
            return null;

        return new PracticeAiInsightDto
        {
            Vi = session.AiInsightVi,
            En = session.AiInsightEn,
            SkillsToImprove = DeserializeSkillsToImprove(session.SkillsToImproveJson)
        };
    }

    private static BilingualStringListDto DeserializeSkillsToImprove(string? json)
    {
        var empty = new BilingualStringListDto();
        if (string.IsNullOrWhiteSpace(json))
            return empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new BilingualStringListDto
            {
                Vi = ReadStringArray(root, "vi"),
                En = ReadStringArray(root, "en")
            };
        }
        catch (JsonException)
        {
            return empty;
        }
    }

    private static List<string> ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
            return [];
        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s);
            }
        }
        return list;
    }

    public static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static Dictionary<string, double>? DeserializeDimensionScores(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
