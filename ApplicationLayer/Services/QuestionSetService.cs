using System.Text.Json;
using ApplicationLayer.DTOs.QuestionSet;
using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.Helpers;
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
        IRagService ragService)
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

        if (questionSet.OwnerId != ownerId)
            throw new ForbiddenException("Bạn không có quyền truy cập question set này.");

        object? planObj = null;
        if (!string.IsNullOrWhiteSpace(questionSet.PlanJson))
            planObj = JsonSerializer.Deserialize<object>(questionSet.PlanJson, JsonOptions);

        var (companyName, companyLogo) = await _hrCompanyInfoService.GetByHrUserIdAsync(ownerId);

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
            HrNote = questionSet.HrNote,
            TimeLimitMinutes = questionSet.TimeLimitMinutes,
            Plan = planObj,
            GeneratedAt = questionSet.GeneratedAt,
            SavedAt = questionSet.CreatedAt,
            PublishedAt = questionSet.PublishedAt,
            Questions = (await Task.WhenAll(
                questionSet.Questions
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
            EvaluationCriteriaJson = JsonSerializer.Serialize(dto.EvaluationCriteria, JsonOptions),
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

    public Task<UpdateQuestionSetJobDescriptionResponseDto> SetJobDescriptionFromTextAsync(
        Guid questionSetId, Guid ownerId, string jobDescription)
    {
        var jd = JobDescriptionValidator.Validate(jobDescription);
        return PersistJobDescriptionAsync(questionSetId, ownerId, jd);
    }

    public async Task<UpdateQuestionSetJobDescriptionResponseDto> SetJobDescriptionFromFileAsync(
        Guid questionSetId, Guid ownerId, Stream file, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (ext is not ".pdf" and not ".docx" and not ".txt")
            throw new BadRequestException("Chỉ hỗ trợ file PDF, DOCX hoặc TXT.");

        var parsed = await _ragService.ParseJdAsync(file, string.IsNullOrWhiteSpace(fileName) ? "jd.txt" : fileName, ct);
        if (!parsed.Success || string.IsNullOrWhiteSpace(parsed.JobDescription))
            throw new BadRequestException(parsed.Error ?? "Không đọc được Job Description từ file.");

        var jd = JobDescriptionValidator.Validate(parsed.JobDescription, fileName);
        return await PersistJobDescriptionAsync(questionSetId, ownerId, jd);
    }

    private async Task<UpdateQuestionSetJobDescriptionResponseDto> PersistJobDescriptionAsync(
        Guid questionSetId, Guid ownerId, string jd)
    {
        var questionSet = await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);
        questionSet.JobDescription = jd;
        questionSet.UpdatedAt = DateTime.UtcNow;
        await _questionSetRepository.UpdateAsync(questionSet);
        return new UpdateQuestionSetJobDescriptionResponseDto
        {
            QuestionSetId = questionSet.Id,
            HasJobDescription = true,
            CharacterCount = jd.Length
        };
    }

    public async Task<IReadOnlyList<QuestionSetPractitionerDto>> GetPractitionersAsync(Guid questionSetId, Guid ownerId)
    {
        await EnsureOwnedQuestionSetAsync(questionSetId, ownerId);

        var rows = await _practiceSessionRepository.ListPractitionersByQuestionSetAsync(questionSetId);
        return rows.Select(r => new QuestionSetPractitionerDto
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
            CompletedAt = r.CompletedAt
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
            EvaluationCriteria = JsonSerializer.Deserialize<List<object>>(q.EvaluationCriteriaJson, JsonOptions) ?? new(),
            Citations = JsonSerializer.Deserialize<List<object>>(q.CitationsJson, JsonOptions) ?? new()
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
