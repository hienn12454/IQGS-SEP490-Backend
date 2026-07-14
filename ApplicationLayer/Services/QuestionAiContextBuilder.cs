using System.Text.Json;
using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.DTOs.Rag;
using DomainLayer.Entities;

namespace ApplicationLayer.Services;

/// <summary>
/// Gom context JD/plan/question/citations/chat history để gửi sang RAG Ask AI (SCRUM-215).
/// </summary>
public class QuestionAiContextBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public const int MaxChatHistoryMessages = 20;

    public QuestionAssistRequest Build(
        QuestionGenerationJob job,
        QuestionSet? questionSet,
        GeneratedQuestion identityQuestion,
        QuestionSnapshot snapshot,
        IReadOnlyList<QuestionAiChatMessage> chatHistory,
        string hrMessage)
    {
        object? planObj = null;
        var planJson = questionSet?.PlanJson ?? job.Plan?.PlanJson;
        if (!string.IsNullOrWhiteSpace(planJson))
            planObj = JsonSerializer.Deserialize<object>(planJson, JsonOptions);

        var skills = JsonSerializer.Deserialize<List<string>>(job.SkillsJson, JsonOptions) ?? new List<string>();
        var citations = ParseJsonArray(snapshot.CitationsJson);
        var evaluationCriteria = ParseJsonArray(snapshot.EvaluationCriteriaJson);

        var isManuallyEdited = !string.Equals(
            snapshot.Question,
            identityQuestion.Question,
            StringComparison.Ordinal);

        return new QuestionAssistRequest
        {
            OwnerId = job.OwnerId,
            HrMessage = hrMessage,
            Context = new QuestionAssistContextDto
            {
                JobDescription = questionSet?.JobDescription ?? job.JobDescription,
                HrNote = questionSet?.HrNote ?? job.HrNote,
                Plan = planObj,
                Difficulty = job.Difficulty,
                Skills = skills,
                IsManuallyEdited = isManuallyEdited,
                CurrentQuestion = BuildCurrentQuestionObject(identityQuestion, snapshot, citations, evaluationCriteria)
            },
            ChatHistory = chatHistory
                .OrderBy(m => m.CreatedAt)
                .Select(m => new QuestionAssistChatMessageDto
                {
                    Role = m.Role,
                    Content = m.Content
                })
                .ToList()
        };
    }

    public static QuestionSnapshot ResolveBaseSnapshot(
        GeneratedQuestion generated,
        QuestionSet? questionSet)
    {
        if (questionSet is null)
            return QuestionSnapshot.FromGenerated(generated);

        var saved = questionSet.Questions.FirstOrDefault(q => q.Order == generated.Order);
        return saved is not null
            ? QuestionSnapshot.FromSaved(saved)
            : QuestionSnapshot.FromGenerated(generated);
    }

    private static object BuildCurrentQuestionObject(
        GeneratedQuestion identityQuestion,
        QuestionSnapshot snapshot,
        List<object> citations,
        List<object> evaluationCriteria)
    {
        return new
        {
            id = identityQuestion.Id,
            order = identityQuestion.Order,
            question = snapshot.Question,
            questionType = snapshot.QuestionType,
            difficulty = snapshot.Difficulty,
            skill = snapshot.Skill,
            focusArea = snapshot.FocusArea,
            rationale = snapshot.Rationale,
            sampleAnswer = snapshot.SampleAnswer,
            citations,
            evaluationCriteria
        };
    }

    private static List<object> ParseJsonArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<object>();

        try
        {
            return JsonSerializer.Deserialize<List<object>>(json, JsonOptions) ?? new List<object>();
        }
        catch
        {
            return new List<object>();
        }
    }
}

/// <summary>Snapshot nội dung câu hỏi sau khi chọn nguồn + merge FE override.</summary>
public sealed class QuestionSnapshot
{
    public string Question { get; init; } = string.Empty;
    public string QuestionType { get; init; } = string.Empty;
    public string Difficulty { get; init; } = string.Empty;
    public string? Skill { get; init; }
    public string? FocusArea { get; init; }
    public string? Rationale { get; init; }
    public string? SampleAnswer { get; init; }
    public string EvaluationCriteriaJson { get; init; } = "[]";
    public string CitationsJson { get; init; } = "[]";

    public static QuestionSnapshot FromGenerated(GeneratedQuestion question) => new()
    {
        Question = question.Question,
        QuestionType = question.QuestionType,
        Difficulty = question.Difficulty,
        Skill = question.Skill,
        FocusArea = question.FocusArea,
        Rationale = question.Rationale,
        SampleAnswer = question.SampleAnswer,
        EvaluationCriteriaJson = question.EvaluationCriteriaJson,
        CitationsJson = question.CitationsJson
    };

    public static QuestionSnapshot FromSaved(QuestionSetQuestion question) => new()
    {
        Question = question.Question,
        QuestionType = question.QuestionType,
        Difficulty = question.Difficulty,
        Skill = question.Skill,
        FocusArea = question.FocusArea,
        Rationale = question.Rationale,
        SampleAnswer = question.SampleAnswer,
        EvaluationCriteriaJson = question.EvaluationCriteriaJson,
        CitationsJson = question.CitationsJson
    };

    public QuestionSnapshot MergeWith(CurrentQuestionOverrideDto? ov)
    {
        if (ov is null)
            return this;

        return new QuestionSnapshot
        {
            Question = Pick(ov.Question, Question),
            QuestionType = Pick(ov.QuestionType, QuestionType),
            Difficulty = Pick(ov.Difficulty, Difficulty),
            Skill = PickNullable(ov.Skill, Skill),
            FocusArea = PickNullable(ov.FocusArea, FocusArea),
            Rationale = PickNullable(ov.Rationale, Rationale),
            SampleAnswer = PickNullable(ov.SampleAnswer, SampleAnswer),
            EvaluationCriteriaJson = EvaluationCriteriaJson,
            CitationsJson = CitationsJson
        };
    }

    private static string Pick(string? overrideValue, string fallback)
        => string.IsNullOrWhiteSpace(overrideValue) ? fallback : overrideValue.Trim();

    private static string? PickNullable(string? overrideValue, string? fallback)
        => string.IsNullOrWhiteSpace(overrideValue) ? fallback : overrideValue.Trim();
}
