using System.Text.Json;
using DomainLayer.Common;
using ApplicationLayer.DTOs.KnowledgeBase;
using ApplicationLayer.DTOs.QuestionGeneration;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Settings;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services;

public class QuestionGenerationJobService : IQuestionGenerationJobService
{
    private const int MaxHrNoteLength = 2000;
    private const int JobDescriptionPreviewLength = 120;

    private readonly IQuestionGenerationJobRepository _repository;
    private readonly IJobScheduler _jobScheduler;
    private readonly IRagService _ragService;
    private readonly KnowledgeBaseSettings _kbSettings;
    private readonly IQuestionSetRepository _questionSetRepository;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public QuestionGenerationJobService(
        IQuestionGenerationJobRepository repository,
        IJobScheduler jobScheduler,
        IRagService ragService,
        IQuestionSetRepository questionSetRepository,
        IOptions<KnowledgeBaseSettings> kbSettings)
    {
        _repository = repository;
        _jobScheduler = jobScheduler;
        _ragService = ragService;
        _kbSettings = kbSettings.Value;
        _questionSetRepository = questionSetRepository;
    }

    public Task<CreatePlanJobResponseDto> CreatePlanJobAsync(
        Guid ownerId, CreatePlanJobRequestDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.JobDescription))
        {
            throw StructuredHttpException.FromBe(
                "Thiếu mô tả công việc",
                ErrorStage.MissingJdInput,
                ["Cần nhập jobDescription hoặc upload file JD."]);
        }

        return CreatePlanJobInternalAsync(
            ownerId,
            dto.JobDescription,
            dto.HrNote,
            null,
            null,
            0,
            dto.NumberOfQuestions,
            dto.Difficulty,
            dto.QuestionTypes,
            dto.Skills,
            ct);
    }

    public Task<CreatePlanJobResponseDto> CreatePlanJobFromUploadAsync(
        Guid ownerId,
        string? jobDescription,
        string? hrNote,
        Stream? fileStream,
        string? fileName,
        long fileSize,
        int numberOfQuestions,
        string difficulty,
        List<string> questionTypes,
        List<string> skills,
        CancellationToken ct = default)
    {
        var hasText = !string.IsNullOrWhiteSpace(jobDescription);
        var hasFile = fileStream is not null && fileSize > 0;

        if (!hasText && !hasFile)
        {
            throw StructuredHttpException.FromBe(
                "Thiếu mô tả công việc",
                ErrorStage.MissingJdInput,
                ["Cần nhập jobDescription hoặc upload file JD."]);
        }

        if (hasFile)
            ValidateUploadFile(fileName!, fileSize);

        return CreatePlanJobInternalAsync(
            ownerId,
            jobDescription,
            hrNote,
            fileStream,
            fileName,
            fileSize,
            numberOfQuestions,
            difficulty,
            questionTypes,
            skills,
            ct);
    }

    private async Task<CreatePlanJobResponseDto> CreatePlanJobInternalAsync(
        Guid ownerId,
        string? jobDescription,
        string? hrNote,
        Stream? fileStream,
        string? fileName,
        long fileSize,
        int numberOfQuestions,
        string difficulty,
        List<string> questionTypes,
        List<string> skills,
        CancellationToken ct)
    {
        ValidateBusinessFields(hrNote);
        QuestionGenerationInputValidator.ValidateNumberOfQuestions(numberOfQuestions);
        var normalizedDifficulty = QuestionGenerationInputValidator.ValidateAndNormalizeDifficulty(difficulty);
        var normalizedTypes = QuestionGenerationInputValidator.ValidateAndNormalizeQuestionTypes(questionTypes);

        var resolved = await ResolveJobDescriptionAsync(
            jobDescription, fileStream, fileName, fileSize, ct);

        var job = new QuestionGenerationJob
        {
            OwnerId = ownerId,
            JobDescription = resolved.Text,
            HrNote = string.IsNullOrWhiteSpace(hrNote) ? null : hrNote.Trim(),
            JdInputType = resolved.InputType,
            JdFileName = resolved.FileName,
            NumberOfQuestions = numberOfQuestions,
            Difficulty = normalizedDifficulty,
            QuestionTypesJson = JsonSerializer.Serialize(normalizedTypes, JsonOptions),
            SkillsJson = JsonSerializer.Serialize(skills, JsonOptions),
            Status = QuestionGenerationJobStatus.PlanQueued
        };

        await _repository.AddAsync(job);
        _jobScheduler.EnqueueGeneratePlan(job.Id);

        return new CreatePlanJobResponseDto
        {
            JobId = job.Id,
            Status = job.Status,
            JdInputType = job.JdInputType,
            Warnings = resolved.Warnings
        };
    }

    public async Task<JobStatusResponseDto> UpdateJobInputAsync(
        Guid jobId, Guid ownerId, CreatePlanJobRequestDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.JobDescription))
        {
            throw StructuredHttpException.FromBe(
                "Thiếu mô tả công việc",
                ErrorStage.MissingJdInput,
                ["Cần nhập jobDescription hoặc upload file JD."]);
        }

        var job = await GetOwnedJob(jobId, ownerId);
        await EnsureJobInputEditableAsync(job);

        await ApplyJobInputAsync(
            job,
            dto.JobDescription,
            dto.HrNote,
            null,
            null,
            0,
            dto.NumberOfQuestions,
            dto.Difficulty,
            dto.QuestionTypes,
            dto.Skills,
            ct);

        return await MapJobStatusAsync(job, job.Plan);
    }

    public Task<JobStatusResponseDto> UpdateJobInputFromUploadAsync(
        Guid jobId,
        Guid ownerId,
        string? jobDescription,
        string? hrNote,
        Stream? fileStream,
        string? fileName,
        long fileSize,
        int numberOfQuestions,
        string difficulty,
        List<string> questionTypes,
        List<string> skills,
        CancellationToken ct = default)
    {
        var hasText = !string.IsNullOrWhiteSpace(jobDescription);
        var hasFile = fileStream is not null && fileSize > 0;

        if (!hasText && !hasFile)
        {
            throw StructuredHttpException.FromBe(
                "Thiếu mô tả công việc",
                ErrorStage.MissingJdInput,
                ["Cần nhập jobDescription hoặc upload file JD."]);
        }

        if (hasFile)
            ValidateUploadFile(fileName!, fileSize);

        return UpdateJobInputFromUploadInternalAsync(
            jobId, ownerId, jobDescription, hrNote, fileStream, fileName, fileSize,
            numberOfQuestions, difficulty, questionTypes, skills, ct);
    }

    private async Task<JobStatusResponseDto> UpdateJobInputFromUploadInternalAsync(
        Guid jobId,
        Guid ownerId,
        string? jobDescription,
        string? hrNote,
        Stream? fileStream,
        string? fileName,
        long fileSize,
        int numberOfQuestions,
        string difficulty,
        List<string> questionTypes,
        List<string> skills,
        CancellationToken ct)
    {
        var job = await GetOwnedJob(jobId, ownerId);
        await EnsureJobInputEditableAsync(job);

        await ApplyJobInputAsync(
            job,
            jobDescription,
            hrNote,
            fileStream,
            fileName,
            fileSize,
            numberOfQuestions,
            difficulty,
            questionTypes,
            skills,
            ct);

        return await MapJobStatusAsync(job, job.Plan);
    }

    private async Task ApplyJobInputAsync(
        QuestionGenerationJob job,
        string? jobDescription,
        string? hrNote,
        Stream? fileStream,
        string? fileName,
        long fileSize,
        int numberOfQuestions,
        string difficulty,
        List<string> questionTypes,
        List<string> skills,
        CancellationToken ct)
    {
        ValidateBusinessFields(hrNote);
        QuestionGenerationInputValidator.ValidateNumberOfQuestions(numberOfQuestions);
        var normalizedDifficulty = QuestionGenerationInputValidator.ValidateAndNormalizeDifficulty(difficulty);
        var normalizedTypes = QuestionGenerationInputValidator.ValidateAndNormalizeQuestionTypes(questionTypes);

        var resolved = await ResolveJobDescriptionAsync(
            jobDescription, fileStream, fileName, fileSize, ct);

        job.JobDescription = resolved.Text;
        job.HrNote = string.IsNullOrWhiteSpace(hrNote) ? null : hrNote.Trim();
        job.JdInputType = resolved.InputType;
        job.JdFileName = resolved.FileName;
        job.NumberOfQuestions = numberOfQuestions;
        job.Difficulty = normalizedDifficulty;
        job.QuestionTypesJson = JsonSerializer.Serialize(normalizedTypes, JsonOptions);
        job.SkillsJson = JsonSerializer.Serialize(skills, JsonOptions);
        job.Status = QuestionGenerationJobStatus.PlanQueued;
        job.ErrorMessage = null;

        if (job.Plan is not null && !job.Plan.IsApproved)
        {
            job.Plan.IsApproved = false;
            job.Plan.ApprovedAt = null;
            await _repository.UpdatePlanAsync(job.Plan);
        }

        await _repository.UpdateAsync(job);
        _jobScheduler.EnqueueGeneratePlan(job.Id);
    }

    private Task EnsureJobInputEditableAsync(QuestionGenerationJob job)
    {
        if (job.Status != QuestionGenerationJobStatus.Failed)
            throw new BadRequestException("Chỉ được sửa input khi status FAILED.");

        if (job.Plan?.IsApproved == true)
            throw new BadRequestException("Job đã approve plan — không thể sửa input, dùng retry-questions.");

        var (_, error) = MapJobError(job.ErrorMessage);
        if (error?.Stage == ErrorStage.RagUnavailable)
        {
            throw new BadRequestException(
                "Lỗi dịch vụ AI tạm thời — dùng POST retry-plan hoặc retry-questions, không sửa input.");
        }

        if (error?.Stage is not null && !JobUiStateMapper.IsInputEditableStage(error.Stage))
        {
            throw new BadRequestException("Job không thể sửa input với loại lỗi hiện tại.");
        }

        return Task.CompletedTask;
    }

    private async Task<(string Text, string InputType, string? FileName, List<string> Warnings)> ResolveJobDescriptionAsync(
        string? jobDescription,
        Stream? fileStream,
        string? fileName,
        long fileSize,
        CancellationToken ct)
    {
        if (fileStream is not null && fileSize > 0)
        {
            var parseResult = await _ragService.ParseJdAsync(fileStream, fileName ?? "jd.txt", ct);
            var validatedText = JobDescriptionValidator.Validate(
                parseResult.JobDescription,
                fileName ?? "JD");

            return (
                validatedText,
                JdInputType.File,
                fileName,
                parseResult.Warnings.Distinct().ToList());
        }

        var validatedTextFromInput = JobDescriptionValidator.Validate(jobDescription);

        return (
            validatedTextFromInput,
            JdInputType.Text,
            null,
            []);
    }

    private void ValidateUploadFile(string fileName, long fileSize)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!_kbSettings.AllowedExtensions.Contains(ext))
        {
            throw StructuredHttpException.FromBe(
                "File không hợp lệ",
                ErrorStage.InvalidFileType,
                [$"Chỉ chấp nhận file: {string.Join(", ", _kbSettings.AllowedExtensions)}."]);
        }

        var maxBytes = _kbSettings.MaxFileSizeMb * 1024L * 1024L;
        if (fileSize > maxBytes)
        {
            throw StructuredHttpException.FromBe(
                "File quá lớn",
                ErrorStage.FileTooLarge,
                [$"File vượt quá {_kbSettings.MaxFileSizeMb}MB."]);
        }
    }

    private static void ValidateBusinessFields(string? hrNote)
    {
        if (hrNote?.Length > MaxHrNoteLength)
            throw new BadRequestException($"hrNote tối đa {MaxHrNoteLength} ký tự.");
    }


    public async Task<PagedResultDto<QuestionGenerationJobListItemDto>> ListJobsAsync(
        Guid ownerId, QuestionGenerationListQueryDto query)
    {
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, 100);
        var paged = await _repository.GetPagedByOwnerAsync(ownerId, query);
        var draftJobIds = await _questionSetRepository.GetSourceJobIdsWithDraftAsync(paged.Items.Select(j => j.Id));
        var items = paged.Items.Select(job => new QuestionGenerationJobListItemDto
        {
            JobId = job.Id,
            Status = job.Status,
            JobDescriptionPreview = BuildJobDescriptionPreview(job.JobDescription),
            NumberOfQuestions = job.NumberOfQuestions,
            JdInputType = job.JdInputType,
            JdFileName = job.JdFileName,
            CreatedAt = job.CreatedAt,
            CompletedAt = job.CompletedAt,
            QuestionCount = job.Questions.Count,
            HasDraft = draftJobIds.Contains(job.Id)
        }).ToList();
        return new PagedResultDto<QuestionGenerationJobListItemDto>
        {
            Items = items,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task<PagedResultDto<QuestionGenerationPlanListItemDto>> ListPlansAsync(
        Guid ownerId, QuestionGenerationListQueryDto query)
    {
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, 100);
        var paged = await _repository.GetPagedPlansByOwnerAsync(ownerId, query);

        var items = paged.Items.Select(job =>
        {
            var plan = job.Plan!;
            var summary = PlanJsonSummaryReader.Read(job, plan);
            return new QuestionGenerationPlanListItemDto
            {
                JobId = job.Id,
                JobTitle = summary.JobTitle,
                Role = summary.Role,
                Level = summary.Level,
                Question = summary.Question,
                CreatedAt = plan.CreatedAt,
                Status = job.Status,
                IsPlanApproved = plan.IsApproved
            };
        }).ToList();

        return new PagedResultDto<QuestionGenerationPlanListItemDto>
        {
            Items = items,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task<QuestionGenerationPlanDetailResponseDto> GetPlanAsync(Guid jobId, Guid ownerId)
    {
        var job = await GetOwnedJob(jobId, ownerId);

        if (job.Plan is null)
            throw new NotFoundException("Plan chưa được tạo.");

        var plan = job.Plan;
        object? planObj = null;
        if (!string.IsNullOrWhiteSpace(plan.PlanJson))
            planObj = JsonSerializer.Deserialize<object>(plan.PlanJson, JsonOptions);

        var hasDraft = await _questionSetRepository.ExistsBySourceJobIdAsync(job.Id);
        var ui = JobUiStateMapper.Map(job.Status, hasDraft, plan.IsApproved, null);

        return new QuestionGenerationPlanDetailResponseDto
        {
            PlanId = plan.Id,
            JobId = job.Id,
            Status = job.Status,
            IsPlanApproved = plan.IsApproved,
            ApprovedAt = plan.ApprovedAt,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt,
            Input = MapPlanHrInput(job),
            Plan = planObj,
            Ui = ui
        };
    }

    public async Task DeletePlanAsync(Guid jobId, Guid ownerId)
    {
        var job = await GetOwnedJob(jobId, ownerId);

        if (job.Plan is null)
            throw new NotFoundException("Plan chưa được tạo.");

        if (job.Status is QuestionGenerationJobStatus.PlanProcessing
            or QuestionGenerationJobStatus.QuestionProcessing)
        {
            throw new ConflictException("Không thể xóa khi job đang xử lý.");
        }

        if (await _questionSetRepository.ExistsBySourceJobIdAsync(jobId))
            throw new ConflictException("Không thể xóa vì session đã được lưu draft.");

        await _repository.DeleteJobAsync(job);
    }

    public async Task<QuestionExportFileDto> ExportPlanQuestionsExcelAsync(Guid jobId, Guid ownerId)
    {
        var job = await GetOwnedJob(jobId, ownerId);

        if (job.Plan is null)
            throw new NotFoundException("Plan chưa được tạo.");

        if (job.Status != QuestionGenerationJobStatus.Completed)
            throw new BadRequestException("Chỉ tải được khi session ở trạng thái COMPLETED.");

        if (job.Questions.Count == 0)
            throw new BadRequestException("Session chưa có câu hỏi để xuất.");

        return GeneratedQuestionsExcelExporter.Build(job);
    }

    public async Task<JobStatusResponseDto> GetJobAsync(Guid jobId, Guid ownerId)
    {
        var job = await GetOwnedJob(jobId, ownerId);
        return await MapJobStatusAsync(job, job.Plan);
    }

    public async Task<object> UpdatePlanAsync(Guid jobId, Guid ownerId, UpdatePlanRequestDto dto)
    {
        var job = await GetOwnedJob(jobId, ownerId);

        if (job.Status != QuestionGenerationJobStatus.WaitingHrApproval)
            throw new BadRequestException("Chỉ được sửa plan khi status là WAITING_HR_APPROVAL.");

        if (dto.TotalQuestions <= 0)
            throw new BadRequestException("totalQuestions phải lớn hơn 0.");

        if (job.Plan is null)
            throw new BadRequestException("Plan chưa tồn tại.");

        var planObject = new
        {
            roleTitle = dto.RoleTitle,
            summary = dto.Summary,
            difficulty = dto.Difficulty,
            totalQuestions = dto.TotalQuestions,
            skills = dto.Skills,
            questionTypeDistribution = dto.QuestionTypeDistribution,
            difficultyDistribution = dto.DifficultyDistribution,
            coverage = dto.Coverage,
            recommendedQuestionOutline = dto.RecommendedQuestionOutline,
            notes = dto.Notes
        };

        job.Plan.PlanJson = JsonSerializer.Serialize(planObject, JsonOptions);
        await _repository.UpdatePlanAsync(job.Plan);

        return planObject;
    }

    public async Task<JobStatusResponseDto> ApprovePlanAsync(Guid jobId, Guid ownerId)
    {
        var job = await GetOwnedJob(jobId, ownerId);

        if (job.Plan is null)
            throw new BadRequestException("Job chưa có plan.");

        if (job.Status != QuestionGenerationJobStatus.WaitingHrApproval)
            throw new BadRequestException("Job không ở trạng thái WAITING_HR_APPROVAL.");

        job.Plan.IsApproved = true;
        job.Plan.ApprovedAt = DateTime.UtcNow;
        job.Status = QuestionGenerationJobStatus.QuestionQueued;
        job.ErrorMessage = null;

        await _repository.UpdatePlanAsync(job.Plan);
        await _repository.UpdateAsync(job);
        _jobScheduler.EnqueueGenerateQuestionsFromPlan(jobId);

        return await MapJobStatusAsync(job, job.Plan);
    }

    public async Task<JobQuestionsResponseDto> GetQuestionsAsync(Guid jobId, Guid ownerId)
    {
        var job = await GetOwnedJob(jobId, ownerId);

        var (errorMessage, error) = MapJobError(job.ErrorMessage);

        return new JobQuestionsResponseDto
        {
            JobId = job.Id,
            Status = job.Status,
            Questions = job.Questions
                .OrderBy(q => q.Order)
                .Select(MapGeneratedQuestion)
                .ToList(),
            ErrorMessage = errorMessage,
            Error = error
        };
    }

    public async Task<JobStatusResponseDto> RetryPlanAsync(Guid jobId, Guid ownerId)
    {
        var job = await GetOwnedJob(jobId, ownerId);

        if (job.Status != QuestionGenerationJobStatus.Failed)
            throw new BadRequestException("Chỉ retry plan khi status FAILED.");

        if (job.Plan?.IsApproved == true)
            throw new BadRequestException("Job đã approve plan — dùng retry-questions.");

        job.Status = QuestionGenerationJobStatus.PlanQueued;
        job.ErrorMessage = null;
        await _repository.UpdateAsync(job);
        _jobScheduler.EnqueueGeneratePlan(jobId);

        return await MapJobStatusAsync(job, job.Plan);
    }

    public async Task<JobStatusResponseDto> RetryQuestionsAsync(Guid jobId, Guid ownerId)
    {
        var job = await GetOwnedJob(jobId, ownerId);

        if (job.Status != QuestionGenerationJobStatus.Failed)
            throw new BadRequestException("Chỉ retry questions khi status FAILED.");

        if (job.Plan is null || !job.Plan.IsApproved)
            throw new BadRequestException("Plan chưa được approve.");

        job.Status = QuestionGenerationJobStatus.QuestionQueued;
        job.ErrorMessage = null;
        await _repository.UpdateAsync(job);
        _jobScheduler.EnqueueGenerateQuestionsFromPlan(jobId);

        return await MapJobStatusAsync(job, job.Plan);
    }


    public async Task<GeneratedQuestionResponseDto> UpdateQuestionAsync(
        Guid jobId, Guid questionId, Guid ownerId, UpdateQuestionRequestDto dto)
    {
        var job = await GetOwnedJob(jobId, ownerId);
        await EnsureJobAllowsQuestionEditAsync(job);

        var question = await _repository.GetQuestionByIdAsync(questionId)
            ?? throw new NotFoundException("Câu hỏi không tồn tại.");

        if (question.JobId != jobId)
            throw new NotFoundException("Câu hỏi không thuộc session này.");

        ApplyQuestionFields(question, dto.Question, dto.QuestionType, dto.Difficulty,
            dto.Skill, dto.FocusArea, dto.Rationale, dto.SampleAnswer,
            dto.EvaluationCriteria, dto.Citations);

        await _repository.UpdateQuestionAsync(question);
        return MapGeneratedQuestion(question);
    }

    public async Task<GeneratedQuestionResponseDto> AddQuestionAsync(
        Guid jobId, Guid ownerId, CreateQuestionRequestDto dto)
    {
        var job = await GetOwnedJob(jobId, ownerId);
        await EnsureJobAllowsQuestionEditAsync(job);

        ValidateQuestionInput(dto.Question, dto.QuestionType, dto.Difficulty);

        var order = dto.Order ?? (await _repository.GetMaxOrderByJobIdAsync(jobId) + 1);
        if (order <= 0)
            throw new BadRequestException("order phải lớn hơn 0.");

        var question = new GeneratedQuestion
        {
            JobId = jobId,
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

        await _repository.AddQuestionsAsync(new[] { question });
        return MapGeneratedQuestion(question);
    }

    public async Task DeleteQuestionAsync(Guid jobId, Guid questionId, Guid ownerId)
    {
        var job = await GetOwnedJob(jobId, ownerId);
        await EnsureJobAllowsQuestionEditAsync(job);

        var count = await _repository.GetQuestionCountByJobIdAsync(jobId);
        if (count <= 1)
            throw new BadRequestException("Không thể xóa câu hỏi cuối cùng.");

        var question = await _repository.GetQuestionByIdAsync(questionId)
            ?? throw new NotFoundException("Câu hỏi không tồn tại.");

        if (question.JobId != jobId)
            throw new NotFoundException("Câu hỏi không thuộc session này.");

        await _repository.DeleteQuestionAsync(question);
        await NormalizeQuestionOrdersAsync(jobId);
    }

    public async Task<IReadOnlyList<GeneratedQuestionResponseDto>> ReorderQuestionsAsync(
        Guid jobId, Guid ownerId, ReorderQuestionsRequestDto dto)
    {
        var job = await GetOwnedJob(jobId, ownerId);
        await EnsureJobAllowsQuestionEditAsync(job);

        if (dto.Items is null || dto.Items.Count == 0)
            throw new BadRequestException("items không được rỗng.");

        var existing = await _repository.GetQuestionsByJobIdAsync(jobId);
        if (dto.Items.Count != existing.Count)
            throw new BadRequestException("Danh sách reorder phải chứa đủ tất cả câu hỏi.");

        var existingIds = existing.Select(q => q.Id).ToHashSet();
        foreach (var item in dto.Items)
        {
            if (!existingIds.Contains(item.QuestionId))
                throw new BadRequestException("questionId không thuộc session này.");
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
            await _repository.UpdateQuestionAsync(question);
        }

        var sorted = (await _repository.GetQuestionsByJobIdAsync(jobId))
            .OrderBy(q => q.Order)
            .Select(MapGeneratedQuestion)
            .ToList();

        return sorted;
    }

    private async Task EnsureJobAllowsQuestionEditAsync(QuestionGenerationJob job)
    {
        if (job.Status != QuestionGenerationJobStatus.Completed)
            throw new BadRequestException("Chỉ được sửa câu hỏi khi session ở trạng thái COMPLETED.");

        if (await _questionSetRepository.ExistsBySourceJobIdAsync(job.Id))
            throw new ConflictException("Session đã lưu draft, không thể sửa câu hỏi thêm.");
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
        GeneratedQuestion question,
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

    private async Task NormalizeQuestionOrdersAsync(Guid jobId)
    {
        var questions = await _repository.GetQuestionsByJobIdAsync(jobId);
        var ordered = questions.OrderBy(q => q.Order).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var expected = i + 1;
            if (ordered[i].Order == expected)
                continue;
            ordered[i].Order = expected;
            await _repository.UpdateQuestionAsync(ordered[i]);
        }
    }
    private async Task<QuestionGenerationJob> GetOwnedJob(Guid jobId, Guid ownerId)
    {
        var job = await _repository.GetByIdWithPlanAndQuestionsAsync(jobId)
            ?? throw new NotFoundException("Job không tồn tại.");

        if (job.OwnerId != ownerId)
            throw new ForbiddenException("Bạn không có quyền truy cập job này.");

        return job;
    }

    private static PlanHrInputDto MapPlanHrInput(QuestionGenerationJob job)
    {
        var questionTypes = JsonSerializer.Deserialize<List<string>>(job.QuestionTypesJson, JsonOptions) ?? new();
        var skills = JsonSerializer.Deserialize<List<string>>(job.SkillsJson, JsonOptions) ?? new();
        return new PlanHrInputDto
        {
            JobDescription = job.JobDescription,
            HrNote = job.HrNote,
            NumberOfQuestions = job.NumberOfQuestions,
            Difficulty = job.Difficulty,
            QuestionTypes = questionTypes,
            Skills = skills,
            JdInputType = job.JdInputType,
            JdFileName = job.JdFileName
        };
    }

    private async Task<JobStatusResponseDto> MapJobStatusAsync(QuestionGenerationJob job, QuestionGenerationPlan? plan)
    {
        object? planObj = null;
        if (plan is not null)
            planObj = JsonSerializer.Deserialize<object>(plan.PlanJson, JsonOptions);

        var questionTypes = JsonSerializer.Deserialize<List<string>>(job.QuestionTypesJson, JsonOptions) ?? new();
        var skills = JsonSerializer.Deserialize<List<string>>(job.SkillsJson, JsonOptions) ?? new();
        var hasDraft = await _questionSetRepository.ExistsBySourceJobIdAsync(job.Id);
        var planApproved = plan?.IsApproved == true;

        var (errorMessage, error) = MapJobError(job.ErrorMessage);
        var failure = job.Status == QuestionGenerationJobStatus.Failed
            ? JobUiStateMapper.MapFailure(error)
            : null;
        var ui = JobUiStateMapper.Map(job.Status, hasDraft, planApproved, error);

        var input = new JobInputSnapshotDto
        {
            JobDescription = job.JobDescription,
            JobDescriptionPreview = BuildJobDescriptionPreview(job.JobDescription),
            HrNote = job.HrNote,
            NumberOfQuestions = job.NumberOfQuestions,
            Difficulty = job.Difficulty,
            QuestionTypes = questionTypes,
            Skills = skills,
            JdInputType = job.JdInputType,
            JdFileName = job.JdFileName
        };

        var meta = new JobMetaDto
        {
            CreatedAt = job.CreatedAt,
            CompletedAt = job.CompletedAt,
            QuestionCount = job.Questions.Count,
            HasDraft = hasDraft,
            PlanApproved = planApproved
        };

        return new JobStatusResponseDto
        {
            JobId = job.Id,
            Status = job.Status,
            Phase = ui.Phase,
            Input = input,
            Meta = meta,
            Ui = ui,
            Failure = failure,
            JobDescription = job.JobDescription,
            HrNote = job.HrNote,
            JdInputType = job.JdInputType,
            JdFileName = job.JdFileName,
            NumberOfQuestions = job.NumberOfQuestions,
            Difficulty = job.Difficulty,
            QuestionTypes = questionTypes,
            Skills = skills,
            Plan = planObj,
            ErrorMessage = errorMessage,
            Error = error,
            Warnings = []
        };
    }


    private static string BuildJobDescriptionPreview(string jobDescription)
    {
        var trimmed = jobDescription.Trim();
        if (trimmed.Length <= JobDescriptionPreviewLength)
            return trimmed;
        return trimmed.Substring(0, JobDescriptionPreviewLength) + "...";
    }

    private GeneratedQuestionResponseDto MapGeneratedQuestion(GeneratedQuestion q)
    {
        return new GeneratedQuestionResponseDto
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
    }

    private static (string? ErrorMessage, StructuredErrorResponseDto? Error) MapJobError(string? rawErrorMessage)
    {
        var structuredError = JobErrorSerializer.TryDeserialize(rawErrorMessage);
        if (structuredError is null)
            return (rawErrorMessage, null);

        return (
            structuredError.Detail ?? structuredError.Error,
            new StructuredErrorResponseDto
            {
                Error = structuredError.Error,
                Detail = structuredError.Detail,
                Stage = structuredError.Stage,
                Source = structuredError.Source,
                Errors = structuredError.Errors
            });
    }
}
