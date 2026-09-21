using System.Text.Json;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.Helpers;
using ApplicationLayer.DTOs.QuestionGeneration;
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
    private readonly IPlatformSettingsRepository _platformSettingsRepository;
    private readonly IHrCompanyInfoService _hrCompanyInfoService;
    private readonly IPracticeSessionRepository _practiceSessionRepository;
    private readonly IHrQuestionSetBookmarkRepository _bookmarkRepository;
    private readonly ISubscriptionGateService _subscriptionGate;
    private readonly IBlobStorageService _blobStorage;
    private readonly IRagService _ragService;
    private readonly IQuestionSetFeedbackRepository _feedbackRepository;

    private static readonly HashSet<string> AllowedQuestionImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp"
    };
    private const long MaxQuestionImageBytes = 5 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public QuestionSetService(
        IQuestionSetRepository questionSetRepository,
        IQuestionGenerationJobRepository jobRepository,
        IPlatformSettingsRepository platformSettingsRepository,
        IHrCompanyInfoService hrCompanyInfoService,
        IPracticeSessionRepository practiceSessionRepository,
        IHrQuestionSetBookmarkRepository bookmarkRepository,
        ISubscriptionGateService subscriptionGate,
        IBlobStorageService blobStorage,
        IRagService ragService,
        IQuestionSetFeedbackRepository feedbackRepository)
    {
        _questionSetRepository = questionSetRepository;
        _jobRepository = jobRepository;
        _platformSettingsRepository = platformSettingsRepository;
        _hrCompanyInfoService = hrCompanyInfoService;
        _practiceSessionRepository = practiceSessionRepository;
        _bookmarkRepository = bookmarkRepository;
        _subscriptionGate = subscriptionGate;
        _blobStorage = blobStorage;
        _ragService = ragService;
        _feedbackRepository = feedbackRepository;
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

        var (companyName, companyLogo) = await _hrCompanyInfoService.GetByHrUserIdAsync(ownerId);

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

    /// <summary>SCRUM-397: tạo bộ DRAFT rỗng để HR soạn câu hỏi thủ công trên Question Builder.</summary>
    public async Task<SaveDraftResponseDto> CreateManualDraftAsync(
        Guid ownerId, CreateManualDraftQuestionSetRequestDto dto)
    {
        var title = (dto.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new BadRequestException("Tiêu đề không được để trống.");
        if (title.Length > 500)
            throw new BadRequestException("Tiêu đề không được vượt quá 500 ký tự.");

        var description = dto.Description?.Trim();
        if (description is { Length: > 2000 })
            throw new BadRequestException("Mô tả không được vượt quá 2000 ký tự.");

        var questionSet = new QuestionSet
        {
            OwnerId = ownerId,
            Status = QuestionSetStatus.Draft,
            Title = title,
            // Không có cột Description — dùng HrNote cho ghi chú bộ thủ công
            HrNote = string.IsNullOrWhiteSpace(description) ? null : description,
            JobDescription = string.Empty,
            PlanJson = "{}",
            GeneratedAt = null
        };

        await _questionSetRepository.AddAsync(questionSet, Array.Empty<QuestionSetQuestion>());

        var (companyName, companyLogo) = await _hrCompanyInfoService.GetByHrUserIdAsync(ownerId);

        return new SaveDraftResponseDto
        {
            QuestionSetId = questionSet.Id,
            Status = questionSet.Status,
            SourceJobId = null,
            SourceProjectId = null,
            QuestionCount = 0,
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
        var (companyName, companyLogo) = await _hrCompanyInfoService.GetByHrUserIdAsync(ownerId);
        var counts = await _questionSetRepository.GetQuestionCountsBySetIdsAsync(questionSets.Select(q => q.Id));
        var bookmarkedIds = await _bookmarkRepository.GetBookmarkedQuestionSetIdsAsync(ownerId);

        return questionSets.Select(qs => new QuestionSetListItemDto
        {
            QuestionSetId = qs.Id,
            JobId = qs.SourceJobId,
            SourceProjectId = qs.SourceProjectId,
            Title = qs.Title,
            Status = qs.Status,
            CompanyName = companyName,
            CompanyLogo = companyLogo,
            SavedAt = qs.CreatedAt,
            PublishedAt = qs.PublishedAt,
            QuestionCount = counts.TryGetValue(qs.Id, out var c) ? c : 0,
            IsBookmarked = bookmarkedIds.Contains(qs.Id)
        }).ToList();
    }

    public async Task<QuestionSetDetailResponseDto> GetQuestionSetAsync(Guid questionSetId, Guid ownerId)
    {
        var questionSet = await _questionSetRepository.GetByIdWithQuestionsAsync(questionSetId)
            ?? throw new NotFoundException("Question set không tồn tại.");

        if (!questionSet.IsActive)
            throw new NotFoundException("Question set không tồn tại.");

        if (questionSet.OwnerId != ownerId)
            throw new ForbiddenException("Bạn không có quyền truy cập question set này.");

        object? planObj = null;
        if (!string.IsNullOrWhiteSpace(questionSet.PlanJson))
            planObj = JsonSerializer.Deserialize<object>(questionSet.PlanJson, JsonOptions);

        var (companyName, companyLogo) = await _hrCompanyInfoService.GetByHrUserIdAsync(ownerId);

        string? jdFileUrl = null;
        if (!string.IsNullOrWhiteSpace(questionSet.JdBlobPath))
        {
            try
            {
                jdFileUrl = await _blobStorage.GenerateReadSasUrlAsync(
                    questionSet.JdBlobPath.Trim(), TimeSpan.FromHours(2));
            }
            catch { /* ignore */ }
        }

        return new QuestionSetDetailResponseDto
        {
            QuestionSetId = questionSet.Id,
            Status = questionSet.Status,
            SourceJobId = questionSet.SourceJobId,
            SourceProjectId = questionSet.SourceProjectId,
            Title = questionSet.Title,
            CompanyName = companyName,
            CompanyLogo = companyLogo,
            JobDescription = questionSet.JobDescription,
            JdSourceType = string.IsNullOrWhiteSpace(questionSet.JdSourceType) ? "PastedText" : questionSet.JdSourceType,
            JdOriginalFileName = questionSet.JdOriginalFileName,
            JdBlobPath = questionSet.JdBlobPath,
            JdFileUrl = jdFileUrl,
            PublicJobDescription = questionSet.PublicJobDescription,
            JobLocation = questionSet.JobLocation,
            WorkplaceType = questionSet.WorkplaceType,
            SalaryMin = questionSet.SalaryMin,
            SalaryMax = questionSet.SalaryMax,
            SalaryNegotiable = questionSet.SalaryNegotiable,
            JobExpertise = questionSet.JobExpertise,
            JobDomain = questionSet.JobDomain,
            HrNote = questionSet.HrNote,
            TimeLimitMinutes = questionSet.TimeLimitMinutes,
            AutoRecommendEnabled = questionSet.AutoRecommendEnabled,
            RecommendationMinScore = questionSet.RecommendationMinScore,
            IsHiringAssessment = questionSet.IsHiringAssessment,
            HrAntiCheatEnabled = questionSet.HrAntiCheatEnabled,
            Plan = planObj,
            GeneratedAt = questionSet.GeneratedAt,
            SavedAt = questionSet.CreatedAt,
            PublishedAt = questionSet.PublishedAt,
            Questions = (await Task.WhenAll(
                questionSet.Questions
                    // SCRUM-439: HR thấy cả inactive để tick lại khi publish
                    .OrderBy(q => q.Order)
                    .Select(async q => await MapQuestionAsync(q))))
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
            dto.AnswerMethod, dto.EvaluationCriteria, dto.Citations);

        await _questionSetRepository.UpdateQuestionAsync(question);
        return await MapQuestionAsync(question);
    }

    public async Task<QuestionSetQuestionResponseDto> AddQuestionAsync(
        Guid questionSetId, Guid ownerId, CreateQuestionRequestDto dto)
    {
        await EnsureEditableQuestionSetAsync(questionSetId, ownerId);
        ValidateQuestionInput(dto.Question, dto.QuestionType, dto.Difficulty);
        var answerMethod = AnswerMethodNormalizer.Require(dto.AnswerMethod);

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
            AnswerMethod = answerMethod,
            EvaluationCriteriaJson = SerializeEvaluationCriteria(dto.EvaluationCriteria),
            CitationsJson = JsonSerializer.Serialize(dto.Citations, JsonOptions)
        };

        await _questionSetRepository.AddQuestionAsync(question);
        return await MapQuestionAsync(question);
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

        return (await Task.WhenAll(
            (await _questionSetRepository.GetQuestionsByQuestionSetIdAsync(questionSetId))
                .OrderBy(q => q.Order)
                .Select(async q => await MapQuestionAsync(q))))
            .ToList();
    }

    public async Task<QuestionSetActionResponseDto> PublishAsync(
        Guid questionSetId, Guid ownerId, PublishQuestionSetRequestDto? request = null)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        if (questionSet.Status == QuestionSetStatus.Published)
            throw new ConflictException("Bộ câu hỏi đã được publish trước đó.");

        // SCRUM-439: soft-select câu đưa lên marketplace
        var selectedIds = request?.QuestionIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (selectedIds is { Count: > 0 })
        {
            try
            {
                PublishQuestionSelectionHelper.ApplySelection(
                    questionSet.Questions.Select(q => (q.Id, q.IsActive)),
                    selectedIds,
                    (id, shouldActive) =>
                    {
                        var q = questionSet.Questions.First(x => x.Id == id);
                        q.IsActive = shouldActive;
                        q.UpdatedAt = DateTime.UtcNow;
                    });
            }
            catch (ArgumentException ex)
            {
                throw new BadRequestException(ex.Message);
            }

            foreach (var q in questionSet.Questions)
                await _questionSetRepository.UpdateQuestionAsync(q);

            // Reload active flags on tracked entity
            questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);
        }

        // Time limit trong cùng request (null = không giới hạn)
        questionSet.TimeLimitMinutes = request?.TimeLimitMinutes;

        // SCRUM-439 UX: cấu hình gợi ý ứng viên khi publish (null = giữ nguyên)
        if (request?.AutoRecommendEnabled is bool autoRec)
            questionSet.AutoRecommendEnabled = autoRec;
        if (request?.RecommendationMinScore is double minScore)
            questionSet.RecommendationMinScore = RecommendationService.ResolveIntakeMinScore(minScore);

        // SCRUM-464: Practice vs Tuyển — null = giữ giá trị đã Save từ review
        if (request?.IsHiringAssessment is bool isHiring)
            questionSet.IsHiringAssessment = isHiring;
        if (request?.HrAntiCheatEnabled is bool hrAc)
            questionSet.HrAntiCheatEnabled = hrAc;
        if (!questionSet.IsHiringAssessment)
            questionSet.HrAntiCheatEnabled = false;

        if (questionSet.IsHiringAssessment
            && (string.IsNullOrWhiteSpace(questionSet.JobDescription)
                || questionSet.JobDescription.Trim().StartsWith("(Studio)", StringComparison.OrdinalIgnoreCase)))
            throw new BadRequestException("Bộ Tuyển cần Job Description hợp lệ trước khi publish.");

        if (HiringJdExposureHelper.RequiresPublicJobDescription(
                questionSet.IsHiringAssessment, questionSet.PublicJobDescription))
            throw new BadRequestException("Bộ Tuyển cần bản JD ngắn (PublicJobDescription) trước khi publish.");

        // SCRUM-468: tin tuyển cần location / expertise / domain / lương (hoặc thỏa thuận)
        if (HiringPostingHelper.RequiresHiringPostingFields(
                questionSet.IsHiringAssessment,
                questionSet.JobLocation,
                questionSet.JobExpertise,
                questionSet.JobDomain,
                questionSet.SalaryMin,
                questionSet.SalaryMax,
                questionSet.SalaryNegotiable))
            throw new BadRequestException(
                "Bộ Tuyển cần địa điểm, chuyên môn, lĩnh vực và lương (hoặc thỏa thuận) trước khi publish.");

        var minQuestionsToPublish = (await _platformSettingsRepository.GetAsync()).MinQuestionsToPublish;
        var activeQuestions = questionSet.Questions.Where(q => q.IsActive).ToList();
        if (activeQuestions.Count < minQuestionsToPublish)
            throw new BadRequestException(
                $"Bộ câu hỏi cần tối thiểu {minQuestionsToPublish} câu hỏi để publish (hiện có {activeQuestions.Count}).");

        foreach (var q in activeQuestions)
        {
            if (string.IsNullOrWhiteSpace(q.SampleAnswer))
                throw new BadRequestException($"Câu hỏi #{q.Order} thiếu đáp án mẫu — không thể publish.");

            var rubricDoc = RubricNormalizer.NormalizeFromJson(q.EvaluationCriteriaJson);
            if (!RubricNormalizer.IsPublishReady(rubricDoc))
                throw new BadRequestException(
                    $"Câu hỏi #{q.Order} thiếu tiêu chí chấm hợp lệ (weight=100%, mỗi tiêu chí ≥2 mốc).");
        }

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

    /// <summary>SCRUM-424: cho phép sửa khi PUBLISHED — chỉ ảnh hưởng gợi ý tương lai.</summary>
    public async Task<SetRecommendationSettingsResponseDto> SetRecommendationSettingsAsync(
        Guid questionSetId, Guid ownerId, SetRecommendationSettingsRequestDto dto)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        questionSet.AutoRecommendEnabled = dto.AutoRecommendEnabled;
        questionSet.RecommendationMinScore = RecommendationService.ResolveIntakeMinScore(dto.RecommendationMinScore);
        questionSet.UpdatedAt = DateTime.UtcNow;
        await _questionSetRepository.UpdateAsync(questionSet);

        return new SetRecommendationSettingsResponseDto
        {
            QuestionSetId = questionSet.Id,
            AutoRecommendEnabled = questionSet.AutoRecommendEnabled,
            RecommendationMinScore = questionSet.RecommendationMinScore
        };
    }

    /// <summary>SCRUM-464: cho phép sửa khi PUBLISHED — chỉ ảnh hưởng phiên practice mới.</summary>
    public async Task<SetHiringAssessmentResponseDto> SetHiringAssessmentAsync(
        Guid questionSetId, Guid ownerId, SetHiringAssessmentRequestDto dto)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        if (HiringJdExposureHelper.RequiresPublicJobDescription(
                dto.IsHiringAssessment, questionSet.PublicJobDescription))
            throw new BadRequestException("Bật Tuyển cần bản JD ngắn (PublicJobDescription) trước.");

        if (HiringPostingHelper.RequiresHiringPostingFields(
                dto.IsHiringAssessment,
                questionSet.JobLocation,
                questionSet.JobExpertise,
                questionSet.JobDomain,
                questionSet.SalaryMin,
                questionSet.SalaryMax,
                questionSet.SalaryNegotiable))
            throw new BadRequestException(
                "Bật Tuyển cần địa điểm, chuyên môn, lĩnh vực và lương (hoặc thỏa thuận) trước.");

        if (dto.IsHiringAssessment
            && (string.IsNullOrWhiteSpace(questionSet.JobDescription)
                || questionSet.JobDescription.Trim().StartsWith("(Studio)", StringComparison.OrdinalIgnoreCase)))
            throw new BadRequestException("Bật Tuyển cần Job Description gốc hợp lệ.");

        questionSet.IsHiringAssessment = dto.IsHiringAssessment;
        questionSet.HrAntiCheatEnabled = dto.IsHiringAssessment && dto.HrAntiCheatEnabled;
        questionSet.UpdatedAt = DateTime.UtcNow;
        await _questionSetRepository.UpdateAsync(questionSet);

        return new SetHiringAssessmentResponseDto
        {
            QuestionSetId = questionSet.Id,
            IsHiringAssessment = questionSet.IsHiringAssessment,
            HrAntiCheatEnabled = questionSet.HrAntiCheatEnabled
        };
    }

    /// <summary>SCRUM-465: bản JD ngắn cho candidate.</summary>
    public async Task<SetPublicJobDescriptionResponseDto> SetPublicJobDescriptionAsync(
        Guid questionSetId, Guid ownerId, SetPublicJobDescriptionRequestDto dto)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);
        var text = (dto.PublicJobDescription ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new BadRequestException("PublicJobDescription không được để trống.");
        if (text.Length > 20000)
            throw new BadRequestException("PublicJobDescription tối đa 20000 ký tự.");

        questionSet.PublicJobDescription = text;
        questionSet.UpdatedAt = DateTime.UtcNow;
        await _questionSetRepository.UpdateAsync(questionSet);

        return new SetPublicJobDescriptionResponseDto
        {
            QuestionSetId = questionSet.Id,
            PublicJobDescription = questionSet.PublicJobDescription ?? text,
            CharacterCount = text.Length
        };
    }

    /// <summary>SCRUM-468: metadata tin tuyển cho candidate (cho phép sửa khi PUBLISHED).</summary>
    public async Task<SetHiringPostingResponseDto> SetHiringPostingAsync(
        Guid questionSetId, Guid ownerId, SetHiringPostingRequestDto dto)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        var location = HiringPostingHelper.NormalizeOptionalText(dto.JobLocation, 200)
            ?? throw new BadRequestException("JobLocation không được để trống.");
        var expertise = HiringPostingHelper.NormalizeOptionalText(dto.JobExpertise, 120)
            ?? throw new BadRequestException("JobExpertise không được để trống.");
        var domain = HiringPostingHelper.NormalizeOptionalText(dto.JobDomain, 120)
            ?? throw new BadRequestException("JobDomain không được để trống.");

        string? workplace = null;
        if (!string.IsNullOrWhiteSpace(dto.WorkplaceType))
        {
            workplace = HiringPostingHelper.NormalizeWorkplaceType(dto.WorkplaceType)
                ?? throw new BadRequestException("WorkplaceType phải là AtOffice, Hybrid hoặc Remote.");
        }

        if (!HiringPostingHelper.HasValidSalary(dto.SalaryMin, dto.SalaryMax, dto.SalaryNegotiable))
            throw new BadRequestException(
                "Lương không hợp lệ — bật thỏa thuận hoặc nhập min/max dương (max ≥ min).");

        questionSet.JobLocation = location;
        questionSet.WorkplaceType = workplace;
        questionSet.SalaryMin = dto.SalaryNegotiable ? null : dto.SalaryMin;
        questionSet.SalaryMax = dto.SalaryNegotiable ? null : dto.SalaryMax;
        questionSet.SalaryNegotiable = dto.SalaryNegotiable;
        questionSet.JobExpertise = expertise;
        questionSet.JobDomain = domain;
        questionSet.UpdatedAt = DateTime.UtcNow;
        await _questionSetRepository.UpdateAsync(questionSet);

        return new SetHiringPostingResponseDto
        {
            QuestionSetId = questionSet.Id,
            JobLocation = questionSet.JobLocation,
            WorkplaceType = questionSet.WorkplaceType,
            SalaryMin = questionSet.SalaryMin,
            SalaryMax = questionSet.SalaryMax,
            SalaryNegotiable = questionSet.SalaryNegotiable,
            JobExpertise = questionSet.JobExpertise,
            JobDomain = questionSet.JobDomain
        };
    }

    public async Task<RenameQuestionSetTitleResponseDto> RenameTitleAsync(
        Guid questionSetId, Guid ownerId, RenameQuestionSetTitleRequestDto dto)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        // Chỉ đổi Title — không đụng Status, giữ nguyên DRAFT/PUBLISHED hiện tại (SCRUM-330 AC).
        questionSet.Title = dto.Title.Trim();
        questionSet.UpdatedAt = DateTime.UtcNow;
        await _questionSetRepository.UpdateAsync(questionSet);

        return new RenameQuestionSetTitleResponseDto
        {
            QuestionSetId = questionSet.Id,
            Title = questionSet.Title
        };
    }

    public async Task<UpdateQuestionSetJobDescriptionResponseDto> SetJobDescriptionFromTextAsync(
        Guid questionSetId, Guid ownerId, string jobDescription)
    {
        var jd = JobDescriptionValidator.Validate(jobDescription);
        await JdItClassifyHelper.EnsureItJobPostingAsync(_ragService, jd);
        return await PersistJobDescriptionAsync(questionSetId, ownerId, jd, "PastedText", null, null);
    }

    public async Task<UpdateQuestionSetJobDescriptionResponseDto> SetJobDescriptionFromFileAsync(
        Guid questionSetId, Guid ownerId, Stream file, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (ext is not ".pdf" and not ".docx" and not ".txt" and not ".jpg" and not ".jpeg" and not ".png")
            throw new BadRequestException("Chỉ hỗ trợ file PDF, DOCX, TXT, JPG, JPEG hoặc PNG.");

        var safeName = string.IsNullOrWhiteSpace(fileName) ? "jd.txt" : Path.GetFileName(fileName.Trim());

        // Buffer để vừa parse vừa upload blob
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        if (ms.Length == 0)
            throw new BadRequestException("File rỗng.");
        if (ms.Length > 20 * 1024 * 1024)
            throw new BadRequestException("File JD tối đa 20 MB.");

        ms.Position = 0;
        var parsed = await _ragService.ParseJdAsync(ms, safeName, ct);
        if (!parsed.Success || string.IsNullOrWhiteSpace(parsed.JobDescription))
            throw new BadRequestException(parsed.Error ?? "Không đọc được Job Description từ file.");

        var jd = JobDescriptionValidator.Validate(parsed.JobDescription, safeName);
        // SCRUM-466 L2: classify trước khi lưu blob/JD
        await JdItClassifyHelper.EnsureItJobPostingAsync(_ragService, jd, ct: ct);

        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
        var blobPath = BlobPathHelper.BuildQuestionSetJobDescriptionPath(questionSetId, safeName);
        ms.Position = 0;
        await _blobStorage.UploadAsync(ms, contentType, blobPath, ct);

        return await PersistJobDescriptionAsync(questionSetId, ownerId, jd, "UploadedFile", safeName, blobPath);
    }

    private async Task<UpdateQuestionSetJobDescriptionResponseDto> PersistJobDescriptionAsync(
        Guid questionSetId, Guid ownerId, string jd, string sourceType, string? originalFileName, string? blobPath)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        if (sourceType == "PastedText" && !string.IsNullOrWhiteSpace(questionSet.JdBlobPath))
        {
            try { await _blobStorage.DeleteAsync(questionSet.JdBlobPath); }
            catch { /* ignore */ }
            questionSet.JdBlobPath = null;
        }
        else if (sourceType == "UploadedFile" && !string.IsNullOrWhiteSpace(blobPath))
        {
            if (!string.IsNullOrWhiteSpace(questionSet.JdBlobPath)
                && !string.Equals(questionSet.JdBlobPath, blobPath, StringComparison.Ordinal))
            {
                try { await _blobStorage.DeleteAsync(questionSet.JdBlobPath); }
                catch { /* ignore */ }
            }
            questionSet.JdBlobPath = blobPath;
        }

        questionSet.JobDescription = jd;
        questionSet.JdSourceType = sourceType;
        questionSet.JdOriginalFileName = sourceType == "UploadedFile" ? originalFileName : null;
        questionSet.UpdatedAt = DateTime.UtcNow;
        await _questionSetRepository.UpdateAsync(questionSet);
        return new UpdateQuestionSetJobDescriptionResponseDto
        {
            QuestionSetId = questionSet.Id,
            HasJobDescription = true,
            CharacterCount = jd.Length,
            JdSourceType = questionSet.JdSourceType,
            JdOriginalFileName = questionSet.JdOriginalFileName
        };
    }

    public async Task<IReadOnlyList<QuestionSetPractitionerDto>> GetPractitionersAsync(
        Guid questionSetId, Guid ownerId, bool includePractice = false)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        var rows = await _practiceSessionRepository.ListPractitionersByQuestionSetAsync(questionSetId);

        // SCRUM-464 / SCRUM-471: bộ Tuyển mặc định chỉ official (+ IN_PROGRESS);
        // includePractice=true → HR xem thêm phiên luyện.
        IEnumerable<QuestionSetPractitionerRow> filtered = rows;
        if (questionSet.IsHiringAssessment && !includePractice)
        {
            filtered = rows.Where(r =>
                r.IsOfficialTest
                || string.Equals(r.Status, PracticeSessionStatus.InProgress, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.Select(r => new QuestionSetPractitionerDto
        {
            SessionId = r.SessionId,
            CandidateUserId = r.CandidateUserId,
            CandidateName = r.CandidateName,
            CandidateEmail = r.CandidateEmail,
            TargetRole = r.TargetRole,
            SeniorityLevel = r.SeniorityLevel,
            Status = r.Status,
            OverallScore = r.OverallScore,
            StartedAt = r.StartedAt,
            CompletedAt = r.CompletedAt,
            IsOfficialTest = r.IsOfficialTest
        }).ToList();
    }

    public async Task<QuestionSetActionResponseDto> UnpublishAsync(Guid questionSetId, Guid ownerId)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        if (questionSet.Status != QuestionSetStatus.Published)
            throw new ConflictException("Bộ câu hỏi hiện không ở trạng thái PUBLISHED.");

        questionSet.Status = QuestionSetStatus.Draft;
        questionSet.PublishedAt = null;
        // SCRUM-404: unpublish thì bỏ pin khỏi Marketplace
        questionSet.IsPinned = false;
        questionSet.PinnedAt = null;
        questionSet.UpdatedAt = DateTime.UtcNow;

        await _questionSetRepository.UpdateAsync(questionSet);

        // SCRUM-408: hủy phiên đang làm — publish lại sẽ tạo phiên mới, không resume.
        var abandoned = await _practiceSessionRepository.AbandonInProgressByQuestionSetAsync(questionSet.Id);

        return new QuestionSetActionResponseDto
        {
            QuestionSetId = questionSet.Id,
            Status = questionSet.Status,
            PublishedAt = questionSet.PublishedAt,
            AbandonedSessionCount = abandoned
        };
    }

    /// <summary>SCRUM-438: tổng hợp mọi set PUBLISHED của HR.</summary>
    public async Task<IReadOnlyList<PublishedOverviewItemDto>> GetPublishedOverviewAsync(Guid ownerId)
    {
        var sets = (await _questionSetRepository.ListByOwnerAsync(ownerId))
            .Where(qs => qs.IsActive && qs.Status == QuestionSetStatus.Published)
            .OrderByDescending(qs => qs.PublishedAt ?? qs.UpdatedAt ?? qs.CreatedAt)
            .ToList();

        if (sets.Count == 0)
            return Array.Empty<PublishedOverviewItemDto>();

        var counts = await _questionSetRepository.GetQuestionCountsBySetIdsAsync(sets.Select(s => s.Id));
        var result = new List<PublishedOverviewItemDto>(sets.Count);

        foreach (var qs in sets)
        {
            var practitioners = await _practiceSessionRepository.ListPractitionersByQuestionSetAsync(qs.Id);
            // SCRUM-464: bộ Tuyển chỉ đếm official (+ in-progress) cho overview HR
            IReadOnlyList<QuestionSetPractitionerRow> scoped = qs.IsHiringAssessment
                ? practitioners
                    .Where(p =>
                        p.IsOfficialTest
                        || string.Equals(p.Status, PracticeSessionStatus.InProgress, StringComparison.OrdinalIgnoreCase))
                    .ToList()
                : practitioners;
            var attemptCount = scoped.Count;
            var completed = scoped.Where(p =>
                string.Equals(p.Status, PracticeSessionStatus.Completed, StringComparison.OrdinalIgnoreCase)).ToList();
            var inProgress = scoped.Count(p =>
                string.Equals(p.Status, PracticeSessionStatus.InProgress, StringComparison.OrdinalIgnoreCase));
            double? avgScore = null;
            var scores = completed.Where(p => p.OverallScore.HasValue).Select(p => p.OverallScore!.Value).ToList();
            if (scores.Count > 0)
                avgScore = Math.Round(scores.Average(), 1);

            var (avgRating, feedbackCount) = await _feedbackRepository.GetSummaryAsync(qs.Id);

            result.Add(new PublishedOverviewItemDto
            {
                QuestionSetId = qs.Id,
                Title = qs.Title,
                PublishedAt = qs.PublishedAt,
                QuestionCount = counts.TryGetValue(qs.Id, out var c) ? c : qs.Questions.Count(q => q.IsActive),
                TimeLimitMinutes = qs.TimeLimitMinutes,
                AttemptCount = attemptCount,
                CompletedCount = completed.Count,
                InProgressCount = inProgress,
                AverageScore = avgScore,
                AverageRating = avgRating.HasValue ? Math.Round(avgRating.Value, 2) : null,
                FeedbackCount = feedbackCount,
                IsHiringAssessment = qs.IsHiringAssessment
            });
        }

        return result;
    }

    /// <summary>SCRUM-391: xuất Excel — kiểm tra subscription CanExport.</summary>
    public async Task<QuestionExportFileDto> ExportExcelAsync(Guid questionSetId, Guid ownerId)
    {
        await _subscriptionGate.CheckExportAsync(ownerId);
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);
        var questions = questionSet.Questions.Where(q => q.IsActive).OrderBy(q => q.Order).ToList();
        if (questions.Count == 0)
            throw new BadRequestException("Bộ câu hỏi chưa có câu hỏi để xuất.");

        return GeneratedQuestionsExcelExporter.BuildFromQuestionSet(questionSet, questions);
    }

    /// <summary>SCRUM-391: soft-delete; unpublish trước nếu đang PUBLISHED.</summary>
    public async Task SoftDeleteAsync(Guid questionSetId, Guid ownerId)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        if (questionSet.Status == QuestionSetStatus.Published)
        {
            questionSet.Status = QuestionSetStatus.Draft;
            questionSet.PublishedAt = null;
        }

        await _questionSetRepository.SoftDeleteAsync(questionSet);
    }

    public async Task<QuestionSetQuestionResponseDto> UploadQuestionImageAsync(
        Guid questionSetId,
        Guid questionId,
        Guid ownerId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileLength)
    {
        await EnsureEditableQuestionSetAsync(questionSetId, ownerId);

        var question = await _questionSetRepository.GetQuestionByIdAsync(questionId)
            ?? throw new NotFoundException("Câu hỏi không tồn tại.");
        if (question.QuestionSetId != questionSetId)
            throw new NotFoundException("Câu hỏi không thuộc question set này.");

        if (fileLength <= 0)
            throw new BadRequestException("File ảnh trống.");
        if (fileLength > MaxQuestionImageBytes)
            throw new BadRequestException("Ảnh tối đa 5MB.");

        var ctNorm = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        if (!AllowedQuestionImageContentTypes.Contains(ctNorm)
            && !fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ chấp nhận jpg/jpeg/png/webp.");

        if (!string.IsNullOrWhiteSpace(question.AttachedImageBlobPath))
        {
            try { await _blobStorage.DeleteAsync(question.AttachedImageBlobPath); }
            catch { /* ignore */ }
        }

        var blobPath = BlobPathHelper.BuildQuestionSetImagePath(questionSetId, questionId, fileName);
        await _blobStorage.UploadAsync(fileStream, ctNorm, blobPath);
        question.AttachedImageBlobPath = blobPath;
        question.UpdatedAt = DateTime.UtcNow;
        await _questionSetRepository.UpdateQuestionAsync(question);
        return await MapQuestionAsync(question);
    }

    public async Task<QuestionSetQuestionResponseDto> DeleteQuestionImageAsync(
        Guid questionSetId, Guid questionId, Guid ownerId)
    {
        await EnsureEditableQuestionSetAsync(questionSetId, ownerId);

        var question = await _questionSetRepository.GetQuestionByIdAsync(questionId)
            ?? throw new NotFoundException("Câu hỏi không tồn tại.");
        if (question.QuestionSetId != questionSetId)
            throw new NotFoundException("Câu hỏi không thuộc question set này.");

        if (!string.IsNullOrWhiteSpace(question.AttachedImageBlobPath))
        {
            try { await _blobStorage.DeleteAsync(question.AttachedImageBlobPath); }
            catch { /* ignore */ }
            question.AttachedImageBlobPath = null;
            question.UpdatedAt = DateTime.UtcNow;
            await _questionSetRepository.UpdateQuestionAsync(question);
        }

        return await MapQuestionAsync(question);
    }

    private async Task<QuestionSet> EnsureOwnedQuestionSetAsync(Guid questionSetId, Guid ownerId)
    {
        var questionSet = await _questionSetRepository.GetByIdWithQuestionsAsync(questionSetId)
            ?? throw new NotFoundException("Question set không tồn tại.");

        if (!questionSet.IsActive)
            throw new NotFoundException("Question set không tồn tại.");

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
        string answerMethod,
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
        question.AnswerMethod = AnswerMethodNormalizer.Require(answerMethod);
        question.EvaluationCriteriaJson = SerializeEvaluationCriteria(evaluationCriteria);
        question.CitationsJson = JsonSerializer.Serialize(citations, JsonOptions);
    }

    /// <summary>SCRUM-418: Chuẩn hóa rubric trước khi lưu jsonb.</summary>
    private static string SerializeEvaluationCriteria(List<object> evaluationCriteria)
    {
        if (evaluationCriteria is not { Count: > 0 })
            return RubricNormalizer.SerializeForStorage(RubricNormalizer.NormalizeFromJson(null));

        var first = evaluationCriteria[0];
        if (first is JsonElement el && el.ValueKind == JsonValueKind.Object
            && el.TryGetProperty("criteria", out _))
        {
            return RubricNormalizer.SerializeForStorage(
                RubricNormalizer.NormalizeFromObjects(evaluationCriteria));
        }

        if (first is JsonElement el2 && el2.ValueKind == JsonValueKind.Object
            && (el2.TryGetProperty("label", out _) || el2.TryGetProperty("anchors", out _)))
        {
            var doc = RubricNormalizer.NormalizeFromObjects(evaluationCriteria);
            return RubricNormalizer.SerializeForStorage(doc);
        }

        var docFromLegacy = RubricNormalizer.NormalizeFromObjects(evaluationCriteria);
        return RubricNormalizer.SerializeForStorage(docFromLegacy);
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

    private async Task<QuestionSetQuestionResponseDto> MapQuestionAsync(QuestionSetQuestion q)
    {
        string? url = null;
        if (!string.IsNullOrWhiteSpace(q.AttachedImageBlobPath))
        {
            try
            {
                url = await _blobStorage.GenerateReadSasUrlAsync(q.AttachedImageBlobPath, TimeSpan.FromHours(2));
            }
            catch
            {
                url = null;
            }
        }

        return new QuestionSetQuestionResponseDto
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
            AttachedImageUrl = url,
            AnswerMethod = AnswerMethodNormalizer.Resolve(q.AnswerMethod),
            // Studio lưu RubricV1 object; legacy là array — không Deserialize<List> cứng (gây 500 khi mở History)
            EvaluationCriteria = ParseEvaluationCriteriaPayload(q.EvaluationCriteriaJson),
            Citations = ParseCitationsPayload(q.CitationsJson),
            IsActive = q.IsActive
        };
    }

    /// <summary>Nhận RubricV1 JSON object hoặc legacy criteria array — không throw.</summary>
    private static object ParseEvaluationCriteriaPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<object>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            // Clone raw JSON tree — tránh Deserialize<List> khi Studio lưu RubricV1 object
            return doc.RootElement.Clone();
        }
        catch
        {
            return new List<object>();
        }
    }

    private static List<object> ParseCitationsPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<object>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new List<object>();
            return JsonSerializer.Deserialize<List<object>>(json, JsonOptions) ?? new List<object>();
        }
        catch
        {
            return new List<object>();
        }
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
