using System.Text.Json;
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
        GeneratedQuestion question,
        IReadOnlyList<QuestionAiChatMessage> chatHistory,
        string hrMessage)
    {
        object? planObj = null;
        if (job.Plan is not null && !string.IsNullOrWhiteSpace(job.Plan.PlanJson))
            planObj = JsonSerializer.Deserialize<object>(job.Plan.PlanJson, JsonOptions);

        var skills = JsonSerializer.Deserialize<List<string>>(job.SkillsJson, JsonOptions) ?? new List<string>();
        var citations = ParseJsonArray(question.CitationsJson);
        var evaluationCriteria = ParseJsonArray(question.EvaluationCriteriaJson);

        var isManuallyEdited = question.UpdatedAt.HasValue
            && question.UpdatedAt.Value > question.CreatedAt.AddSeconds(1);

        return new QuestionAssistRequest
        {
            OwnerId = job.OwnerId,
            HrMessage = hrMessage,
            Context = new QuestionAssistContextDto
            {
                JobDescription = job.JobDescription,
                HrNote = job.HrNote,
                Plan = planObj,
                Difficulty = job.Difficulty,
                Skills = skills,
                IsManuallyEdited = isManuallyEdited,
                CurrentQuestion = new
                {
                    id = question.Id,
                    order = question.Order,
                    question = question.Question,
                    questionType = question.QuestionType,
                    difficulty = question.Difficulty,
                    skill = question.Skill,
                    focusArea = question.FocusArea,
                    rationale = question.Rationale,
                    sampleAnswer = question.SampleAnswer,
                    citations,
                    evaluationCriteria
                }
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
