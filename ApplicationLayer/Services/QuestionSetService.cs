using System.Text.Json;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Mapping;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class QuestionSetService : IQuestionSetService
{
    private readonly IQuestionSetRepository _questionSetRepository;
    private readonly IQuestionGenerationJobRepository _jobRepository;
    private readonly IPlatformSettingsRepository _platformSettingsRepository;
    private readonly IHRProfileRepository _hrProfileRepository;
    private readonly ICompanyRepository _companyRepository;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public QuestionSetService(
        IQuestionSetRepository questionSetRepository,
        IQuestionGenerationJobRepository jobRepository,
        IPlatformSettingsRepository platformSettingsRepository,
        IHRProfileRepository hrProfileRepository,
        ICompanyRepository companyRepository)
    {
        _questionSetRepository = questionSetRepository;
        _jobRepository = jobRepository;
        _platformSettingsRepository = platformSettingsRepository;
        _hrProfileRepository = hrProfileRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>Lấy tên + logo công ty của HR sở hữu (qua HRProfile.CompanyId) — dùng gán vào response question set cho HR.</summary>
    private async Task<(string Name, string Logo)> GetOwnerCompanyInfoAsync(Guid ownerId)
    {
        var hrProfile = await _hrProfileRepository.GetByUserIdAsync(ownerId);
        if (hrProfile is null)
            return (string.Empty, CompanyLogoResolver.Resolve(null, null, string.Empty));

        var company = await _companyRepository.GetByIdAsync(hrProfile.CompanyId);
        var name = company?.Name ?? string.Empty;
        return (name, CompanyLogoResolver.Resolve(company?.LogoUrl, company?.WebsiteUrl, name));
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

        var (companyName, companyLogo) = await GetOwnerCompanyInfoAsync(ownerId);

        return new SaveDraftResponseDto
        {
            QuestionSetId = questionSet.Id,
            Status = questionSet.Status,
            SourceJobId = jobId,
            QuestionCount = snapshotQuestions.Count,
            SavedAt = questionSet.CreatedAt,
            CompanyName = companyName,
            CompanyLogo = companyLogo
        };
    }

    public async Task<IReadOnlyList<QuestionSetListItemDto>> ListQuestionSetsAsync(
        Guid ownerId, QuestionSetListQueryDto query)
    {
        var questionSets = await _questionSetRepository.ListByOwnerAsync(ownerId, query.JobId);

        // Cùng 1 ownerId -> cùng 1 công ty cho mọi item trong list — chỉ cần lookup 1 lần, tránh N+1 query.
        var (companyName, companyLogo) = await GetOwnerCompanyInfoAsync(ownerId);

        return questionSets.Select(qs => new QuestionSetListItemDto
        {
            QuestionSetId = qs.Id,
            JobId = qs.SourceJobId,
            Title = qs.Title,
            Status = qs.Status,
            CompanyName = companyName,
            CompanyLogo = companyLogo,
            SavedAt = qs.CreatedAt,
            PublishedAt = qs.PublishedAt
        }).ToList();
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

        var (companyName, companyLogo) = await GetOwnerCompanyInfoAsync(ownerId);

        return new QuestionSetDetailResponseDto
        {
            QuestionSetId = questionSet.Id,
            Status = questionSet.Status,
            SourceJobId = questionSet.SourceJobId,
            Title = questionSet.Title,
            CompanyName = companyName,
            CompanyLogo = companyLogo,
            JobDescription = questionSet.JobDescription,
            HrNote = questionSet.HrNote,
            TimeLimitMinutes = questionSet.TimeLimitMinutes,
            Plan = planObj,
            GeneratedAt = questionSet.GeneratedAt,
            SavedAt = questionSet.CreatedAt,
            PublishedAt = questionSet.PublishedAt,
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

    public async Task<QuestionSetQuestionResponseDto> UpdateQuestionAsync(
        Guid questionSetId, Guid questionId, Guid ownerId, UpdateQuestionRequestDto dto)
    {
        await EnsureEditableQuestionSetAsync(questionSetId, ownerId);

        var question = await _questionSetRepository.GetQuestionByIdAsync(questionId)
            ?? throw new NotFoundException("Câu hỏi không tồn tại.");

        if (question.QuestionSetId != questionSetId)
            throw new NotFoundException("Câu hỏi không thuộc question set này.");

        ApplyQuestionFields(question, dto.Question, dto.QuestionType, dto.Difficulty,
            dto.Skill, dto.FocusArea, dto.Rationale, dto.SampleAnswer,
            dto.EvaluationCriteria, dto.Citations);

        await _questionSetRepository.UpdateQuestionAsync(question);
        return MapQuestion(question);
    }

    public async Task<QuestionSetQuestionResponseDto> AddQuestionAsync(
        Guid questionSetId, Guid ownerId, CreateQuestionRequestDto dto)
    {
        await EnsureEditableQuestionSetAsync(questionSetId, ownerId);
        ValidateQuestionInput(dto.Question, dto.QuestionType, dto.Difficulty);

        var order = dto.Order ?? (await _questionSetRepository.GetMaxOrderByQuestionSetIdAsync(questionSetId) + 1);
        if (order <= 0)
            throw new BadRequestException("order phải lớn hơn 0.");

        var question = new QuestionSetQuestion
        {
            QuestionSetId = questionSetId,
            Order = order,
            Question = dto.Question.Trim(),
            QuestionType = QuestionTypeNormalizer.Normalize(new List<string> { dto.QuestionType }).First(),
            Difficulty = dto.Difficulty.Trim(),
            Skill = dto.Skill?.Trim(),
            FocusArea = dto.FocusArea?.Trim(),
            Rationale = dto.Rationale?.Trim(),
            SampleAnswer = dto.SampleAnswer?.Trim(),
            EvaluationCriteriaJson = JsonSerializer.Serialize(dto.EvaluationCriteria, JsonOptions),
            CitationsJson = JsonSerializer.Serialize(dto.Citations, JsonOptions)
        };

        await _questionSetRepository.AddQuestionAsync(question);
        return MapQuestion(question);
    }

    public async Task DeleteQuestionAsync(Guid questionSetId, Guid questionId, Guid ownerId)
    {
        await EnsureEditableQuestionSetAsync(questionSetId, ownerId);

        var count = await _questionSetRepository.GetQuestionCountByQuestionSetIdAsync(questionSetId);
        if (count <= 1)
            throw new BadRequestException("Không thể xóa câu hỏi cuối cùng.");

        var question = await _questionSetRepository.GetQuestionByIdAsync(questionId)
            ?? throw new NotFoundException("Câu hỏi không tồn tại.");

        if (question.QuestionSetId != questionSetId)
            throw new NotFoundException("Câu hỏi không thuộc question set này.");

        await _questionSetRepository.DeleteQuestionAsync(question);
        await NormalizeQuestionOrdersAsync(questionSetId);
    }

    public async Task<IReadOnlyList<QuestionSetQuestionResponseDto>> ReorderQuestionsAsync(
        Guid questionSetId, Guid ownerId, ReorderQuestionsRequestDto dto)
    {
        await EnsureEditableQuestionSetAsync(questionSetId, ownerId);

        if (dto.Items is null || dto.Items.Count == 0)
            throw new BadRequestException("items không được rỗng.");

        var existing = await _questionSetRepository.GetQuestionsByQuestionSetIdAsync(questionSetId);
        if (dto.Items.Count != existing.Count)
            throw new BadRequestException("Danh sách reorder phải chứa đủ tất cả câu hỏi.");

        var existingIds = existing.Select(q => q.Id).ToHashSet();
        foreach (var item in dto.Items)
        {
            if (!existingIds.Contains(item.QuestionId))
                throw new BadRequestException("questionId không thuộc question set này.");
            if (item.Order <= 0)
                throw new BadRequestException("order phải lớn hơn 0.");
        }

        var orders = dto.Items.Select(i => i.Order).ToList();
        if (orders.Distinct().Count() != orders.Count)
            throw new BadRequestException("order bị trùng.");

        foreach (var item in dto.Items)
        {
            var question = existing.First(q => q.Id == item.QuestionId);
            question.Order = item.Order;
            await _questionSetRepository.UpdateQuestionAsync(question);
        }

        return (await _questionSetRepository.GetQuestionsByQuestionSetIdAsync(questionSetId))
            .OrderBy(q => q.Order)
            .Select(MapQuestion)
            .ToList();
    }

    public async Task<QuestionSetActionResponseDto> PublishAsync(Guid questionSetId, Guid ownerId)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        if (questionSet.Status == QuestionSetStatus.Published)
            throw new ConflictException("Bộ câu hỏi đã được publish trước đó.");

        var minQuestionsToPublish = (await _platformSettingsRepository.GetAsync()).MinQuestionsToPublish;
        var activeQuestionCount = questionSet.Questions.Count(q => q.IsActive);
        if (activeQuestionCount < minQuestionsToPublish)
            throw new BadRequestException(
                $"Bộ câu hỏi cần tối thiểu {minQuestionsToPublish} câu hỏi để publish (hiện có {activeQuestionCount}).");

        questionSet.Status = QuestionSetStatus.Published;
        questionSet.PublishedAt = DateTime.UtcNow;
        questionSet.UpdatedAt = DateTime.UtcNow;

        await _questionSetRepository.UpdateAsync(questionSet);

        return new QuestionSetActionResponseDto
        {
            QuestionSetId = questionSet.Id,
            Status = questionSet.Status,
            PublishedAt = questionSet.PublishedAt
        };
    }

    public async Task<SetTimeLimitResponseDto> SetTimeLimitAsync(
        Guid questionSetId, Guid ownerId, SetTimeLimitRequestDto dto)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        if (questionSet.Status == QuestionSetStatus.Published)
            throw new ConflictException("Bộ câu hỏi đang PUBLISHED — unpublish trước khi đổi giới hạn thời gian.");

        questionSet.TimeLimitMinutes = dto.TimeLimitMinutes;
        questionSet.UpdatedAt = DateTime.UtcNow;
        await _questionSetRepository.UpdateAsync(questionSet);

        return new SetTimeLimitResponseDto
        {
            QuestionSetId = questionSet.Id,
            TimeLimitMinutes = questionSet.TimeLimitMinutes
        };
    }

    public async Task<QuestionSetActionResponseDto> UnpublishAsync(Guid questionSetId, Guid ownerId)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        if (questionSet.Status != QuestionSetStatus.Published)
            throw new ConflictException("Bộ câu hỏi hiện không ở trạng thái PUBLISHED.");

        questionSet.Status = QuestionSetStatus.Draft;
        questionSet.PublishedAt = null;
        questionSet.UpdatedAt = DateTime.UtcNow;

        await _questionSetRepository.UpdateAsync(questionSet);

        return new QuestionSetActionResponseDto
        {
            QuestionSetId = questionSet.Id,
            Status = questionSet.Status,
            PublishedAt = questionSet.PublishedAt
        };
    }

    private async Task<QuestionSet> EnsureOwnedQuestionSetAsync(Guid questionSetId, Guid ownerId)
    {
        var questionSet = await _questionSetRepository.GetByIdWithQuestionsAsync(questionSetId)
            ?? throw new NotFoundException("Question set không tồn tại.");

        if (questionSet.OwnerId != ownerId)
            throw new ForbiddenException("Bạn không có quyền truy cập question set này.");

        return questionSet;
    }

    /// <summary>Như <see cref="EnsureOwnedQuestionSetAsync"/> nhưng chặn sửa câu hỏi khi bộ đang PUBLISHED trên marketplace.</summary>
    private async Task<QuestionSet> EnsureEditableQuestionSetAsync(Guid questionSetId, Guid ownerId)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        if (questionSet.Status == QuestionSetStatus.Published)
            throw new ConflictException("Bộ câu hỏi đang publish trên marketplace. Vui lòng unpublish trước khi chỉnh sửa câu hỏi.");

        return questionSet;
    }

    private static void ValidateQuestionInput(string question, string questionType, string difficulty)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new BadRequestException("question không được để trống.");
        if (string.IsNullOrWhiteSpace(questionType))
            throw new BadRequestException("questionType không được để trống.");
        if (string.IsNullOrWhiteSpace(difficulty))
            throw new BadRequestException("difficulty không được để trống.");
    }

    private void ApplyQuestionFields(
        QuestionSetQuestion question,
        string text,
        string questionType,
        string difficulty,
        string? skill,
        string? focusArea,
        string? rationale,
        string? sampleAnswer,
        List<object> evaluationCriteria,
        List<object> citations)
    {
        ValidateQuestionInput(text, questionType, difficulty);

        question.Question = text.Trim();
        question.QuestionType = QuestionTypeNormalizer.Normalize(new List<string> { questionType }).First();
        question.Difficulty = difficulty.Trim();
        question.Skill = skill?.Trim();
        question.FocusArea = focusArea?.Trim();
        question.Rationale = rationale?.Trim();
        question.SampleAnswer = sampleAnswer?.Trim();
        question.EvaluationCriteriaJson = JsonSerializer.Serialize(evaluationCriteria, JsonOptions);
        question.CitationsJson = JsonSerializer.Serialize(citations, JsonOptions);
    }

    private async Task NormalizeQuestionOrdersAsync(Guid questionSetId)
    {
        var questions = await _questionSetRepository.GetQuestionsByQuestionSetIdAsync(questionSetId);
        var ordered = questions.OrderBy(q => q.Order).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var expected = i + 1;
            if (ordered[i].Order == expected)
                continue;
            ordered[i].Order = expected;
            await _questionSetRepository.UpdateQuestionAsync(ordered[i]);
        }
    }

    private QuestionSetQuestionResponseDto MapQuestion(QuestionSetQuestion q) => new()
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
    };

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
