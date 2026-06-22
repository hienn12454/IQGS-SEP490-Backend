using System.Text.Json;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class QuestionSetService : IQuestionSetService
{
    private readonly IQuestionSetRepository _questionSetRepository;
    private readonly IQuestionGenerationJobRepository _jobRepository;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public QuestionSetService(
        IQuestionSetRepository questionSetRepository,
        IQuestionGenerationJobRepository jobRepository)
    {
        _questionSetRepository = questionSetRepository;
        _jobRepository = jobRepository;
    }

    public async Task<SaveDraftResponseDto> SaveDraftFromJobAsync(Guid jobId, Guid ownerId)
    {
        var job = await _jobRepository.GetByIdWithPlanAndQuestionsAsync(jobId)
            ?? throw new NotFoundException("Job không tồn tại.");

        if (job.OwnerId != ownerId)
            throw new ForbiddenException("Bạn không có quyền truy cập job này.");

        if (job.Status != QuestionGenerationJobStatus.Completed)
            throw new BadRequestException("Chỉ lưu draft khi session ở trạng thái COMPLETED.");

        var questions = job.Questions.OrderBy(q => q.Order).ToList();
        if (questions.Count == 0)
            throw new BadRequestException("Session chưa có câu hỏi để lưu draft.");

        if (await _questionSetRepository.ExistsBySourceJobIdAsync(jobId))
            throw new ConflictException("Session này đã được lưu draft trước đó.");

        var title = TryExtractRoleTitle(job.Plan?.PlanJson);

        var questionSet = new QuestionSet
        {
            OwnerId = ownerId,
            SourceJobId = jobId,
            Status = QuestionSetStatus.Draft,
            Title = title,
            JobDescription = job.JobDescription,
            HrNote = job.HrNote,
            PlanJson = job.Plan?.PlanJson ?? "{}",
            GeneratedAt = job.CompletedAt
        };

        var snapshotQuestions = questions.Select(q => new QuestionSetQuestion
        {
            QuestionSetId = questionSet.Id,
            Order = q.Order,
            Question = q.Question,
            QuestionType = q.QuestionType,
            Difficulty = q.Difficulty,
            Skill = q.Skill,
            FocusArea = q.FocusArea,
            Rationale = q.Rationale,
            SampleAnswer = q.SampleAnswer,
            EvaluationCriteriaJson = q.EvaluationCriteriaJson,
            CitationsJson = q.CitationsJson
        }).ToList();

        await _questionSetRepository.AddAsync(questionSet, snapshotQuestions);

        return new SaveDraftResponseDto
        {
            QuestionSetId = questionSet.Id,
            Status = questionSet.Status,
            SourceJobId = jobId,
            QuestionCount = snapshotQuestions.Count,
            SavedAt = questionSet.CreatedAt
        };
    }

    public async Task<QuestionSetDetailResponseDto> GetQuestionSetAsync(Guid questionSetId, Guid ownerId)
    {
        var questionSet = await _questionSetRepository.GetByIdWithQuestionsAsync(questionSetId)
            ?? throw new NotFoundException("Question set không tồn tại.");

        if (questionSet.OwnerId != ownerId)
            throw new ForbiddenException("Bạn không có quyền truy cập question set này.");

        object? planObj = null;
        if (!string.IsNullOrWhiteSpace(questionSet.PlanJson))
            planObj = JsonSerializer.Deserialize<object>(questionSet.PlanJson, JsonOptions);

        return new QuestionSetDetailResponseDto
        {
            QuestionSetId = questionSet.Id,
            Status = questionSet.Status,
            SourceJobId = questionSet.SourceJobId,
            Title = questionSet.Title,
            JobDescription = questionSet.JobDescription,
            HrNote = questionSet.HrNote,
            Plan = planObj,
            GeneratedAt = questionSet.GeneratedAt,
            SavedAt = questionSet.CreatedAt,
            Questions = questionSet.Questions
                .OrderBy(q => q.Order)
                .Select(q => new QuestionSetQuestionResponseDto
                {
                    Id = q.Id,
                    Order = q.Order,
                    Question = q.Question,
                    QuestionType = q.QuestionType,
                    Difficulty = q.Difficulty,
                    Skill = q.Skill,
                    FocusArea = q.FocusArea,
                    Rationale = q.Rationale,
                    SampleAnswer = q.SampleAnswer,
                    EvaluationCriteria = JsonSerializer.Deserialize<List<object>>(q.EvaluationCriteriaJson, JsonOptions) ?? new(),
                    Citations = JsonSerializer.Deserialize<List<object>>(q.CitationsJson, JsonOptions) ?? new()
                })
                .ToList()
        };
    }

    private static string? TryExtractRoleTitle(string? planJson)
    {
        if (string.IsNullOrWhiteSpace(planJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(planJson);
            if (doc.RootElement.TryGetProperty("roleTitle", out var roleTitle))
                return roleTitle.GetString();
        }
        catch (JsonException)
        {
            // PlanJson lỗi format — bỏ qua title, không chặn save draft
        }

        return null;
    }
}
