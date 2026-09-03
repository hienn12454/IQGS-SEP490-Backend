using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services;
using ApplicationLayer.Studio.Contracts;
using ApplicationLayer.Studio.Helpers;
using ApplicationLayer.Studio.Interfaces;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using InfrastructureLayer.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace InfrastructureLayer.Services.Studio;

public sealed class InterviewProjectService(
    AppDbContext dbContext,
    IQuestionSetService questionSetService) : IInterviewProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<StudioProjectDto> CreateAsync(Guid userId, CreateStudioProjectRequest request, CancellationToken ct)
    {
        var entity = new InterviewProject
        {
            OwnerId = userId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Status = InterviewProjectStatus.Draft
        };
        dbContext.InterviewProjects.Add(entity);
        await dbContext.SaveChangesAsync(ct);
        return new StudioProjectDto(entity.Id, entity.Name, entity.Description, entity.Status);
    }

    public async Task<IReadOnlyList<StudioProjectDto>> ListByOwnerAsync(Guid userId, CancellationToken ct)
    {
        return await dbContext.InterviewProjects.Where(x => x.OwnerId == userId && x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new StudioProjectDto(x.Id, x.Name, x.Description, x.Status))
            .ToListAsync(ct);
    }

    public async Task<StudioProjectDetailDto> GetAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var project = await EnsureProjectAccessAsync(projectId, userId, false, ct);
        return await ToDetailAsync(project, ct);
    }

    public async Task<StudioProjectDetailDto> UpdateAsync(Guid projectId, Guid userId, UpdateStudioProjectRequest request, CancellationToken ct)
    {
        var project = await EnsureProjectAccessAsync(projectId, userId, true, ct);
        project.Name = request.Name.Trim();
        project.Description = request.Description;
        project.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return await ToDetailAsync(project, ct);
    }

    /// <summary>
    /// Soft-delete thôi — IsActive=false. Không Remove entity vì FK Restrict
    /// (question_sets.SourceProjectId + children Studio) sẽ chặn hard-delete.
    /// </summary>
    public async Task DeleteAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var project = await EnsureProjectAccessAsync(projectId, userId, true, ct);
        project.IsActive = false;
        project.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<InterviewProject> EnsureProjectAccessAsync(Guid projectId, Guid userId, bool requireEdit, CancellationToken ct)
    {
        var project = await dbContext.InterviewProjects.FirstOrDefaultAsync(x => x.Id == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("PROJECT_NOT_FOUND", StatusCodes.Status404NotFound, "Không tìm thấy project.");
        if (project.OwnerId != userId)
            throw new StudioBusinessException("PROJECT_ACCESS_DENIED", StatusCodes.Status403Forbidden, "Bạn không có quyền truy cập project.");
        _ = requireEdit;
        return project;
    }

    public async Task<StudioSaveQuestionSetResponseDto> SaveQuestionSetAsync(Guid projectId, Guid userId, CancellationToken ct)
        => await SaveQuestionSetCoreAsync(projectId, userId, interviewQuestionIds: null, ct);

    /// <summary>SCRUM-439: snapshot chỉ các interview question được chọn (null = tất cả).</summary>
    private async Task<StudioSaveQuestionSetResponseDto> SaveQuestionSetCoreAsync(
        Guid projectId,
        Guid userId,
        IReadOnlyList<Guid>? interviewQuestionIds,
        CancellationToken ct)
    {
        var project = await EnsureProjectAccessAsync(projectId, userId, true, ct);

        var interviewQuestionsQuery = dbContext.InterviewQuestions
            .Where(q => q.ProjectId == projectId && q.IsActive);
        if (interviewQuestionIds is { Count: > 0 })
        {
            var idSet = interviewQuestionIds.Where(id => id != Guid.Empty).Distinct().ToHashSet();
            interviewQuestionsQuery = interviewQuestionsQuery.Where(q => idSet.Contains(q.Id));
        }

        var interviewQuestions = await interviewQuestionsQuery
            .OrderBy(q => q.OrderIndex)
            .ToListAsync(ct);

        if (interviewQuestions.Count == 0)
            throw new StudioBusinessException("NO_QUESTIONS", StatusCodes.Status400BadRequest, "Chưa có câu hỏi để lưu. Hãy generate trước.");

        var plan = await dbContext.InterviewPlans
            .Where(p => p.ProjectId == projectId && p.IsActive)
            .OrderByDescending(p => p.Revision)
            .FirstOrDefaultAsync(ct);

        var jd = await dbContext.StudioJobDescriptions
            .FirstOrDefaultAsync(j => j.ProjectId == projectId && j.IsActive, ct);

        var latestRun = await dbContext.QuestionGenerationRuns
            .Where(r => r.ProjectId == projectId && r.IsActive)
            .OrderByDescending(r => r.CompletedAt ?? r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var existing = await dbContext.QuestionSets
            .Include(qs => qs.Questions)
            .FirstOrDefaultAsync(qs => qs.SourceProjectId == projectId && qs.IsActive, ct);

        if (existing is not null && existing.Status == DomainLayer.Constants.QuestionSetStatus.Published)
            throw new StudioBusinessException(
                "SET_PUBLISHED",
                StatusCodes.Status409Conflict,
                "Bộ câu hỏi đang PUBLISHED — unpublish trước khi Save lại.");

        if (existing is not null && existing.Questions.Count > 0)
        {
            var qIds = existing.Questions.Select(q => q.Id).ToList();
            var hasAnswers = await dbContext.CandidateAnswers
                .AnyAsync(a => qIds.Contains(a.QuestionSetQuestionId), ct);
            if (hasAnswers)
                throw new StudioBusinessException(
                    "SET_HAS_PRACTICE",
                    StatusCodes.Status409Conflict,
                    "Bộ câu hỏi đã có bài practice — không thể thay snapshot. Tạo project mới hoặc giữ set hiện tại.");
        }

        var title = FirstNonEmpty(jd?.Title, plan?.Title, jd?.DetectedRole, project.Name);
        var jdContent = string.IsNullOrWhiteSpace(jd?.Content)
            ? "(Studio) Job description trống."
            : jd!.Content.Trim();
        var jdSourceType = jd?.SourceType == DomainLayer.Studio.Enums.JobDescriptionSourceType.UploadedFile
            ? "UploadedFile"
            : "PastedText";
        var jdFileName = jdSourceType == "UploadedFile"
            ? (string.IsNullOrWhiteSpace(jd?.OriginalFileName) ? null : jd!.OriginalFileName!.Trim())
            : null;
        var planJson = string.IsNullOrWhiteSpace(plan?.SourcePlanJson) ? "{}" : plan!.SourcePlanJson!;
        var ownerId = project.OwnerId != Guid.Empty ? project.OwnerId : userId;

        // Section name làm fallback FocusArea (nếu TagsJson thiếu)
        var sectionNames = await dbContext.PlanSections.AsNoTracking()
            .Where(s => s.IsActive && interviewQuestions.Select(q => q.PlanSectionId).Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var snapshot = interviewQuestions.Select((q, index) =>
        {
            var meta = StudioRagQuestionMapper.ParseMeta(q.TagsJson);
            sectionNames.TryGetValue(q.PlanSectionId, out var sectionName);

            var skill = FirstNonEmpty(meta.Skill);
            var focus = FirstNonEmpty(meta.FocusArea, sectionName);
            var rationaleCore = FirstNonEmpty(meta.Rationale, q.ScoringRubric);
            // Sample answer giữ nguyên đáp án mẫu — KHÔNG nhét starter code câu hỏi vào đây
            // (trước đây append "Code snippet:" làm UI sampleAnswer hiện nhầm stub đề bài).
            var sample = FirstNonEmpty(q.ExpectedAnswer);

            var rationaleParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(rationaleCore))
                rationaleParts.Add(rationaleCore!);
            if (!string.IsNullOrWhiteSpace(meta.CodeTemplateType))
                rationaleParts.Add("template=" + meta.CodeTemplateType.Trim());
            // Starter code câu hỏi → rationale meta để History infer template card
            if (!string.IsNullOrWhiteSpace(meta.CodeSnippet))
            {
                var snipFlat = meta.CodeSnippet.Trim()
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace("\n", "\\n", StringComparison.Ordinal)
                    .Replace(';', ',');
                rationaleParts.Add("snippet=" + snipFlat);
            }
            if (!string.IsNullOrWhiteSpace(meta.ImageHint))
                rationaleParts.Add("imageHint=" + meta.ImageHint.Trim().Replace(';', ','));
            var rationale = rationaleParts.Count > 0 ? string.Join(";", rationaleParts) : null;

            var rubricDoc = StudioRagQuestionMapper.ResolveRubricDocument(meta, q.ScoringRubric);
            var criteriaJson = RubricNormalizer.SerializeForStorage(rubricDoc);

            var citationsJson = meta.Citations is { Count: > 0 }
                ? JsonSerializer.Serialize(meta.Citations, JsonOptions)
                : "[]";

            return new DomainLayer.Entities.QuestionSetQuestion
            {
                Order = q.OrderIndex > 0 ? q.OrderIndex : index + 1,
                Question = q.Content,
                QuestionType = MapQuestionType(q.Type),
                Difficulty = MapDifficulty(q.Difficulty),
                Skill = skill,
                FocusArea = focus,
                Rationale = rationale,
                SampleAnswer = sample,
                AttachedImageBlobPath = string.IsNullOrWhiteSpace(meta.AttachedImageBlobPath)
                    ? null
                    : meta.AttachedImageBlobPath.Trim(),
                AnswerMethod = ApplicationLayer.Helpers.AnswerMethodNormalizer.Resolve(
                    meta.AnswerMethod, meta.CodeTemplateType, meta.CodeSnippet),
                EvaluationCriteriaJson = criteriaJson,
                CitationsJson = citationsJson
            };
        }).ToList();

        if (existing is null)
        {
            var set = new DomainLayer.Entities.QuestionSet
            {
                OwnerId = ownerId,
                SourceJobId = null,
                SourceProjectId = projectId,
                SourcePlanId = plan?.Id,
                SourceRunId = latestRun?.Id,
                Status = DomainLayer.Constants.QuestionSetStatus.Draft,
                Title = title,
                JobDescription = jdContent,
                JdSourceType = jdSourceType,
                JdOriginalFileName = jdFileName,
                HrNote = $"STUDIO_SAVE; project={projectId}",
                PlanJson = planJson,
                GeneratedAt = latestRun?.CompletedAt ?? DateTime.UtcNow
            };

            foreach (var q in snapshot)
                q.QuestionSetId = set.Id;

            dbContext.QuestionSets.Add(set);
            dbContext.QuestionSetQuestions.AddRange(snapshot);
            await dbContext.SaveChangesAsync(ct);
            return new StudioSaveQuestionSetResponseDto(set.Id, set.Status, snapshot.Count, set.CreatedAt);
        }

        existing.Title = title;
        existing.JobDescription = jdContent;
        existing.JdSourceType = jdSourceType;
        existing.JdOriginalFileName = jdFileName;
        existing.HrNote = $"STUDIO_SAVE; project={projectId}";
        existing.PlanJson = planJson;
        existing.SourcePlanId = plan?.Id;
        existing.SourceRunId = latestRun?.Id;
        existing.GeneratedAt = latestRun?.CompletedAt ?? DateTime.UtcNow;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.OwnerId = ownerId;

        if (existing.Questions.Count > 0)
            dbContext.QuestionSetQuestions.RemoveRange(existing.Questions);

        foreach (var q in snapshot)
            q.QuestionSetId = existing.Id;

        dbContext.QuestionSetQuestions.AddRange(snapshot);
        await dbContext.SaveChangesAsync(ct);
        return new StudioSaveQuestionSetResponseDto(existing.Id, existing.Status, snapshot.Count, existing.UpdatedAt ?? existing.CreatedAt);
    }

    public async Task PublishFromProjectAsync(
        Guid projectId, Guid userId, StudioPublishRequestDto? request, CancellationToken ct)
    {
        var detail = await GetAsync(projectId, userId, ct);
        Guid? questionSetId = detail.QuestionSetId;
        var selected = request?.InterviewQuestionIds;
        List<Guid>? mappedSetQuestionIds = null;

        try
        {
            // SCRUM-439: Save subset trước rồi publish (mọi câu trong snapshot = active)
            var saved = await SaveQuestionSetCoreAsync(projectId, userId, selected, ct);
            questionSetId = saved.QuestionSetId;
        }
        catch (StudioBusinessException ex) when (ex.ErrorCode == "SET_HAS_PRACTICE")
        {
            if (questionSetId is null)
                throw;

            // Không thay snapshot được — soft-select theo map Content/Order
            if (selected is { Count: > 0 })
            {
                var idSet = selected.Where(id => id != Guid.Empty).Distinct().ToHashSet();
                var interviewRows = await dbContext.InterviewQuestions.AsNoTracking()
                    .Where(q => q.ProjectId == projectId && idSet.Contains(q.Id))
                    .Select(q => new { q.Id, q.Content, q.OrderIndex })
                    .ToListAsync(ct);
                var setRows = await dbContext.QuestionSetQuestions.AsNoTracking()
                    .Where(q => q.QuestionSetId == questionSetId.Value)
                    .Select(q => new { q.Id, q.Question, q.Order })
                    .ToListAsync(ct);

                mappedSetQuestionIds = ApplicationLayer.Helpers.PublishQuestionSelectionHelper
                    .MapInterviewSelectionToSetQuestionIds(
                        interviewRows.Select(r => (r.Id, r.Content ?? string.Empty, r.OrderIndex)).ToList(),
                        setRows.Select(r => (r.Id, r.Question ?? string.Empty, r.Order)).ToList());

                if (mappedSetQuestionIds.Count == 0)
                    throw new StudioBusinessException(
                        "PUBLISH_SELECTION_MAP_FAILED",
                        StatusCodes.Status400BadRequest,
                        "Không map được câu Studio sang bộ đã có practice — hãy unpublish hoặc tạo project mới.");
            }
        }

        if (questionSetId is null)
            throw new StudioBusinessException("SET_NOT_FOUND", StatusCodes.Status404NotFound,
                "Không lưu được bộ câu hỏi để publish.");

        await questionSetService.PublishAsync(
            questionSetId.Value,
            userId,
            new ApplicationLayer.DTOs.QuestionSet.PublishQuestionSetRequestDto
            {
                QuestionIds = mappedSetQuestionIds,
                TimeLimitMinutes = request?.TimeLimitMinutes,
                AutoRecommendEnabled = request?.AutoRecommendEnabled,
                RecommendationMinScore = request?.RecommendationMinScore
            });
    }

    public async Task<ApplicationLayer.DTOs.QuestionSet.QuestionSetActionResponseDto> UnpublishFromProjectAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var detail = await GetAsync(projectId, userId, ct);
        if (detail.QuestionSetId is null)
            throw new StudioBusinessException("SET_NOT_FOUND", StatusCodes.Status404NotFound, "Project chưa có bộ câu hỏi đã Save.");

        return await questionSetService.UnpublishAsync(detail.QuestionSetId.Value, userId);
    }

    private async Task<StudioProjectDetailDto> ToDetailAsync(InterviewProject p, CancellationToken ct)
    {
        var set = await dbContext.QuestionSets.AsNoTracking()
            .Where(qs => qs.SourceProjectId == p.Id && qs.IsActive)
            .Select(qs => new { qs.Id, qs.Status })
            .FirstOrDefaultAsync(ct);

        return new StudioProjectDetailDto(
            p.Id,
            p.OwnerId,
            p.Name,
            p.Description,
            p.Status,
            p.LatestPlanRevision,
            set?.Id,
            set is not null && set.Status == DomainLayer.Constants.QuestionSetStatus.Published,
            set?.Status);
    }

    private static string MapQuestionType(QuestionType type) => type switch
    {
        QuestionType.Behavioral => "behavioral",
        QuestionType.SystemDesign => "system-design",
        QuestionType.ProblemSolving => "problem-solving",
        QuestionType.Situational => "situational",
        QuestionType.FollowUp => "follow-up",
        _ => "technical"
    };

    private static string MapDifficulty(QuestionDifficulty d) => d switch
    {
        QuestionDifficulty.Easy => "easy",
        QuestionDifficulty.Hard => "hard",
        _ => "medium"
    };

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}

public sealed class JobDescriptionService(
    AppDbContext dbContext,
    IInterviewProjectService projectService,
    IJobDescriptionAnalyzer analyzer,
    IRagService ragService) : IJobDescriptionService
{
    public async Task<AnalyzeJobDescriptionResponse> UpsertAsync(Guid projectId, Guid userId, UpsertJobDescriptionRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        // SCRUM-416: validate cấu trúc + domain IT (keyword) trước khi gọi LLM.
        var content = ApplicationLayer.Helpers.JobDescriptionValidator.Validate(request.Content);

        // SCRUM-432: classify IT job posting TRƯỚC SaveChanges — fail → 422/502, không ghi DB.
        var summary = await analyzer.AnalyzeAsync(content, ct);

        var row = await dbContext.StudioJobDescriptions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (row is null)
        {
            row = new JobDescription
            {
                ProjectId = projectId,
                Content = content,
                SourceType = request.SourceType,
                OriginalFileName = request.OriginalFileName
            };
            dbContext.StudioJobDescriptions.Add(row);
        }
        else
        {
            row.Content = content;
            row.SourceType = request.SourceType;
            if (request.OriginalFileName is not null)
                row.OriginalFileName = request.OriginalFileName;
            row.UpdatedAt = DateTime.UtcNow;
        }

        row.WordCount = content.Split(new char[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        row.CharacterCount = content.Length;
        row.DetectedRole = summary.DetectedRole;
        row.DetectedSeniority = summary.DetectedSeniority;
        row.DetectedLanguage = summary.DetectedLanguage;
        row.DetectedSkillsJson = System.Text.Json.JsonSerializer.Serialize(summary.Skills);
        row.Title = FirstNonEmptyLocal(summary.Position, summary.JobTitle, summary.DetectedRole);
        row.ExtractedInformationJson = StudioAiConfigurationHelper.SerializeExtractedInformation(
            summary.Responsibilities ?? [],
            summary.Summary);
        await dbContext.SaveChangesAsync(ct);

        return summary with
        {
            Position = row.Title,
            JobTitle = row.Title
        };
    }

    public async Task<AnalyzeJobDescriptionResponse> AnalyzeAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        var row = await dbContext.StudioJobDescriptions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("JOB_DESCRIPTION_NOT_FOUND", StatusCodes.Status404NotFound, "Chưa có JD.");

        var summary = await analyzer.AnalyzeAsync(row.Content, ct);
        row.DetectedRole = summary.DetectedRole;
        row.DetectedSeniority = summary.DetectedSeniority;
        row.DetectedLanguage = summary.DetectedLanguage;
        row.DetectedSkillsJson = System.Text.Json.JsonSerializer.Serialize(summary.Skills);
        // SCRUM-416: lưu vị trí extract vào Title (HR có thể PATCH sau).
        row.Title = FirstNonEmptyLocal(summary.Position, summary.JobTitle, summary.DetectedRole);
        row.ExtractedInformationJson = StudioAiConfigurationHelper.SerializeExtractedInformation(
            summary.Responsibilities ?? [],
            summary.Summary);
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return summary with
        {
            Position = row.Title,
            JobTitle = row.Title
        };
    }

    public async Task<RecommendInterviewConfigurationResponseDto> RecommendConfigurationAsync(
        Guid projectId,
        Guid userId,
        RecommendInterviewConfigurationRequestDto? request,
        CancellationToken ct)
    {
        var project = await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        var row = await dbContext.StudioJobDescriptions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("JOB_DESCRIPTION_NOT_FOUND", StatusCodes.Status404NotFound, "Chưa có JD.");

        string[] skills = [];
        if (!string.IsNullOrWhiteSpace(row.DetectedSkillsJson))
        {
            try { skills = System.Text.Json.JsonSerializer.Deserialize<string[]>(row.DetectedSkillsJson) ?? []; }
            catch { skills = []; }
        }

        var jobProfile = StudioAiConfigurationHelper.BuildJobProfile(
            row.Title, row.DetectedRole, row.DetectedSeniority, row.DetectedLanguage, skills, row.ExtractedInformationJson);

        var documentIds = await dbContext.StudioKnowledgeDocuments
            .Where(x => x.ProjectId == projectId && x.IsActive && x.IsSelected
                        && x.ProcessingStatus == DocumentProcessingStatus.Completed
                        && x.KnowledgeDocumentId != null)
            .Select(x => x.KnowledgeDocumentId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var ragResult = await ragService.RecommendInterviewConfigurationAsync(new RecommendInterviewConfigurationRequest
        {
            OwnerId = project.OwnerId,
            JobDescription = row.Content,
            JobProfile = StudioAiConfigurationHelper.ToRagJobProfile(jobProfile),
            DocumentIds = documentIds,
            NumberOfQuestions = request?.NumberOfQuestions
        }, ct);

        if (!ragResult.Success || ragResult.RecommendedConfiguration is null)
        {
            throw new StudioBusinessException(
                "RECOMMEND_CONFIGURATION_FAILED",
                StatusCodes.Status502BadGateway,
                ragResult.Detail ?? ragResult.Error ?? "Đề xuất cấu hình phỏng vấn thất bại.");
        }

        var recommended = StudioAiConfigurationHelper.MapRecommendedConfiguration(ragResult.RecommendedConfiguration.Value);

        var settings = await dbContext.StudioSettings.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (settings is null)
        {
            settings = new StudioSettings { ProjectId = projectId, AppliedPlanId = null };
            dbContext.StudioSettings.Add(settings);
        }

        // Chỉ lưu draft AI — không ghi đè focusAreas / distribution / styles HR đã lưu.
        settings.AiRecommendationJson = StudioAiConfigurationHelper.SerializeRecommendedConfiguration(recommended);
        settings.AiRecommendationGeneratedAt = DateTime.UtcNow;
        settings.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return new RecommendInterviewConfigurationResponseDto(jobProfile, recommended);
    }

    public async Task<AnalyzeJobDescriptionResponse> UpdatePositionAsync(
        Guid projectId, Guid userId, UpdateJobDescriptionPositionRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var position = (request.Position ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(position))
            throw new StudioBusinessException("POSITION_REQUIRED", StatusCodes.Status422UnprocessableEntity, "Vị trí không được rỗng.");
        if (position.Length > 150)
            throw new StudioBusinessException("POSITION_TOO_LONG", StatusCodes.Status422UnprocessableEntity, "Vị trí tối đa 150 ký tự.");

        var row = await dbContext.StudioJobDescriptions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("JOB_DESCRIPTION_NOT_FOUND", StatusCodes.Status404NotFound, "Chưa có JD.");

        row.Title = position;
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return BuildSummaryFromRow(row);
    }

    /// <summary>SCRUM-417: HR xác nhận Position + Level (+ Role / Skills) — lưu trước generate plan.</summary>
    public async Task<AnalyzeJobDescriptionResponse> UpdateMetadataAsync(
        Guid projectId, Guid userId, UpdateJobDescriptionMetadataRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);

        var position = (request.Position ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(position))
            throw new StudioBusinessException("POSITION_REQUIRED", StatusCodes.Status422UnprocessableEntity, "Vị trí không được rỗng.");
        if (position.Length > 150)
            throw new StudioBusinessException("POSITION_TOO_LONG", StatusCodes.Status422UnprocessableEntity, "Vị trí tối đa 150 ký tự.");

        var seniority = ApplicationLayer.Studio.Helpers.StudioJdSeniority.NormalizeDisplay(request.DetectedSeniority)
            ?? throw new StudioBusinessException(
                "SENIORITY_REQUIRED",
                StatusCodes.Status422UnprocessableEntity,
                "Cấp độ bắt buộc (Intern|Junior|Mid|Senior|Lead).");

        var role = string.IsNullOrWhiteSpace(request.DetectedRole) ? null : request.DetectedRole.Trim();
        if (role is { Length: > 150 })
            role = role[..150];

        var row = await dbContext.StudioJobDescriptions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("JOB_DESCRIPTION_NOT_FOUND", StatusCodes.Status404NotFound, "Chưa có JD.");

        row.Title = position;
        row.DetectedSeniority = seniority;
        row.DetectedRole = role;
        // Skills null = giữ nguyên; list (kể cả []) = HR ghi đè DetectedSkillsJson
        if (request.Skills is not null)
            row.DetectedSkillsJson = System.Text.Json.JsonSerializer.Serialize(
                ApplicationLayer.Studio.Helpers.StudioHrSkillsHelper.Normalize(request.Skills));
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return BuildSummaryFromRow(row);
    }

    private static AnalyzeJobDescriptionResponse BuildSummaryFromRow(DomainLayer.Studio.JobDescription row)
    {
        string[] skills = [];
        if (!string.IsNullOrWhiteSpace(row.DetectedSkillsJson))
        {
            try { skills = System.Text.Json.JsonSerializer.Deserialize<string[]>(row.DetectedSkillsJson) ?? []; }
            catch { skills = []; }
        }

        var (responsibilities, summaryText) = StudioAiConfigurationHelper.ParseExtractedInformation(row.ExtractedInformationJson);

        return new AnalyzeJobDescriptionResponse(
            row.DetectedRole,
            row.DetectedSeniority,
            row.DetectedLanguage,
            skills,
            row.Title,
            JobTitle: row.Title,
            ExperienceLevel: StudioJdSeniority.ToRagExperienceLevel(row.DetectedSeniority),
            Responsibilities: responsibilities,
            Summary: summaryText);
    }

    public async Task<JobDescriptionContentDto?> GetContentAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        var row = await dbContext.StudioJobDescriptions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (row is null) return null;

        var summary = BuildSummaryFromRow(row);
        var position = FirstNonEmptyLocal(row.Title, row.DetectedRole);

        return new JobDescriptionContentDto(
            row.Content,
            row.SourceType,
            row.OriginalFileName,
            row.WordCount,
            row.CharacterCount,
            summary with { Position = position, JobTitle = position },
            position);
    }

    private static string? FirstNonEmptyLocal(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return null;
    }
}

public sealed class InterviewPlanService(
    AppDbContext dbContext,
    IInterviewProjectService projectService,
    IRagService ragService,
    IAiChatService aiChatService,
    ISubscriptionGateService subscriptionGate,
    IUsageMeteringService usageMetering) : IInterviewPlanService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Plan bắt buộc có SessionId (FK → ai_chat_sessions). Lúc generate/refine
    /// user thường chưa mở chat → tạo session project+user trước khi Save plan.
    /// </summary>
    private async Task<Guid> EnsureProjectChatSessionIdAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var session = await dbContext.AiChatSessions
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId && x.IsActive, ct);
        if (session is not null)
            return session.Id;

        session = new AiChatSession
        {
            ProjectId = projectId,
            UserId = userId,
            SelectionMode = AiModelSelectionMode.Auto,
            SelectedModelDisplayName = "Studio"
        };
        dbContext.AiChatSessions.Add(session);
        await dbContext.SaveChangesAsync(ct);
        return session.Id;
    }

    /// <summary>
    /// Cấp revision kế tiếp. Không chỉ tin LatestPlanRevision trên project —
    /// sau fail/orphan data MAX(Revision) trong DB có thể cao hơn → tránh unique IX (ProjectId, Revision).
    /// </summary>
    private async Task<int> AllocateNextPlanRevisionAsync(InterviewProject project, CancellationToken ct)
    {
        var maxInDb = await dbContext.InterviewPlans
            .Where(x => x.ProjectId == project.Id)
            .Select(x => (int?)x.Revision)
            .MaxAsync(ct) ?? 0;
        var next = Math.Max(project.LatestPlanRevision, maxInDb) + 1;
        project.LatestPlanRevision = next;
        return next;
    }

    public async Task<IReadOnlyList<PlanSummaryDto>> ListAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        return await dbContext.InterviewPlans.Where(x => x.ProjectId == projectId && x.IsActive)
            .OrderByDescending(x => x.Revision)
            .Select(x => new PlanSummaryDto(x.Id, x.Revision, x.Title, x.Status, x.TotalQuestions))
            .ToListAsync(ct);
    }

    public async Task<PlanDetailDto?> GetCurrentAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        var plan = await dbContext.InterviewPlans
            .Where(x => x.ProjectId == projectId && x.IsActive && (x.Status == InterviewPlanStatus.Approved || x.Status == InterviewPlanStatus.AwaitingApproval))
            .OrderByDescending(x => x.Status == InterviewPlanStatus.Approved)
            .ThenByDescending(x => x.Revision)
            .FirstOrDefaultAsync(ct);
        if (plan is null) return null;
        // Trả full detail (sections) — FE cần để hiện approval card / mở khóa chat
        return await GetDetailAsync(projectId, plan.Id, userId, ct);
    }

    public async Task<PlanDetailDto> GetDetailAsync(Guid projectId, Guid planId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        var plan = await dbContext.InterviewPlans.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.Id == planId && x.IsActive, ct)
            ?? throw new StudioBusinessException("PLAN_NOT_FOUND", 404, "Không tìm thấy plan.");
        var sections = await dbContext.PlanSections
            .Where(x => x.InterviewPlanId == plan.Id && x.IsActive)
            .OrderBy(x => x.OrderIndex)
            .Select(x => new PlanSectionItemDto(x.Id, x.Name, x.Description, x.OrderIndex, x.NumberOfQuestions, x.Difficulty, x.EstimatedMinutes))
            .ToListAsync(ct);
        if (sections.Count == 0)
        {
            // Fallback để approval card luôn có data section cơ bản.
            sections =
            [
                new PlanSectionItemDto(Guid.Empty, "Technical Foundations", "Core backend fundamentals", 1, Math.Max(1, plan.TotalQuestions / 5), QuestionDifficulty.Medium, 15),
                new PlanSectionItemDto(Guid.Empty, "System Design", "Architecture and trade-offs", 2, Math.Max(1, plan.TotalQuestions / 3), QuestionDifficulty.Hard, 25),
                new PlanSectionItemDto(Guid.Empty, "Problem Solving", "Debug and optimization scenarios", 3, Math.Max(1, plan.TotalQuestions / 5), QuestionDifficulty.Medium, 15),
                new PlanSectionItemDto(Guid.Empty, "Behavioral", "Communication and ownership", 4, Math.Max(1, plan.TotalQuestions / 5), QuestionDifficulty.Medium, 15),
                new PlanSectionItemDto(Guid.Empty, "Follow-up", "Deep dive follow-up questions", 5, 1, QuestionDifficulty.Medium, 5)
            ];
        }

        var focusRows = await dbContext.PlanFocusAreas
            .Where(x => x.InterviewPlanId == plan.Id && x.IsActive)
            .OrderBy(x => x.OrderIndex)
            .Select(x => new { x.Name, x.Weight, x.OrderIndex })
            .ToListAsync(ct);
        var focusAreas = focusRows
            .Select(x => new PlanFocusAreaItemDto(x.Name, x.Weight, x.OrderIndex, Array.Empty<string>()))
            .ToList();
        if (focusAreas.Count == 0)
        {
            focusAreas =
            [
                new PlanFocusAreaItemDto("Technical", 0.35m, 1, Array.Empty<string>()),
                new PlanFocusAreaItemDto("System Design", 0.30m, 2, Array.Empty<string>()),
                new PlanFocusAreaItemDto("Problem Solving", 0.20m, 3, Array.Empty<string>()),
                new PlanFocusAreaItemDto("Behavioral", 0.15m, 4, Array.Empty<string>())
            ];
        }

        // SCRUM-369: gắn tên file RAG vào từng focus từ SourcePlanJson
        focusAreas = StudioRagPlanMapper.EnrichFocusAreasWithRagSources(focusAreas, plan.SourcePlanJson).ToList();
        // SCRUM-420: provenance waterfall trên focus
        focusAreas = StudioRagPlanMapper.EnrichFocusAreasWithProvenance(focusAreas, plan.SourcePlanJson).ToList();
        var coverageItems = StudioRagPlanMapper.ExtractCoverageItems(plan.SourcePlanJson);

        // Ưu tiên difficulty_distribution từ RAG (số câu); fallback = cộng NumberOfQuestions theo difficulty section
        var fromSource = StudioRagPlanMapper.TryGetDifficultyMix(plan.SourcePlanJson, plan.TotalQuestions);
        var difficultyMix = fromSource is { } mix
            ? new PlanDifficultyMixDto(mix.Easy, mix.Medium, mix.Hard)
            : new PlanDifficultyMixDto(
                sections.Where(x => x.Difficulty == QuestionDifficulty.Easy).Sum(x => x.NumberOfQuestions),
                sections.Where(x => x.Difficulty == QuestionDifficulty.Medium).Sum(x => x.NumberOfQuestions),
                sections.Where(x => x.Difficulty == QuestionDifficulty.Hard).Sum(x => x.NumberOfQuestions));

        var sourcesUsed = new List<string>();
        var hasJd = await dbContext.StudioJobDescriptions.AnyAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (hasJd) sourcesUsed.Add("job-description");
        var selectedDocs = await dbContext.StudioKnowledgeDocuments.CountAsync(x => x.ProjectId == projectId && x.IsActive && x.IsSelected && x.ProcessingStatus == DocumentProcessingStatus.Completed, ct);
        if (selectedDocs > 0) sourcesUsed.Add($"knowledge-documents:{selectedDocs}");
        if (!string.IsNullOrWhiteSpace(plan.SourcePlanJson) || string.Equals(plan.GeneratedByModelName, "RAG", StringComparison.OrdinalIgnoreCase))
            sourcesUsed.Add("rag-retrieve");
        foreach (var file in StudioRagPlanMapper.ExtractCitationSourceFiles(plan.SourcePlanJson).Take(8))
        {
            if (!sourcesUsed.Contains(file, StringComparer.OrdinalIgnoreCase))
                sourcesUsed.Add(file);
        }
        if (sourcesUsed.Count == 0) sourcesUsed.Add("mock-default");

        var sourceDetails = StudioRagPlanMapper.BuildPlanSourceDetails(sourcesUsed, plan.SourcePlanJson);

        var currentSettings = await dbContext.StudioSettings
            .Include(s => s.FocusAreas.Where(f => f.IsActive))
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        var embeddedSnapshot = StudioPlanSettingsSnapshotHelper.TryExtract(plan.SourcePlanJson);
        var isSettingsStale = currentSettings is null
            || StudioPlanSettingsSnapshotHelper.IsStale(embeddedSnapshot, currentSettings);

        var outlineItems = StudioRagPlanMapper.ExtractOutlineItems(plan.SourcePlanJson);

        return new PlanDetailDto(
            plan.Id,
            plan.ProjectId,
            plan.Revision,
            plan.Title,
            plan.Status,
            plan.TotalQuestions,
            plan.InterviewLengthMinutes,
            plan.Difficulty,
            difficultyMix,
            focusAreas,
            sourcesUsed,
            sections,
            sections,
            plan.ConcurrencyVersion,
            sourceDetails,
            coverageItems,
            isSettingsStale,
            outlineItems,
            plan.GeneratedByModelName);
    }

    public async Task<PlanSummaryDto> GenerateInitialAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        // SCRUM-367: giống GeneratePlanJob — gọi RAG sync (retrieve SYSTEM+HR theo OwnerId)
        // SCRUM-382: Free cooldown tạo bộ / plan
        await subscriptionGate.CheckGenerateSetAsync(userId);

        var project = await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var jd = await dbContext.StudioJobDescriptions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("JD_REQUIRED", StatusCodes.Status422UnprocessableEntity, "Cần Job Description trước khi tạo plan.");
        if (string.IsNullOrWhiteSpace(jd.Content))
            throw new StudioBusinessException("JD_EMPTY", StatusCodes.Status422UnprocessableEntity, "Job Description đang trống.");
        // SCRUM-416: bắt buộc có vị trí (Title) trước khi generate.
        if (string.IsNullOrWhiteSpace(jd.Title))
            throw new StudioBusinessException(
                "POSITION_REQUIRED",
                StatusCodes.Status422UnprocessableEntity,
                "Cần xác nhận vị trí trước khi tạo plan.");
        // SCRUM-417: bắt buộc Level HR đã confirm.
        var confirmedSeniority = ApplicationLayer.Studio.Helpers.StudioJdSeniority.NormalizeDisplay(jd.DetectedSeniority);
        if (confirmedSeniority is null)
            throw new StudioBusinessException(
                "SENIORITY_REQUIRED",
                StatusCodes.Status422UnprocessableEntity,
                "Cần xác nhận cấp độ (Intern|Junior|Mid|Senior|Lead) trước khi tạo plan.");

        var selectedDocs = await dbContext.StudioKnowledgeDocuments.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.IsActive && x.IsSelected
                        && x.ProcessingStatus == DocumentProcessingStatus.Completed
                        && x.KnowledgeDocumentId != null)
            .Select(x => new { x.KnowledgeDocumentId, x.FileName })
            .ToListAsync(ct);
        var selectedReady = selectedDocs.Count;
        var documentIds = selectedDocs.Select(x => x.KnowledgeDocumentId!.Value).Distinct().ToList();
        // Knowledge documents optional — chỉ JD là bắt buộc để lập plan (SCRUM: JD-only)

        var settings = await dbContext.StudioSettings.AsNoTracking()
            .Include(s => s.FocusAreas.Where(f => f.IsActive))
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);

        // SCRUM-422/423: thiếu distribution/focus → recommend + seed AI config (kèm frame count/difficulty/minutes)
        settings = await EnsureAiConfigSeededIfNeededAsync(
            projectId, project.OwnerId, jd, settings, documentIds, ct);

        var numberOfQuestions = settings?.NumberOfQuestions > 0 ? settings.NumberOfQuestions : 15;
        var difficulty = (settings?.Difficulty ?? QuestionDifficulty.Medium).ToString().ToLowerInvariant();
        var skills = ParseSkillsJson(jd.DetectedSkillsJson);
        // preferredMinutes sau seed đã là AI-derived (count×4) trên lần tạo đầu
        var preferredMinutes = settings?.InterviewLengthMinutes;
        var interviewMinutes = preferredMinutes is > 0 ? preferredMinutes.Value : Math.Clamp(numberOfQuestions * 4, 30, 120);

        var questionDistribution = settings is not null
            ? StudioAiConfigurationHelper.ParseQuestionDistribution(settings.QuestionDistributionJson)
            : [];
        if (questionDistribution.Count == 0 && settings is not null && settings.NumberOfQuestions > 0)
        {
            var legacyTypes = StudioQuestionTypesHelper.ParseOrDefault(settings.QuestionTypesJson);
            var (derived, _) = StudioQuestionTaxonomyMapper.FromLegacyQuestionTypes(legacyTypes, settings.NumberOfQuestions);
            questionDistribution = derived;
        }

        var focusAreas = settings is not null
            ? StudioAiConfigurationHelper.MapFocusAreas(settings.FocusAreas)
            : [];
        var questionStyles = settings is not null
            ? StudioAiConfigurationHelper.ParseQuestionStyles(settings.QuestionStylesJson)
            : [];
        if (questionStyles.Count == 0 && settings is not null)
        {
            questionStyles = StudioQuestionTaxonomyMapper.ExtractStylesFromLegacyTypes(
                StudioQuestionTypesHelper.ParseOrDefault(settings.QuestionTypesJson));
        }
        var codingTaskTypes = ParseCodeTemplatesList(settings?.CodeTemplatesJson);
        var contentMode = string.IsNullOrWhiteSpace(settings?.ContentMode) ? "Mixed" : settings.ContentMode.Trim();

        // SCRUM-370: loại câu từ distribution hoặc legacy settings
        var studioQuestionTypes = questionDistribution.Count > 0
            ? StudioQuestionTaxonomyMapper.ToLegacyQuestionTypes(questionDistribution).ToList()
            : StudioQuestionTypesHelper.ParseOrDefault(settings?.QuestionTypesJson);
        var outputLanguage = StudioOutputLanguage.Normalize(settings?.Language);

        var settingsSnapshot = settings is not null
            ? StudioPlanSettingsSnapshotHelper.BuildFrom(settings)
            : null;

        GeneratePlanResult ragResult;
        try
        {
            ragResult = await ragService.GeneratePlanAsync(new GeneratePlanRequest
            {
                OwnerId = userId,
                JobDescription = jd.Content,
                NumberOfQuestions = numberOfQuestions,
                Difficulty = difficulty,
                QuestionTypes = studioQuestionTypes,
                Skills = skills,
                Language = outputLanguage,
                DocumentIds = documentIds,
                ExperienceLevel = ApplicationLayer.Studio.Helpers.StudioJdSeniority.ToRagExperienceLevel(confirmedSeniority),
                QuestionDistribution = questionDistribution.Count > 0
                    ? questionDistribution.Select(d => new RagQuestionDistributionItemDto
                    {
                        Category = d.Category,
                        Percentage = d.Percentage,
                        QuestionCount = d.QuestionCount
                    }).ToList()
                    : null,
                FocusAreas = focusAreas.Count > 0
                    ? focusAreas.Select(f => new RagFocusAreaItemDto
                    {
                        Name = f.Name,
                        Weight = f.Weight,
                        OrderIndex = f.OrderIndex,
                        Description = f.Description,
                        SourceReason = f.SourceReason
                    }).ToList()
                    : null,
                QuestionStyles = questionStyles.Count > 0 ? questionStyles.ToList() : null,
                CodingTaskTypes = codingTaskTypes.Count > 0 ? codingTaskTypes.ToList() : null,
                HrNote = StudioRagPlanHrNoteBuilder.BuildInitial(
                    projectId, selectedReady, numberOfQuestions, interviewMinutes, outputLanguage,
                    jd.Title, jd.DetectedRole, confirmedSeniority,
                    questionDistribution, focusAreas, questionStyles, codingTaskTypes, contentMode)
            }, ct);
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException("RAG_GENERATE_PLAN_FAILED", StatusCodes.Status502BadGateway,
                $"RAG tạo plan thất bại: {ex.Message}");
        }

        StudioRagPlanMapper.MappedPlan mapped;
        try
        {
            mapped = StudioRagPlanMapper.MapFromRagPlanObject(ragResult.Plan, numberOfQuestions, preferredMinutes);
            // SCRUM-434: bổ sung đủ skill JD vào focus + sync coverage (không tin LLM một mình)
            mapped = StudioPlanFocusJdCompleter.EnsureAllJdSkills(mapped, skills, numberOfQuestions);
            // SCRUM-435: gán skill outline theo % focus (Live Preview khớp ngay sau tạo plan)
            mapped = StudioOutlineFocusRedistributor.ApplyFocusWeightsToOutline(mapped);
            // HR đã chọn số câu trên cột phải — luôn giữ đúng, không để LLM/schema mẫu (10) ghi đè
            if (mapped.TotalQuestions != numberOfQuestions)
            {
                mapped = mapped with
                {
                    TotalQuestions = numberOfQuestions,
                    InterviewLengthMinutes = preferredMinutes is > 0
                        ? preferredMinutes.Value
                        : mapped.InterviewLengthMinutes
                };
            }
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException("RAG_PLAN_MAP_FAILED", StatusCodes.Status502BadGateway,
                $"Không map được plan RAG: {ex.Message}");
        }

        if (settingsSnapshot is not null)
        {
            mapped = mapped with
            {
                SourcePlanJson = StudioPlanSettingsSnapshotHelper.EmbedInSourcePlanJson(
                    mapped.SourcePlanJson,
                    settingsSnapshot)
            };
        }

        var revision = await AllocateNextPlanRevisionAsync(project, ct);
        var sessionId = await EnsureProjectChatSessionIdAsync(projectId, userId, ct);
        var plan = new InterviewPlan
        {
            ProjectId = projectId,
            SessionId = sessionId,
            Revision = revision,
            Status = InterviewPlanStatus.AwaitingApproval,
            Title = mapped.Title,
            TotalQuestions = mapped.TotalQuestions,
            InterviewLengthMinutes = mapped.InterviewLengthMinutes,
            SeniorityLevel = mapped.SeniorityLevel,
            Difficulty = mapped.Difficulty,
            Language = outputLanguage,
            GeneratedByModelName = "RAG",
            SourcePlanJson = mapped.SourcePlanJson
        };
        dbContext.InterviewPlans.Add(plan);
        await dbContext.SaveChangesAsync(ct);

        foreach (var s in mapped.Sections)
        {
            dbContext.PlanSections.Add(new PlanSection
            {
                InterviewPlanId = plan.Id,
                Name = s.Name,
                Description = s.Description,
                OrderIndex = s.OrderIndex,
                NumberOfQuestions = s.NumberOfQuestions,
                Difficulty = s.Difficulty,
                EstimatedMinutes = s.EstimatedMinutes
            });
        }
        foreach (var f in mapped.FocusAreas)
        {
            dbContext.PlanFocusAreas.Add(new PlanFocusArea
            {
                InterviewPlanId = plan.Id,
                Name = f.Name,
                Weight = f.Weight,
                OrderIndex = f.OrderIndex
            });
        }

        project.LatestPlanRevision = revision;
        project.Status = InterviewProjectStatus.AwaitingApproval;
        await SyncStudioSettingsFromPlanAsync(projectId, mapped, studioQuestionTypes, ct);
        await dbContext.SaveChangesAsync(ct);

        // SCRUM-376: persist transcript lập plan
        await aiChatService.AppendUserAndAssistantAsync(
            projectId,
            userId,
            "Lập plan từ JD + tài liệu đã chọn (Studio).",
            $"Đã tạo plan (revision {plan.Revision}): {plan.Title} — {plan.TotalQuestions} câu hỏi.",
            plan.Id,
            plan.Revision,
            ct);

        // SCRUM-445: chưa trừ lượt lúc lập plan — trừ khi sinh câu hỏi / JD-fit thành công.
        return new PlanSummaryDto(plan.Id, plan.Revision, plan.Title, plan.Status, plan.TotalQuestions);
    }

    /// <summary>
    /// SCRUM-422/423: Khi tạo plan lần đầu mà settings thiếu distribution/focus hợp lệ,
    /// gọi RAG recommend và seed distribution/focus/styles.
    /// Khung HR đã chọn (số câu / thời lượng / độ khó) được giữ — không ghi đè bằng AI (~10 câu).
    /// </summary>
    private async Task<StudioSettings?> EnsureAiConfigSeededIfNeededAsync(
        Guid projectId,
        Guid ownerId,
        JobDescription jd,
        StudioSettings? settingsSnapshot,
        IReadOnlyList<Guid> documentIds,
        CancellationToken ct)
    {
        var existingDist = settingsSnapshot is not null
            ? StudioAiConfigurationHelper.ParseQuestionDistribution(settingsSnapshot.QuestionDistributionJson)
            : [];
        var existingFocus = settingsSnapshot?.FocusAreas?.Where(f => f.IsActive).ToList() ?? [];
        var needsSeed = existingDist.Count == 0 || existingFocus.Count == 0;
        if (!needsSeed)
            return settingsSnapshot;

        string[] skills = [];
        if (!string.IsNullOrWhiteSpace(jd.DetectedSkillsJson))
        {
            try { skills = JsonSerializer.Deserialize<string[]>(jd.DetectedSkillsJson) ?? []; }
            catch { skills = []; }
        }

        var jobProfile = StudioAiConfigurationHelper.BuildJobProfile(
            jd.Title, jd.DetectedRole, jd.DetectedSeniority, jd.DetectedLanguage, skills, jd.ExtractedInformationJson);

        // HR đã chọn số câu trên cột phải → gửi hint; không thì để RAG đề xuất
        int? hrQuestionHint = settingsSnapshot?.NumberOfQuestions is > 0
            ? Math.Clamp(settingsSnapshot.NumberOfQuestions, 5, 50)
            : null;

        RecommendInterviewConfigurationResult ragResult;
        try
        {
            ragResult = await ragService.RecommendInterviewConfigurationAsync(new RecommendInterviewConfigurationRequest
            {
                OwnerId = ownerId,
                JobDescription = jd.Content,
                JobProfile = StudioAiConfigurationHelper.ToRagJobProfile(jobProfile),
                DocumentIds = documentIds.ToList(),
                NumberOfQuestions = hrQuestionHint
            }, ct);
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException(
                "RECOMMEND_CONFIGURATION_FAILED",
                StatusCodes.Status502BadGateway,
                $"Không đề xuất cấu hình trước khi tạo plan: {ex.Message}");
        }

        if (!ragResult.Success || ragResult.RecommendedConfiguration is null)
        {
            throw new StudioBusinessException(
                "RECOMMEND_CONFIGURATION_FAILED",
                StatusCodes.Status502BadGateway,
                ragResult.Detail ?? ragResult.Error ?? "Đề xuất cấu hình phỏng vấn thất bại.");
        }

        var recommended = StudioAiConfigurationHelper.MapRecommendedConfiguration(ragResult.RecommendedConfiguration.Value);
        var aiQuestionCount = Math.Clamp(recommended.NumberOfQuestions > 0 ? recommended.NumberOfQuestions : 10, 5, 50);
        var aiDifficulty = ParseQuestionDifficultyFromRecommend(recommended.Difficulty);
        // Ưu tiên khung HR đã lưu (cột phải) — không để recommend kéo 30 → 10
        var keepQuestions = hrQuestionHint ?? aiQuestionCount;
        var keepDifficulty = settingsSnapshot is not null ? settingsSnapshot.Difficulty : aiDifficulty;
        var keepMinutes = settingsSnapshot?.InterviewLengthMinutes is > 0
            ? Math.Clamp(settingsSnapshot.InterviewLengthMinutes, 15, 180)
            : Math.Clamp(keepQuestions * 4, 30, 120);
        var scaledDist = StudioAiConfigurationHelper.ScaleDistributionToTotal(
            recommended.QuestionDistribution, keepQuestions);
        if (scaledDist.Count == 0)
        {
            var (derived, _) = StudioQuestionTaxonomyMapper.FromLegacyQuestionTypes(
                StudioQuestionTypesHelper.DefaultTypes, keepQuestions);
            scaledDist = derived;
        }

        var settings = await dbContext.StudioSettings.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (settings is null)
        {
            settings = new StudioSettings
            {
                ProjectId = projectId,
                AppliedPlanId = null,
                NumberOfQuestions = keepQuestions,
                InterviewLengthMinutes = keepMinutes,
                Difficulty = keepDifficulty
            };
            dbContext.StudioSettings.Add(settings);
        }

        // Chỉ seed distribution/focus/styles; khung Time/Số câu/Độ khó giữ theo HR nếu đã có
        settings.NumberOfQuestions = keepQuestions;
        settings.Difficulty = keepDifficulty;
        settings.InterviewLengthMinutes = keepMinutes;
        settings.AiRecommendationJson = StudioAiConfigurationHelper.SerializeRecommendedConfiguration(recommended);
        settings.AiRecommendationGeneratedAt = DateTime.UtcNow;
        settings.QuestionDistributionJson = StudioAiConfigurationHelper.SerializeQuestionDistribution(scaledDist);
        settings.QuestionStylesJson = StudioAiConfigurationHelper.SerializeQuestionStyles(recommended.QuestionStyles);
        settings.QuestionTypesJson = StudioQuestionTypesHelper.ToJson(
            StudioQuestionTaxonomyMapper.ToLegacyQuestionTypes(scaledDist));
        if (recommended.CodingTasksRecommended && recommended.CodingTaskTypes.Count > 0)
        {
            settings.CodeTemplatesJson = JsonSerializer.Serialize(
                recommended.CodingTaskTypes
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                JsonOptions);
        }
        settings.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        if (recommended.FocusAreas.Count > 0)
        {
            dbContext.ChangeTracker.Clear();
            await StudioFocusAreaReplacementHelper.ReplaceViaDbSetAsync(
                dbContext, settings.Id, recommended.FocusAreas, ct);
        }

        return await dbContext.StudioSettings.AsNoTracking()
            .Include(s => s.FocusAreas.Where(f => f.IsActive))
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
    }

    /// <summary>SCRUM-423: map difficulty string từ RAG recommend → enum Studio.</summary>
    private static QuestionDifficulty ParseQuestionDifficultyFromRecommend(string? difficulty)
        => StudioQuestionTaxonomyMapper.NormalizeDifficulty(difficulty) switch
        {
            "easy" => QuestionDifficulty.Easy,
            "hard" => QuestionDifficulty.Hard,
            _ => QuestionDifficulty.Medium
        };

    private static List<string> ParseCodeTemplatesList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return (JsonSerializer.Deserialize<List<string>>(json) ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static List<string> ParseSkillsJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<PlanRefineResultDto> RefineAsync(Guid projectId, Guid planId, Guid userId, string instruction, CancellationToken ct)
    {
        // SCRUM-368 / SCRUM-388: refine qua RAG + sync Studio settings từ intent
        PlanChatScopeGuard.EnsurePlanRelated(instruction);

        var draftKey = planId.ToString("N");
        await subscriptionGate.CheckPlanRegenerateAsync(userId, draftKey);

        var project = await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var source = await dbContext.InterviewPlans.FirstOrDefaultAsync(x => x.Id == planId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("PLAN_NOT_FOUND", StatusCodes.Status404NotFound, "Không tìm thấy plan.");

        if (source.Status is InterviewPlanStatus.Approved or InterviewPlanStatus.Superseded)
            throw new StudioBusinessException("PLAN_STATUS_INVALID", StatusCodes.Status422UnprocessableEntity,
                "Plan đã chốt/superseded — không refine được. Tạo plan mới hoặc dùng revision đang chờ duyệt.");

        var jd = await dbContext.StudioJobDescriptions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("JD_REQUIRED", StatusCodes.Status422UnprocessableEntity, "Cần Job Description trước khi refine plan.");
        if (string.IsNullOrWhiteSpace(jd.Content))
            throw new StudioBusinessException("JD_EMPTY", StatusCodes.Status422UnprocessableEntity, "Job Description đang trống.");
        if (string.IsNullOrWhiteSpace(jd.Title))
            throw new StudioBusinessException(
                "POSITION_REQUIRED",
                StatusCodes.Status422UnprocessableEntity,
                "Cần xác nhận vị trí trước khi refine plan.");
        var refineSeniority = ApplicationLayer.Studio.Helpers.StudioJdSeniority.NormalizeDisplay(jd.DetectedSeniority);
        if (refineSeniority is null)
            throw new StudioBusinessException(
                "SENIORITY_REQUIRED",
                StatusCodes.Status422UnprocessableEntity,
                "Cần xác nhận cấp độ trước khi refine plan.");
        var refineExperienceLevel = ApplicationLayer.Studio.Helpers.StudioJdSeniority.ToRagExperienceLevel(refineSeniority);

        var selectedDocs = await dbContext.StudioKnowledgeDocuments.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.IsActive && x.IsSelected
                        && x.ProcessingStatus == DocumentProcessingStatus.Completed
                        && x.KnowledgeDocumentId != null)
            .Select(x => new { x.KnowledgeDocumentId, x.FileName })
            .ToListAsync(ct);
        var documentIds = selectedDocs.Select(x => x.KnowledgeDocumentId!.Value).Distinct().ToList();
        var selectedDocNames = selectedDocs.Select(x => x.FileName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();

        var sectionRows = await dbContext.PlanSections
            .Where(x => x.InterviewPlanId == source.Id && x.IsActive)
            .OrderBy(x => x.OrderIndex)
            .Select(x => new { x.Name, x.NumberOfQuestions })
            .ToListAsync(ct);
        var sectionTuples = sectionRows.Select(x => (x.Name, x.NumberOfQuestions)).ToList();

        var settingsSnap = await dbContext.StudioSettings.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        var baselineQuestions = settingsSnap?.NumberOfQuestions > 0
            ? settingsSnap.NumberOfQuestions
            : Math.Max(1, source.TotalQuestions);

        var intent = StudioRefineInstructionParser.ParseIntent(instruction);
        var numberOfQuestions = StudioRefineInstructionParser.ResolveNumberOfQuestions(baselineQuestions, intent);
        if (intent.QuestionDelta > 0 && baselineQuestions >= 50)
            throw new StudioBusinessException("QUESTION_COUNT_MAX", StatusCodes.Status400BadRequest,
                "Số câu đã đạt tối đa 50 — không thể thêm nữa.");
        var difficultyEnum = intent.Difficulty ?? settingsSnap?.Difficulty ?? source.Difficulty;
        var difficulty = difficultyEnum.ToString().ToLowerInvariant();
        var skills = ParseSkillsJson(jd.DetectedSkillsJson);
        var baselineTypes = StudioQuestionTypesHelper.ParseOrDefault(settingsSnap?.QuestionTypesJson);
        var questionTypes = StudioRefineInstructionParser.ResolveQuestionTypes(intent, baselineTypes);
        var outputLanguage = StudioOutputLanguage.Normalize(
            intent.Language ?? settingsSnap?.Language ?? source.Language);

        var preferredMinutes = settingsSnap?.InterviewLengthMinutes > 0
            ? settingsSnap.InterviewLengthMinutes
            : source.InterviewLengthMinutes;
        if (intent.InterviewLengthMinutes is int explicitMinutes)
            preferredMinutes = explicitMinutes;
        else if (numberOfQuestions != baselineQuestions && preferredMinutes > 0 && baselineQuestions > 0)
            preferredMinutes = Math.Clamp((int)Math.Round(preferredMinutes * (numberOfQuestions / (double)baselineQuestions)), 20, 180);
        preferredMinutes = Math.Clamp(preferredMinutes <= 0 ? Math.Clamp(numberOfQuestions * 3, 20, 180) : preferredMinutes, 15, 180);

        // SCRUM-389 fast-path: settings-only → patch local, không RAG
        if (StudioRefineInstructionParser.CanUseLocalSettingsPatch(intent, instruction))
        {
            if (string.IsNullOrWhiteSpace(source.SourcePlanJson))
                throw new StudioBusinessException("PLAN_SOURCE_JSON_MISSING", StatusCodes.Status422UnprocessableEntity,
                    "Plan không có SourcePlanJson — cần lập plan bằng RAG trước khi chỉnh settings nhanh.");

            var localMapped = await BuildLocalPatchedMappedPlanAsync(
                source, numberOfQuestions, difficultyEnum, preferredMinutes, questionTypes, ct);
            return await UpdatePlanInPlaceAsync(
                project, source, userId, instruction, intent, localMapped, questionTypes, outputLanguage,
                usedLocalPatch: true, draftKey, ct);
        }

        if (string.IsNullOrWhiteSpace(source.SourcePlanJson))
            throw new StudioBusinessException("PLAN_SOURCE_JSON_MISSING", StatusCodes.Status422UnprocessableEntity,
                "Plan không có SourcePlanJson — cần lập plan bằng RAG trước khi refine.");

        var baselineJson = source.SourcePlanJson;
        var hrNote = StudioRagRefineHrNoteBuilder.Build(
            instruction, source, sectionTuples, numberOfQuestions, difficulty, questionTypes,
            outputLanguage, selectedDocNames, intent.FocusHints, intent.ExclusiveFocus);
        hrNote = AppendConfirmedJdMetadata(hrNote, jd.Title, jd.DetectedRole, refineSeniority);

        RefinePlanResult ragPatch;
        try
        {
            ragPatch = await ragService.RefinePlanAsync(new RefinePlanRequest
            {
                OwnerId = userId,
                JobDescription = jd.Content,
                BaselinePlan = System.Text.Json.Nodes.JsonNode.Parse(baselineJson)!,
                NumberOfQuestions = numberOfQuestions,
                Difficulty = difficulty,
                QuestionTypes = questionTypes,
                Skills = skills,
                Language = outputLanguage,
                DocumentIds = documentIds,
                ExperienceLevel = refineExperienceLevel,
                HrNote = hrNote
            }, ct);
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException("RAG_REFINE_PLAN_FAILED", StatusCodes.Status502BadGateway,
                $"RAG refine plan thất bại: {ex.Message}");
        }

        StudioRagPlanMapper.MappedPlan mapped;
        try
        {
            mapped = PlanMergeService.Merge(
                baselineJson, ragPatch.Patch, numberOfQuestions, preferredMinutes);
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException("RAG_PLAN_MERGE_FAILED", StatusCodes.Status502BadGateway,
                $"Không merge được plan: {ex.Message}");
        }

        var resolvedMinutes = StudioRefineInstructionParser.ResolveInterviewMinutes(
            intent, mapped.InterviewLengthMinutes, numberOfQuestions, preferredMinutes);
        mapped = mapped with { InterviewLengthMinutes = resolvedMinutes };
        if (intent.Difficulty is not null)
            mapped = mapped with { Difficulty = intent.Difficulty.Value };

        if (intent.ExclusiveFocus && intent.FocusHints.Count > 0)
        {
            if (StudioExclusiveCoverageFilter.HasDisallowedFocus(mapped, intent.FocusHints))
            {
                try
                {
                    var retryNote = hrNote + "\nVALIDATION_RETRY: drop non-allowed coverage; ONLY ALLOWED_TOPICS.";
                    var retry = await ragService.RefinePlanAsync(new RefinePlanRequest
                    {
                        OwnerId = userId,
                        JobDescription = jd.Content,
                        BaselinePlan = System.Text.Json.Nodes.JsonNode.Parse(baselineJson)!,
                        NumberOfQuestions = numberOfQuestions,
                        Difficulty = difficulty,
                        QuestionTypes = questionTypes,
                        Skills = skills,
                        Language = outputLanguage,
                        DocumentIds = documentIds,
                        ExperienceLevel = refineExperienceLevel,
                        HrNote = retryNote.Length <= 2000 ? retryNote : retryNote[..2000]
                    }, ct);
                    mapped = PlanMergeService.Merge(
                        baselineJson, retry.Patch, numberOfQuestions, preferredMinutes);
                    mapped = mapped with { InterviewLengthMinutes = resolvedMinutes };
                    if (intent.Difficulty is not null)
                        mapped = mapped with { Difficulty = intent.Difficulty.Value };
                }
                catch
                {
                    // Fallback filter local
                }
            }
            mapped = StudioExclusiveCoverageFilter.Filter(
                mapped, intent.FocusHints, numberOfQuestions, resolvedMinutes, difficultyEnum);
        }

        return await UpdatePlanInPlaceAsync(
            project, source, userId, instruction, intent, mapped, questionTypes, outputLanguage,
            usedLocalPatch: false, draftKey, ct);
    }

    private static string AppendConfirmedJdMetadata(
        string hrNote, string? position, string? role, string? seniority)
    {
        var sb = new System.Text.StringBuilder(hrNote.TrimEnd());
        if (!string.IsNullOrWhiteSpace(position))
            sb.AppendLine().Append("Vị trí mục tiêu: ").Append(position.Trim());
        if (!string.IsNullOrWhiteSpace(role))
            sb.AppendLine().Append("Vai trò: ").Append(role.Trim());
        if (!string.IsNullOrWhiteSpace(seniority))
            sb.AppendLine().Append("Cấp độ bắt buộc (HR đã xác nhận): ").Append(seniority.Trim())
                .AppendLine()
                .Append("experience_level BẮT BUỘC = ").Append(seniority.Trim().ToLowerInvariant())
                .Append(" (không đổi).");
        var text = sb.ToString().Trim();
        return text.Length <= 2000 ? text : text[..2000];
    }

    private async Task<StudioRagPlanMapper.MappedPlan> BuildLocalPatchedMappedPlanAsync(
        InterviewPlan source,
        int numberOfQuestions,
        QuestionDifficulty difficulty,
        int minutes,
        IReadOnlyList<string> questionTypes,
        CancellationToken ct)
    {
        var sectionRows = await dbContext.PlanSections
            .Where(x => x.InterviewPlanId == source.Id && x.IsActive)
            .OrderBy(x => x.OrderIndex)
            .Select(x => new { x.Name, x.Description, x.OrderIndex, x.NumberOfQuestions, x.Difficulty, x.EstimatedMinutes })
            .ToListAsync(ct);
        var focusRows = await dbContext.PlanFocusAreas
            .Where(x => x.InterviewPlanId == source.Id && x.IsActive)
            .OrderBy(x => x.OrderIndex)
            .Select(x => new { x.Name, x.Weight, x.OrderIndex })
            .ToListAsync(ct);
        try
        {
            return StudioPlanSettingsPatcher.Apply(
                source,
                sectionRows.Select(s => new StudioPlanSettingsPatcher.SectionInput(
                    s.Name, s.Description, s.OrderIndex, s.NumberOfQuestions, s.Difficulty, s.EstimatedMinutes)).ToList(),
                focusRows.Select(f => new StudioPlanSettingsPatcher.FocusInput(f.Name, f.Weight, f.OrderIndex)).ToList(),
                numberOfQuestions,
                difficulty,
                minutes,
                questionTypes);
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException("PLAN_SETTINGS_PATCH_FAILED", StatusCodes.Status422UnprocessableEntity,
                $"Không áp dụng settings vào plan: {ex.Message}");
        }
    }

    /// <summary>SCRUM-420: UPDATE cùng planId — Revision++, không Superseded row mới.</summary>
    private async Task<PlanRefineResultDto> UpdatePlanInPlaceAsync(
        InterviewProject project,
        InterviewPlan plan,
        Guid userId,
        string instruction,
        StudioRefineInstructionParser.StudioChatSettingsIntent intent,
        StudioRagPlanMapper.MappedPlan mapped,
        IReadOnlyList<string> questionTypes,
        string outputLanguage,
        bool usedLocalPatch,
        string draftKey,
        CancellationToken ct)
    {
        var revision = await AllocateNextPlanRevisionAsync(project, ct);

        var oldSections = await dbContext.PlanSections
            .Where(x => x.InterviewPlanId == plan.Id && x.IsActive)
            .ToListAsync(ct);
        foreach (var s in oldSections)
        {
            s.IsActive = false;
            s.UpdatedAt = DateTime.UtcNow;
        }

        var oldFocus = await dbContext.PlanFocusAreas
            .Where(x => x.InterviewPlanId == plan.Id && x.IsActive)
            .ToListAsync(ct);
        foreach (var f in oldFocus)
        {
            f.IsActive = false;
            f.UpdatedAt = DateTime.UtcNow;
        }

        if (plan.SessionId == Guid.Empty)
            plan.SessionId = await EnsureProjectChatSessionIdAsync(project.Id, userId, ct);
        else if (!await dbContext.AiChatSessions.AnyAsync(x => x.Id == plan.SessionId && x.IsActive, ct))
            plan.SessionId = await EnsureProjectChatSessionIdAsync(project.Id, userId, ct);

        plan.Revision = revision;
        plan.Status = InterviewPlanStatus.AwaitingApproval;
        plan.Title = mapped.Title;
        plan.TotalQuestions = mapped.TotalQuestions;
        plan.InterviewLengthMinutes = mapped.InterviewLengthMinutes;
        plan.Difficulty = mapped.Difficulty;
        // Tone/format: không còn cập nhật từ chat refine
        plan.Language = outputLanguage;
        plan.SeniorityLevel = mapped.SeniorityLevel;
        plan.IncludeSampleAnswers = intent.IncludeSampleAnswers ?? plan.IncludeSampleAnswers;
        plan.IncludeScoringRubric = intent.IncludeScoringRubric ?? plan.IncludeScoringRubric;
        plan.GeneratedByModelName = usedLocalPatch ? "StudioSettingsPatch" : "RAG-RefinePatch";
        plan.SourcePlanJson = mapped.SourcePlanJson;
        plan.ConcurrencyVersion = Guid.NewGuid();
        plan.UpdatedAt = DateTime.UtcNow;

        foreach (var s in mapped.Sections)
        {
            dbContext.PlanSections.Add(new PlanSection
            {
                InterviewPlanId = plan.Id,
                Name = s.Name,
                Description = s.Description,
                OrderIndex = s.OrderIndex,
                NumberOfQuestions = s.NumberOfQuestions,
                Difficulty = s.Difficulty,
                EstimatedMinutes = s.EstimatedMinutes
            });
        }
        foreach (var f in mapped.FocusAreas)
        {
            dbContext.PlanFocusAreas.Add(new PlanFocusArea
            {
                InterviewPlanId = plan.Id,
                Name = f.Name,
                Weight = f.Weight,
                OrderIndex = f.OrderIndex
            });
        }

        project.LatestPlanRevision = revision;
        project.Status = InterviewProjectStatus.AwaitingApproval;
        await SyncStudioSettingsFromPlanAsync(project.Id, mapped, questionTypes, ct, intent);
        await dbContext.SaveChangesAsync(ct);

        var citationFiles = StudioRagPlanMapper.ExtractCitationSourceFiles(mapped.SourcePlanJson).Take(8).ToList();
        var settingsDto = await BuildSettingsDtoAsync(project.Id, ct);
        var changedFields = StudioChatRefineMessageBuilder.DescribeChanges(
            intent,
            settingsDto.NumberOfQuestions,
            settingsDto.InterviewLengthMinutes,
            settingsDto.Difficulty,
            settingsDto.QuestionTypes,
            settingsDto.Language,
            settingsDto.QuestionTone,
            settingsDto.OutputFormat,
            settingsDto.IncludeSampleAnswers,
            settingsDto.IncludeScoringRubric);
        var assistantMessage = StudioChatRefineMessageBuilder.Build(
            plan.Revision, plan.Title, plan.TotalQuestions, changedFields, citationFiles,
            intent.FocusHints, intent.ExclusiveFocus, usedLocalPatch);

        await aiChatService.AppendUserAndAssistantAsync(
            project.Id,
            userId,
            instruction.Trim(),
            assistantMessage,
            plan.Id,
            plan.Revision,
            ct);

        await usageMetering.IncrementAsync(userId, DomainLayer.Constants.UsageType.HrPlanRegenerate, draftKey);
        return new PlanRefineResultDto(
            plan.Id, plan.Revision, plan.Title, plan.Status, plan.TotalQuestions,
            settingsDto, changedFields, citationFiles, assistantMessage);
    }

    /// <summary>
    /// Bước 1: Focus/phân bổ/styles → patch local cập nhật plan (không gọi RAG generate-plan).
    /// Bước 2: có OutlineItems → patch outline Live Preview.
    /// Focus HR được giữ nguyên (không bị RAG ghi đè).
    /// </summary>
    public async Task<PlanSummaryDto> ApplySettingsAsync(Guid projectId, Guid planId, Guid userId, ApplyPlanSettingsRequest request, CancellationToken ct)
    {
        StudioQuestionTypesHelper.EnsureValidOrThrow(request.QuestionTypes);
        var types = StudioQuestionTypesHelper.Normalize(request.QuestionTypes);
        var effectiveQuestionCount = request.OutlineItems is { Count: > 0 }
            ? request.OutlineItems.Count
            : request.NumberOfQuestions;
        if (effectiveQuestionCount is < 5 or > 50)
            throw new StudioBusinessException("INVALID_QUESTION_COUNT", StatusCodes.Status400BadRequest, "Số câu phải từ 5–50.");
        if (request.InterviewLengthMinutes is < 15 or > 180)
            throw new StudioBusinessException("INVALID_INTERVIEW_MINUTES", StatusCodes.Status400BadRequest, "Thời lượng phải từ 15–180 phút.");

        var project = await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var source = await dbContext.InterviewPlans.FirstOrDefaultAsync(x => x.Id == planId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("PLAN_NOT_FOUND", StatusCodes.Status404NotFound, "Không tìm thấy plan.");
        if (source.Status is InterviewPlanStatus.Approved or InterviewPlanStatus.Superseded)
            throw new StudioBusinessException("PLAN_STATUS_INVALID", StatusCodes.Status422UnprocessableEntity,
                "Plan đã chốt/superseded — không áp dụng settings. Tạo plan mới hoặc dùng revision đang chờ duyệt.");
        if (string.IsNullOrWhiteSpace(source.SourcePlanJson))
            throw new StudioBusinessException("PLAN_SOURCE_JSON_MISSING", StatusCodes.Status422UnprocessableEntity,
                "Plan không có SourcePlanJson — cần lập plan bằng RAG trước.");

        var settings = await dbContext.StudioSettings
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (settings is null)
        {
            settings = new StudioSettings { ProjectId = projectId, AppliedPlanId = null };
            dbContext.StudioSettings.Add(settings);
        }

        IReadOnlyList<QuestionDistributionItemDto> canonicalDistribution;
        if (request.QuestionDistribution is { Count: > 0 })
        {
            canonicalDistribution = request.QuestionDistribution.ToList();
            types = StudioQuestionTaxonomyMapper.ToLegacyQuestionTypes(canonicalDistribution).ToList();
            settings.QuestionDistributionJson = JsonSerializer.Serialize(canonicalDistribution, JsonOptions);
        }
        else
        {
            canonicalDistribution = StudioAiConfigurationHelper.ParseQuestionDistribution(settings.QuestionDistributionJson);
            if (canonicalDistribution.Count == 0)
            {
                var (derived, _) = StudioQuestionTaxonomyMapper.FromLegacyQuestionTypes(types, request.NumberOfQuestions);
                canonicalDistribution = derived;
                settings.QuestionDistributionJson = JsonSerializer.Serialize(derived, JsonOptions);
            }
        }

        if (request.QuestionStyles is { Count: > 0 })
        {
            var normalizedStyles = request.QuestionStyles
                .Select(StudioQuestionTaxonomyMapper.NormalizeStyle)
                .Where(s => s is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();
            settings.QuestionStylesJson = JsonSerializer.Serialize(normalizedStyles, JsonOptions);
        }

        if (request.CodingTaskTypes is { Count: > 0 })
        {
            var coding = request.CodingTaskTypes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            settings.CodeTemplatesJson = JsonSerializer.Serialize(coding, JsonOptions);
        }

        settings.NumberOfQuestions = request.OutlineItems is { Count: > 0 }
            ? request.OutlineItems.Count
            : request.NumberOfQuestions;
        settings.Difficulty = request.Difficulty;
        settings.InterviewLengthMinutes = request.InterviewLengthMinutes;
        settings.QuestionTypesJson = StudioQuestionTypesHelper.ToJson(types);
        settings.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        if (request.FocusAreas is { Count: > 0 })
        {
            dbContext.ChangeTracker.Clear();
            await StudioFocusAreaReplacementHelper.ReplaceViaDbSetAsync(
                dbContext, settings.Id, request.FocusAreas, ct);
        }

        settings = await dbContext.StudioSettings
            .AsNoTracking()
            .Include(s => s.FocusAreas.Where(f => f.IsActive))
            .FirstAsync(x => x.Id == settings.Id, ct);
        var settingsSnapshot = StudioPlanSettingsSnapshotHelper.BuildFrom(settings);

        var sectionRows = await dbContext.PlanSections
            .Where(x => x.InterviewPlanId == source.Id && x.IsActive)
            .OrderBy(x => x.OrderIndex)
            .Select(x => new { x.Name, x.Description, x.OrderIndex, x.NumberOfQuestions, x.Difficulty, x.EstimatedMinutes })
            .ToListAsync(ct);

        IReadOnlyList<StudioPlanSettingsPatcher.FocusInput> focusForPatch;
        if (request.FocusAreas is { Count: > 0 })
        {
            focusForPatch = request.FocusAreas
                .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                .OrderBy(f => f.OrderIndex)
                .Select((f, i) => new StudioPlanSettingsPatcher.FocusInput(
                    f.Name.Trim(),
                    StudioFocusAreaWeightHelper.NormalizeToPercent(f.Weight),
                    f.OrderIndex > 0 ? f.OrderIndex : i))
                .ToList();
        }
        else
        {
            var focusRows = await dbContext.PlanFocusAreas
                .Where(x => x.InterviewPlanId == source.Id && x.IsActive)
                .OrderBy(x => x.OrderIndex)
                .Select(x => new { x.Name, x.Weight, x.OrderIndex })
                .ToListAsync(ct);
            focusForPatch = focusRows
                .Select(f => new StudioPlanSettingsPatcher.FocusInput(f.Name, f.Weight, f.OrderIndex))
                .ToList();
            if (focusForPatch.Count == 0 && settings.FocusAreas.Count > 0)
            {
                focusForPatch = settings.FocusAreas
                    .Where(f => f.IsActive && !string.IsNullOrWhiteSpace(f.Name))
                    .OrderBy(f => f.OrderIndex)
                    .Select(f => new StudioPlanSettingsPatcher.FocusInput(f.Name, f.Weight, f.OrderIndex))
                    .ToList();
            }
        }

        var questionCount = request.OutlineItems is { Count: > 0 }
            ? request.OutlineItems.Count
            : request.NumberOfQuestions;

        StudioRagPlanMapper.MappedPlan mapped;
        try
        {
            mapped = StudioPlanSettingsPatcher.Apply(
                source,
                sectionRows.Select(s => new StudioPlanSettingsPatcher.SectionInput(
                    s.Name, s.Description, s.OrderIndex, s.NumberOfQuestions, s.Difficulty, s.EstimatedMinutes)).ToList(),
                focusForPatch,
                questionCount,
                request.Difficulty,
                request.InterviewLengthMinutes,
                types,
                canonicalDistribution,
                request.OutlineItems,
                string.IsNullOrWhiteSpace(settings.ContentMode) ? "Mixed" : settings.ContentMode.Trim());
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException("PLAN_SETTINGS_PATCH_FAILED", StatusCodes.Status422UnprocessableEntity,
                $"Không cập nhật plan theo settings: {ex.Message}");
        }

        // Giữ Focus HR — không để mapper/coverage ghi đè
        if (focusForPatch.Count > 0)
        {
            var hrFocusDrafts = focusForPatch
                .OrderBy(f => f.OrderIndex)
                .Select((f, i) => new StudioRagPlanMapper.PlanFocusAreaDraft(
                    f.Name,
                    StudioFocusAreaWeightHelper.NormalizeToPercent(f.Weight),
                    f.OrderIndex > 0 ? f.OrderIndex : i,
                    Array.Empty<string>()))
                .ToList();
            mapped = mapped with { FocusAreas = hrFocusDrafts };
        }

        mapped = mapped with
        {
            SourcePlanJson = StudioPlanSettingsSnapshotHelper.EmbedInSourcePlanJson(
                mapped.SourcePlanJson,
                settingsSnapshot)
        };

        // SCRUM-426: rebind citations nếu slot thiếu JD lock (vd. HR đổi skill trên Live Preview)
        if (request.OutlineItems is { Count: > 0 })
        {
            mapped = await TryRebindOutlineSourcesAsync(
                projectId, userId, mapped, request.OutlineItems, ct);
        }

        if (source.Status is InterviewPlanStatus.AwaitingApproval or InterviewPlanStatus.Draft or InterviewPlanStatus.Rejected)
        {
            source.Status = InterviewPlanStatus.Superseded;
            source.UpdatedAt = DateTime.UtcNow;
        }

        var revision = await AllocateNextPlanRevisionAsync(project, ct);
        var sessionId = source.SessionId != Guid.Empty
            ? source.SessionId
            : await EnsureProjectChatSessionIdAsync(projectId, userId, ct);
        if (source.SessionId != Guid.Empty
            && !await dbContext.AiChatSessions.AnyAsync(x => x.Id == source.SessionId && x.IsActive, ct))
        {
            sessionId = await EnsureProjectChatSessionIdAsync(projectId, userId, ct);
        }

        var patched = new InterviewPlan
        {
            ProjectId = projectId,
            SessionId = sessionId,
            Revision = revision,
            Status = InterviewPlanStatus.AwaitingApproval,
            Title = mapped.Title,
            TotalQuestions = mapped.TotalQuestions,
            InterviewLengthMinutes = mapped.InterviewLengthMinutes,
            Difficulty = mapped.Difficulty,
            QuestionTone = source.QuestionTone,
            Language = StudioOutputLanguage.Normalize(settings.Language ?? source.Language),
            SeniorityLevel = string.IsNullOrWhiteSpace(source.SeniorityLevel) ? mapped.SeniorityLevel : source.SeniorityLevel,
            IncludeSampleAnswers = source.IncludeSampleAnswers,
            IncludeScoringRubric = source.IncludeScoringRubric,
            OutputFormat = source.OutputFormat,
            GeneratedByModelName = "StudioSettingsPatch",
            SourcePlanJson = mapped.SourcePlanJson
        };
        dbContext.InterviewPlans.Add(patched);
        await dbContext.SaveChangesAsync(ct);

        foreach (var s in mapped.Sections)
        {
            dbContext.PlanSections.Add(new PlanSection
            {
                InterviewPlanId = patched.Id,
                Name = s.Name,
                Description = s.Description,
                OrderIndex = s.OrderIndex,
                NumberOfQuestions = s.NumberOfQuestions,
                Difficulty = s.Difficulty,
                EstimatedMinutes = s.EstimatedMinutes
            });
        }
        foreach (var f in mapped.FocusAreas)
        {
            dbContext.PlanFocusAreas.Add(new PlanFocusArea
            {
                InterviewPlanId = patched.Id,
                Name = f.Name,
                Weight = f.Weight,
                OrderIndex = f.OrderIndex
            });
        }

        project.LatestPlanRevision = revision;
        project.Status = InterviewProjectStatus.AwaitingApproval;
        await SyncStudioSettingsFromPlanAsync(projectId, mapped, types, ct);
        await dbContext.SaveChangesAsync(ct);

        var summary = request.OutlineItems is { Count: > 0 }
            ? $"Đã cập nhật outline preview (revision {patched.Revision}): {patched.TotalQuestions} slot."
            : $"Đã cập nhật plan theo cấu hình HR (revision {patched.Revision}): {patched.Title} — {patched.TotalQuestions} câu (Focus/phân bổ/styles giữ theo HR).";

        await aiChatService.AppendUserAndAssistantAsync(
            projectId,
            userId,
            $"Áp dụng settings: {request.NumberOfQuestions}q, {request.Difficulty}, {request.InterviewLengthMinutes} phút, types=[{string.Join(", ", types)}]",
            summary,
            patched.Id,
            patched.Revision,
            ct);

        return new PlanSummaryDto(patched.Id, patched.Revision, patched.Title, patched.Status, patched.TotalQuestions);
    }

    /// <summary>SCRUM-426: gọi RAG bind-outline-sources khi slot thiếu citations.</summary>
    private async Task<StudioRagPlanMapper.MappedPlan> TryRebindOutlineSourcesAsync(
        Guid projectId,
        Guid userId,
        StudioRagPlanMapper.MappedPlan mapped,
        IReadOnlyList<PlanOutlineItemDto> outlineItems,
        CancellationToken ct)
    {
        var needsBind = outlineItems.Any(o =>
            o.Citations is null
            || o.Citations.Count == 0
            || !o.Citations.Any(c =>
                !string.IsNullOrWhiteSpace(c.Excerpt)
                && (c.SourceFile?.Contains("job-description", StringComparison.OrdinalIgnoreCase) == true
                    || string.Equals(c.Origin, "HR", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.KnowledgeBase, "hr", StringComparison.OrdinalIgnoreCase))));
        if (!needsBind)
            return mapped;

        var jd = await dbContext.StudioJobDescriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (string.IsNullOrWhiteSpace(jd?.Content))
            return mapped;

        try
        {
            var outlinePayload = outlineItems.Select(o => (object)new
            {
                order = o.Order,
                type = o.Type,
                difficulty = o.Difficulty,
                skill = o.Skill,
                focusArea = o.FocusArea,
                goal = o.Goal,
                answerMethod = o.AnswerMethod,
                citations = o.Citations
            }).ToList();

            var bind = await ragService.BindOutlineSourcesAsync(new BindOutlineSourcesRequest
            {
                OwnerId = userId,
                JobDescription = jd.Content,
                Outline = outlinePayload,
                ForceRebind = false
            }, ct);

            if (!bind.Success || bind.Outline is null || bind.Outline.Count == 0)
                return mapped;

            var json = mapped.SourcePlanJson;
            if (string.IsNullOrWhiteSpace(json))
                return mapped;

            var root = JsonNode.Parse(json)?.AsObject();
            if (root is null)
                return mapped;

            var outlineArr = JsonSerializer.SerializeToNode(bind.Outline, JsonOptions);
            if (outlineArr is null)
                return mapped;

            root["recommendedQuestionOutline"] = outlineArr.DeepClone();
            root["recommended_question_outline"] = outlineArr.DeepClone();
            return mapped with { SourcePlanJson = root.ToJsonString(JsonOptions) };
        }
        catch
        {
            // Soft-fail: Apply vẫn thành công dù rebind lỗi
            return mapped;
        }
    }

    private async Task SyncStudioSettingsFromPlanAsync(
        Guid projectId,
        StudioRagPlanMapper.MappedPlan mapped,
        IReadOnlyList<string> questionTypes,
        CancellationToken ct,
        StudioRefineInstructionParser.StudioChatSettingsIntent? intent = null)
    {
        var settings = await dbContext.StudioSettings.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (settings is null)
        {
            settings = new StudioSettings { ProjectId = projectId, AppliedPlanId = null };
            dbContext.StudioSettings.Add(settings);
        }
        settings.NumberOfQuestions = Math.Clamp(mapped.TotalQuestions, 5, 50);
        settings.Difficulty = mapped.Difficulty;
        settings.InterviewLengthMinutes = mapped.InterviewLengthMinutes;
        settings.SeniorityLevel = mapped.SeniorityLevel;
        settings.QuestionTypesJson = StudioQuestionTypesHelper.ToJson(questionTypes);
        var typesForTaxonomy = questionTypes.Count > 0
            ? questionTypes
            : StudioRagPlanMapper.ExtractQuestionTypesFromSourcePlan(mapped.SourcePlanJson);
        var distribution = StudioRagPlanMapper.ExtractCanonicalDistributionFromSourcePlan(
            mapped.SourcePlanJson,
            settings.NumberOfQuestions);
        settings.QuestionDistributionJson = JsonSerializer.Serialize(distribution, JsonOptions);
        var styles = StudioQuestionTaxonomyMapper.ExtractStylesFromLegacyTypes(typesForTaxonomy);
        if (styles.Count > 0)
            settings.QuestionStylesJson = JsonSerializer.Serialize(styles, JsonOptions);

        if (intent?.Language is not null)
            settings.Language = StudioOutputLanguage.Normalize(intent.Language);
        if (intent?.IncludeSampleAnswers is not null)
            settings.IncludeSampleAnswers = intent.IncludeSampleAnswers.Value;
        if (intent?.IncludeScoringRubric is not null)
            settings.IncludeScoringRubric = intent.IncludeScoringRubric.Value;
        settings.UpdatedAt = DateTime.UtcNow;

        if (settings.Id == Guid.Empty)
            await dbContext.SaveChangesAsync(ct);

        if (mapped.FocusAreas.Count > 0)
        {
            var focusDtos = mapped.FocusAreas
                .Select((f, i) => new StudioFocusAreaItemDto(
                    f.Name,
                    StudioFocusAreaWeightHelper.NormalizeToPercent(f.Weight),
                    f.OrderIndex > 0 ? f.OrderIndex : i,
                    Description: null,
                    SourceReason: f.SourceFiles.Count > 0 ? string.Join(", ", f.SourceFiles.Take(3)) : null))
                .ToList();
            await StudioFocusAreaReplacementHelper.ReplaceViaDbSetAsync(dbContext, settings.Id, focusDtos, ct);
        }
    }

    private async Task<StudioSettingsDto> BuildSettingsDtoAsync(Guid projectId, CancellationToken ct)
    {
        var settings = await dbContext.StudioSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        var appliedPlanId = settings?.AppliedPlanId;
        var hasJd = await dbContext.StudioJobDescriptions.AnyAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        var hasSelectedDoc = await dbContext.StudioKnowledgeDocuments.AnyAsync(
            x => x.ProjectId == projectId && x.IsActive && x.IsSelected && x.ProcessingStatus == DocumentProcessingStatus.Completed, ct);
        var hasAwaiting = await dbContext.InterviewPlans.AnyAsync(
            x => x.ProjectId == projectId && x.IsActive && x.Status == InterviewPlanStatus.AwaitingApproval, ct);
        var hasApproved = await dbContext.InterviewPlans.AnyAsync(
            x => x.ProjectId == projectId && x.IsActive && x.Status == InterviewPlanStatus.Approved, ct);
        var canGenerate = hasApproved && appliedPlanId.HasValue && await dbContext.InterviewPlans.AnyAsync(
            x => x.Id == appliedPlanId && x.ProjectId == projectId && x.Status == InterviewPlanStatus.Approved && x.IsActive, ct);
        var readiness = new StudioReadinessDto(hasJd, hasSelectedDoc, hasAwaiting, hasApproved, canGenerate);
        if (settings is null)
        {
            return new StudioSettingsDto(
                projectId, null, 60, 15, QuestionDifficulty.Medium, "Professional",
                true, true, "StructuredInterviewKit", readiness,
                StudioQuestionTypesHelper.DefaultTypes, StudioOutputLanguage.Vietnamese,
                "Mixed", new[] { "BUG_DETECTION", "CODE_COMPLETION", "REFACTORING", "PERFORMANCE_ANALYSIS" });
        }
        return new StudioSettingsDto(
            projectId,
            settings.AppliedPlanId,
            settings.InterviewLengthMinutes,
            settings.NumberOfQuestions,
            settings.Difficulty,
            settings.QuestionTone,
            settings.IncludeSampleAnswers,
            settings.IncludeScoringRubric,
            settings.OutputFormat,
            readiness,
            StudioQuestionTypesHelper.ParseOrDefault(settings.QuestionTypesJson),
            StudioOutputLanguage.Normalize(settings.Language),
            string.IsNullOrWhiteSpace(settings.ContentMode) ? "Mixed" : settings.ContentMode,
            ParseCodeTemplatesOrDefault(settings.CodeTemplatesJson));
    }

    private static IReadOnlyList<string> ParseCodeTemplatesOrDefault(string? json)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new List<string>();
            var cleaned = items.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return cleaned.Count > 0 ? cleaned : new[] { "BUG_DETECTION", "CODE_COMPLETION", "REFACTORING", "PERFORMANCE_ANALYSIS" };
        }
        catch
        {
            return new[] { "BUG_DETECTION", "CODE_COMPLETION", "REFACTORING", "PERFORMANCE_ANALYSIS" };
        }
    }

    public async Task<PlanSummaryDto> SubmitForApprovalAsync(Guid projectId, Guid planId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var plan = await dbContext.InterviewPlans.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.Id == planId && x.IsActive, ct)
            ?? throw new StudioBusinessException("PLAN_NOT_FOUND", 404, "Không tìm thấy plan.");
        if (plan.Status == InterviewPlanStatus.Approved || plan.Status == InterviewPlanStatus.Superseded)
            throw new StudioBusinessException("PLAN_STATUS_INVALID", 422, "Plan đã chốt, không thể submit.");
        plan.Status = InterviewPlanStatus.AwaitingApproval;
        plan.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return new PlanSummaryDto(plan.Id, plan.Revision, plan.Title, plan.Status, plan.TotalQuestions);
    }

    public async Task<PlanSummaryDto> RejectAsync(Guid projectId, Guid planId, Guid userId, RejectPlanRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var plan = await dbContext.InterviewPlans.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.Id == planId && x.IsActive, ct)
            ?? throw new StudioBusinessException("PLAN_NOT_FOUND", 404, "Không tìm thấy plan.");
        if (plan.Status != InterviewPlanStatus.AwaitingApproval)
            throw new StudioBusinessException("PLAN_NOT_AWAITING_APPROVAL", 422, "Plan chưa ở trạng thái chờ duyệt.");
        plan.Status = InterviewPlanStatus.Rejected;
        plan.UpdatedAt = DateTime.UtcNow;
        dbContext.PlanApprovalHistories.Add(new PlanApprovalHistory
        {
            ProjectId = projectId,
            InterviewPlanId = plan.Id,
            Revision = plan.Revision,
            Action = PlanApprovalAction.Rejected,
            ActorId = userId,
            Notes = request.Notes
        });
        await dbContext.SaveChangesAsync(ct);
        return new PlanSummaryDto(plan.Id, plan.Revision, plan.Title, plan.Status, plan.TotalQuestions);
    }

    public async Task<PlanSummaryDto> ApproveAsync(Guid projectId, Guid planId, Guid userId, ApprovePlanRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var plan = await dbContext.InterviewPlans.FirstOrDefaultAsync(x => x.Id == planId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("PLAN_NOT_FOUND", StatusCodes.Status404NotFound, "Không tìm thấy plan.");
        if (plan.Revision != request.Revision) throw new StudioBusinessException("PLAN_REVISION_CONFLICT", 409, "Revision không khớp.");
        if (plan.ConcurrencyVersion != request.ConcurrencyVersion) throw new StudioBusinessException("PLAN_CONCURRENCY_CONFLICT", 409, "Concurrency token không khớp.");
        if (plan.Status != InterviewPlanStatus.AwaitingApproval) throw new StudioBusinessException("PLAN_NOT_AWAITING_APPROVAL", 422, "Plan chưa sẵn sàng approve.");

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var currentApproved = await dbContext.InterviewPlans.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.Status == InterviewPlanStatus.Approved && x.IsActive, ct);
            if (currentApproved is not null)
            {
                currentApproved.Status = InterviewPlanStatus.Superseded;
                currentApproved.UpdatedAt = DateTime.UtcNow;
            }

            plan.Status = InterviewPlanStatus.Approved;
            plan.ApprovedAt = DateTime.UtcNow;
            plan.ApprovedBy = userId;
            plan.UpdatedAt = DateTime.UtcNow;

            var settings = await dbContext.StudioSettings.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
            if (settings is null) { settings = new StudioSettings { ProjectId = projectId }; dbContext.StudioSettings.Add(settings); }
            settings.AppliedPlanId = plan.Id;
            settings.NumberOfQuestions = Math.Clamp(plan.TotalQuestions, 5, 50);
            settings.InterviewLengthMinutes = plan.InterviewLengthMinutes;
            settings.SeniorityLevel = plan.SeniorityLevel;
            // Ưu tiên ngôn ngữ đầu ra HR đã chọn trên Studio Settings — không reset về snapshot plan (có thể EN từ JD/RAG).
            var resolvedLanguage = StudioOutputLanguage.Normalize(
                !string.IsNullOrWhiteSpace(settings.Language) ? settings.Language : plan.Language);
            settings.Language = resolvedLanguage;
            plan.Language = resolvedLanguage;
            settings.Difficulty = plan.Difficulty;
            // Generate-output prefs (tone/sample/rubric/format) giữ từ Studio — không ghi đè từ plan
            // SCRUM-370: suy question types từ SourcePlanJson / sections
            var typesFromPlan = StudioRagPlanMapper.ExtractQuestionTypesFromSourcePlan(plan.SourcePlanJson);
            if (typesFromPlan.Count == 0)
            {
                var sectionNames = await dbContext.PlanSections
                    .Where(x => x.InterviewPlanId == plan.Id && x.IsActive)
                    .Select(x => x.Name)
                    .ToListAsync(ct);
                typesFromPlan = sectionNames
                    .Select(n => StudioRagPlanMapper.MapQuestionType(n).ToString())
                    .Select(t => t switch
                    {
                        nameof(QuestionType.SystemDesign) => "system_design",
                        nameof(QuestionType.ProblemSolving) => "problem_solving",
                        nameof(QuestionType.Behavioral) => "behavioral",
                        nameof(QuestionType.Situational) => "situational",
                        _ => "technical"
                    })
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
            settings.QuestionTypesJson = StudioQuestionTypesHelper.ToJson(
                typesFromPlan.Count > 0 ? typesFromPlan : StudioQuestionTypesHelper.ParseOrDefault(settings.QuestionTypesJson));

            dbContext.PlanApprovalHistories.Add(new PlanApprovalHistory { ProjectId = projectId, InterviewPlanId = plan.Id, Revision = plan.Revision, Action = PlanApprovalAction.Approved, ActorId = userId, Notes = request.Notes });
            var project = await dbContext.InterviewProjects.FirstAsync(x => x.Id == projectId, ct);
            project.Status = InterviewProjectStatus.Approved;

            await dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        // SCRUM-376: ghi event approve vào chat
        await aiChatService.AppendUserAndAssistantAsync(
            projectId,
            userId,
            "Approve plan.",
            $"Đã duyệt plan revision {plan.Revision}: {plan.Title} — {plan.TotalQuestions} câu hỏi. Có thể tạo câu hỏi.",
            plan.Id,
            plan.Revision,
            ct);

        return new PlanSummaryDto(plan.Id, plan.Revision, plan.Title, plan.Status, plan.TotalQuestions);
    }

    /// <summary>SCRUM-393: HR đổi tiêu đề plan (tên công việc) ngay trên PlanWorkspace.</summary>
    public async Task<PlanSummaryDto> RenameTitleAsync(
        Guid projectId, Guid planId, Guid userId, RenamePlanTitleRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var plan = await dbContext.InterviewPlans.FirstOrDefaultAsync(
                x => x.Id == planId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("PLAN_NOT_FOUND", StatusCodes.Status404NotFound, "Không tìm thấy plan.");

        if (plan.Status == InterviewPlanStatus.Superseded)
            throw new StudioBusinessException("PLAN_STATUS_INVALID", StatusCodes.Status422UnprocessableEntity,
                "Plan đã bị thay thế, không thể đổi tiêu đề.");

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new StudioBusinessException("PLAN_TITLE_EMPTY", StatusCodes.Status400BadRequest, "Tiêu đề không được để trống.");
        if (title.Length > 500)
            throw new StudioBusinessException("PLAN_TITLE_TOO_LONG", StatusCodes.Status400BadRequest,
                "Tiêu đề không được vượt quá 500 ký tự.");

        plan.Title = title;
        SyncRoleTitleInSourcePlanJson(plan, title);
        plan.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return new PlanSummaryDto(plan.Id, plan.Revision, plan.Title, plan.Status, plan.TotalQuestions);
    }

    /// <summary>Đồng bộ roleTitle trong SourcePlanJson khi HR đổi tên hiển thị (không đụng summary/coverage).</summary>
    private static void SyncRoleTitleInSourcePlanJson(InterviewPlan plan, string displayTitle)
    {
        if (string.IsNullOrWhiteSpace(plan.SourcePlanJson)) return;
        try
        {
            var node = JsonNode.Parse(plan.SourcePlanJson);
            if (node is null) return;

            var role = displayTitle;
            var colon = displayTitle.IndexOf(':');
            if (colon > 0)
                role = displayTitle[..colon].Trim();
            if (role.Length > 80)
                role = role[..80];

            JsonObject? target = node as JsonObject;
            if (node["plan"] is JsonObject nested)
                target = nested;
            if (target is null) return;

            target["roleTitle"] = role;
            target["role_title"] = role;
            plan.SourcePlanJson = node.ToJsonString(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
        }
        catch
        {
            // Không chặn rename Title nếu JSON cũ lỗi — Title DB vẫn được lưu
        }
    }

    public async Task<IReadOnlyList<PlanApprovalHistoryDto>> GetApprovalHistoryAsync(Guid projectId, Guid planId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        _ = await dbContext.InterviewPlans.FirstOrDefaultAsync(x => x.Id == planId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("PLAN_NOT_FOUND", StatusCodes.Status404NotFound, "Không tìm thấy plan.");
        return await dbContext.PlanApprovalHistories
            .Where(x => x.ProjectId == projectId && x.InterviewPlanId == planId && x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PlanApprovalHistoryDto(x.Id, x.Revision, x.Action, x.ActorId, x.CreatedAt, x.Notes))
            .ToListAsync(ct);
    }
}

public sealed class QuestionGenerationService(
    AppDbContext dbContext,
    IInterviewProjectService projectService,
    IStudioMockAiService mockAiService,
    IRagService ragService,
    IBlobStorageService blobStorage,
    ISubscriptionGateService subscriptionGate,
    IUsageMeteringService usageMetering) : IQuestionGenerationService
{
    public async Task<GenerationRunDto> GenerateAsync(Guid projectId, Guid userId, GenerateQuestionsRequest request, CancellationToken ct)
    {
        // SCRUM-371: giống luồng HR — enqueue RAG async, callback ghi InterviewQuestions
        // SCRUM-445: Free 1/24h — check trước khi sinh full bộ (trừ lượt khi callback OK).
        await subscriptionGate.CheckGenerateSetAsync(userId);
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var plan = await dbContext.InterviewPlans.FirstOrDefaultAsync(x => x.Id == request.PlanId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("PLAN_NOT_FOUND", 404, "Không tìm thấy plan.");
        if (plan.Status != InterviewPlanStatus.Approved) throw new StudioBusinessException("PLAN_NOT_APPROVED", 422, "Plan chưa được duyệt.");
        var settings = await dbContext.StudioSettings.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (settings is null || settings.AppliedPlanId != plan.Id)
            throw new StudioBusinessException("PLAN_NOT_APPLIED_TO_STUDIO", 422, "Plan chưa apply vào Studio settings.");

        var jd = await dbContext.StudioJobDescriptions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("JD_REQUIRED", 422, "Cần Job Description để generate câu hỏi.");
        if (string.IsNullOrWhiteSpace(jd.Content))
            throw new StudioBusinessException("JD_EMPTY", 422, "Job Description đang trống.");
        if (string.IsNullOrWhiteSpace(plan.SourcePlanJson))
            throw new StudioBusinessException("PLAN_SOURCE_JSON_MISSING", 422,
                "Plan không có SourcePlanJson (cần generate lại plan bằng RAG).");

        var existingActiveCount = await dbContext.InterviewQuestions.CountAsync(x => x.ProjectId == projectId && x.InterviewPlanId == plan.Id && x.IsActive, ct);
        if (existingActiveCount > 0 && !request.ReplaceExisting)
            throw new StudioBusinessException("QUESTIONS_ALREADY_EXIST", 409, "Đã có câu hỏi active cho plan này.");

        var busy = await dbContext.QuestionGenerationRuns.AnyAsync(
            x => x.ProjectId == projectId && x.InterviewPlanId == plan.Id && x.IsActive
                 && x.Status == DomainLayer.Studio.Enums.QuestionGenerationStatus.Generating, ct);
        if (busy)
            throw new StudioBusinessException("GENERATION_IN_PROGRESS", 409, "Đang có job sinh câu hỏi cho plan này.");

        object approvedPlan;
        try
        {
            approvedPlan = JsonSerializer.Deserialize<object>(plan.SourcePlanJson)
                ?? throw new InvalidOperationException("SourcePlanJson rỗng.");
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException("PLAN_SOURCE_JSON_INVALID", 422, $"SourcePlanJson không hợp lệ: {ex.Message}");
        }

        var run = new QuestionGenerationRun
        {
            ProjectId = projectId,
            InterviewPlanId = plan.Id,
            RequestedBy = userId,
            Status = DomainLayer.Studio.Enums.QuestionGenerationStatus.Generating,
            RequestedQuestionCount = plan.TotalQuestions,
            ReplaceExisting = request.ReplaceExisting,
            IncludeSampleAnswers = request.IncludeSampleAnswers,
            IncludeScoringRubric = request.IncludeScoringRubric,
            GeneratorType = QuestionGeneratorType.Rag,
            StartedAt = DateTime.UtcNow
        };
        dbContext.QuestionGenerationRuns.Add(run);
        await dbContext.SaveChangesAsync(ct);

        try
        {
            // Ưu tiên settings Studio (UI Ngôn ngữ đầu ra) → plan.Language
            var settingsPref = await dbContext.StudioSettings.AsNoTracking()
                .Where(x => x.ProjectId == projectId && x.IsActive)
                .Select(x => new { x.Language, x.ContentMode, x.CodeTemplatesJson })
                .FirstOrDefaultAsync(ct);
            var outputLanguage = StudioOutputLanguage.Normalize(settingsPref?.Language ?? plan.Language);
            var langInstruction = StudioOutputLanguage.RagInstruction(outputLanguage);
            var contentMode = string.IsNullOrWhiteSpace(settingsPref?.ContentMode) ? "Mixed" : settingsPref!.ContentMode.Trim();
            var codeTemplates = ParseCodeTemplatesOrDefault(settingsPref?.CodeTemplatesJson);
            var templatesSegment = codeTemplates.Count > 0 ? string.Join(",", codeTemplates) : "BUG_DETECTION,CODE_COMPLETION";

            var focusNames = await dbContext.PlanFocusAreas.AsNoTracking()
                .Where(x => x.InterviewPlanId == plan.Id && x.IsActive)
                .OrderBy(x => x.OrderIndex)
                .Select(x => x.Name)
                .ToListAsync(ct);
            if (focusNames.Count == 0)
            {
                focusNames = await dbContext.StudioFocusAreas.AsNoTracking()
                    .Where(x => x.StudioSettingsId == settings.Id && x.IsActive)
                    .OrderBy(x => x.OrderIndex)
                    .Select(x => x.Name)
                    .ToListAsync(ct);
            }

            var hrNote = $"STUDIO_UI=1; Studio project {projectId}; plan {plan.Id}; run {run.Id}; CONTENT_MODE={contentMode}; CODE_TEMPLATES={templatesSegment}; {langInstruction}";
            hrNote = StudioRagPlanHrNoteBuilder.AppendQuestionFocusConstraint(hrNote, focusNames);

            await ragService.EnqueueGenerateQuestionsFromPlanAsync(run.Id, new GenerateQuestionsFromPlanRequest
            {
                OwnerId = userId,
                JobDescription = jd.Content,
                ApprovedPlan = approvedPlan,
                Language = outputLanguage,
                HrNote = hrNote
            }, ct);
        }
        catch (Exception ex)
        {
            // Ưu tiên Detail từ RAG (vd. thiếu approvedPlan.order) thay vì chỉ "Validation error"
            var detail = ex is StructuredHttpException she && !string.IsNullOrWhiteSpace(she.Payload.Detail)
                ? she.Payload.Detail!
                : ex.Message;
            run.Status = DomainLayer.Studio.Enums.QuestionGenerationStatus.Failed;
            run.ErrorCode = "RAG_DISPATCH_FAILED";
            run.ErrorMessage = detail;
            run.FailedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            throw new StudioBusinessException("RAG_DISPATCH_FAILED", StatusCodes.Status502BadGateway,
                $"Không enqueue được RAG sinh câu hỏi: {detail}");
        }

        return MapGenerationRunDto(run);
    }

    public async Task<bool> TryApplyRagCallbackAsync(
        Guid runId,
        ApplicationLayer.DTOs.QuestionGeneration.QuestionGenerationCallbackDto dto,
        CancellationToken ct = default)
    {
        var run = await dbContext.QuestionGenerationRuns.FirstOrDefaultAsync(x => x.Id == runId && x.IsActive, ct);
        if (run is null) return false;

        var phase = dto.Phase?.ToUpperInvariant() ?? string.Empty;
        if (phase != "QUESTIONS")
        {
            run.Status = DomainLayer.Studio.Enums.QuestionGenerationStatus.Failed;
            run.ErrorCode = "INVALID_CALLBACK_PHASE";
            run.ErrorMessage = $"Studio chỉ nhận phase QUESTIONS, nhận: {dto.Phase}";
            run.FailedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return true;
        }

        if (!dto.Success)
        {
            run.Status = DomainLayer.Studio.Enums.QuestionGenerationStatus.Failed;
            run.ErrorCode = "RAG_GENERATE_QUESTIONS_FAILED";
            run.ErrorMessage = dto.Detail ?? dto.Error ?? "RAG sinh câu hỏi thất bại.";
            run.FailedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return true;
        }

        var questions = dto.Questions ?? [];
        if (questions.Count == 0)
        {
            run.Status = DomainLayer.Studio.Enums.QuestionGenerationStatus.Failed;
            run.ErrorCode = "RAG_QUESTIONS_EMPTY";
            run.ErrorMessage = "RAG callback không trả câu hỏi nào.";
            run.FailedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return true;
        }

        // SCRUM-429: regen 1 câu — patch đúng TargetQuestionId, không tạo list mới
        if (run.TargetQuestionId.HasValue)
            return await ApplyRegenCallbackAsync(run, questions, ct);

        var sections = await dbContext.PlanSections
            .Where(x => x.InterviewPlanId == run.InterviewPlanId && x.IsActive)
            .OrderBy(x => x.OrderIndex)
            .ToListAsync(ct);

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            if (run.ReplaceExisting)
            {
                var existing = await dbContext.InterviewQuestions
                    .Where(x => x.ProjectId == run.ProjectId && x.InterviewPlanId == run.InterviewPlanId && x.IsActive)
                    .ToListAsync(ct);
                foreach (var q in existing)
                {
                    q.IsActive = false;
                    q.UpdatedAt = DateTime.UtcNow;
                }
            }

            var created = StudioRagQuestionMapper.Map(
                questions,
                run.ProjectId,
                run.InterviewPlanId,
                run.Id,
                sections,
                run.IncludeSampleAnswers,
                run.IncludeScoringRubric);

            if (created.Count == 0)
                throw new StudioBusinessException("RAG_QUESTIONS_EMPTY", 502, "Không map được câu hỏi từ RAG callback.");

            dbContext.InterviewQuestions.AddRange(created);
            var project = await dbContext.InterviewProjects.FirstAsync(x => x.Id == run.ProjectId, ct);
            project.Status = InterviewProjectStatus.Generated;
            run.Status = DomainLayer.Studio.Enums.QuestionGenerationStatus.Completed;
            run.GeneratedQuestionCount = created.Count;
            run.CompletedAt = DateTime.UtcNow;
            run.ErrorCode = null;
            run.ErrorMessage = null;

            // Không còn mirror dual-write sang V1 jobs — Save/Publish qua Studio Save → question_sets.
            // (StudioHistoryMirror deprecated — Phase 4 drop V1 tables.)

            await dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            // SCRUM-445: Free 1/24h — trừ khi sinh full bộ thành công (không trừ lúc lập plan).
            await usageMetering.MarkGenerateSuccessAsync(run.RequestedBy);
            return true;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            run.Status = DomainLayer.Studio.Enums.QuestionGenerationStatus.Failed;
            run.ErrorCode = ex is StudioBusinessException sbe ? sbe.ErrorCode : "GENERATION_FAILED";
            run.ErrorMessage = ex.Message;
            run.FailedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            throw;
        }
    }

    /// <summary>SCRUM-429: callback regen — update 1 InterviewQuestion theo TargetQuestionId.</summary>
    private async Task<bool> ApplyRegenCallbackAsync(
        QuestionGenerationRun run,
        List<ApplicationLayer.DTOs.Rag.RagGeneratedQuestionDto> questions,
        CancellationToken ct)
    {
        var targetId = run.TargetQuestionId!.Value;
        var q = await dbContext.InterviewQuestions.FirstOrDefaultAsync(
            x => x.Id == targetId && x.ProjectId == run.ProjectId && x.IsActive, ct);
        if (q is null)
        {
            run.Status = DomainLayer.Studio.Enums.QuestionGenerationStatus.Failed;
            run.ErrorCode = "REGEN_TARGET_NOT_FOUND";
            run.ErrorMessage = "Không tìm thấy câu hỏi cần regen (có thể đã xóa).";
            run.FailedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return true;
        }

        var plan = await dbContext.InterviewPlans.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == run.InterviewPlanId && x.IsActive, ct);
        var ragQ = questions.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Question));
        if (ragQ is null)
        {
            run.Status = DomainLayer.Studio.Enums.QuestionGenerationStatus.Failed;
            run.ErrorCode = "RAG_REGEN_EMPTY";
            run.ErrorMessage = "RAG không trả câu hỏi sau khi regen.";
            run.FailedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return true;
        }

        try
        {
            var slot = StudioQuestionRegenHelper.ResolveSlot(q, plan?.SourcePlanJson);
            StudioQuestionRegenHelper.ApplyRagResultToQuestion(
                q, ragQ, slot, run.IncludeSampleAnswers, run.IncludeScoringRubric);
            q.GenerationRunId = run.Id;
            run.Status = DomainLayer.Studio.Enums.QuestionGenerationStatus.Completed;
            run.GeneratedQuestionCount = 1;
            run.CompletedAt = DateTime.UtcNow;
            run.ErrorCode = null;
            run.ErrorMessage = null;
            await dbContext.SaveChangesAsync(ct);
            // SCRUM-445: regen Free tối đa 2 lần / plan — không đụng túi 1/24h.
            await usageMetering.IncrementAsync(
                run.RequestedBy,
                DomainLayer.Constants.UsageType.HrQuestionRegen,
                run.InterviewPlanId.ToString("N"));
            return true;
        }
        catch (Exception ex)
        {
            run.Status = DomainLayer.Studio.Enums.QuestionGenerationStatus.Failed;
            run.ErrorCode = "RAG_REGEN_MAP_FAILED";
            run.ErrorMessage = ex.Message;
            run.FailedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task<StudioQuestionListResponse> ListQuestionsAsync(Guid projectId, Guid userId, StudioQuestionListRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        var query = dbContext.InterviewQuestions.Where(x => x.ProjectId == projectId && x.IsActive);
        if (request.PlanId.HasValue) query = query.Where(x => x.InterviewPlanId == request.PlanId.Value);
        if (request.SectionId.HasValue) query = query.Where(x => x.PlanSectionId == request.SectionId.Value);
        if (request.Difficulty.HasValue) query = query.Where(x => x.Difficulty == request.Difficulty.Value);
        if (request.Type.HasValue) query = query.Where(x => x.Type == request.Type.Value);
        if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(x => x.Content.ToLower().Contains(request.Search.ToLower()));
        var total = await query.CountAsync(ct);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;
        // SCRUM-390: load TagsJson rồi map citations in-memory (không parse JSON trong SQL)
        var rows = await query.OrderBy(x => x.OrderIndex).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        var items = new List<StudioQuestionDto>(rows.Count);
        foreach (var row in rows)
            items.Add(await MapQuestionWithImageSasAsync(row, ct));
        return new StudioQuestionListResponse(page, pageSize, total, items);
    }

    public async Task<StudioQuestionDto> UpdateQuestionAsync(Guid projectId, Guid questionId, Guid userId, UpdateQuestionRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var q = await dbContext.InterviewQuestions.FirstOrDefaultAsync(x => x.Id == questionId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("QUESTION_NOT_FOUND", 404, "Không tìm thấy câu hỏi.");
        q.Content = request.Content.Trim();
        q.Difficulty = request.Difficulty;
        q.Type = request.Type;
        q.EstimatedMinutes = request.EstimatedMinutes;
        q.ExpectedAnswer = request.ExpectedAnswer;

        var meta = StudioRagQuestionMapper.ParseMeta(q.TagsJson);
        RubricNormalizer.RubricDocumentV1 rubricDoc;
        if (!string.IsNullOrWhiteSpace(request.RubricJson))
            rubricDoc = RubricNormalizer.NormalizeFromJson(request.RubricJson);
        else if (!string.IsNullOrWhiteSpace(request.ScoringRubric))
        {
            var lines = request.ScoringRubric
                .Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            rubricDoc = RubricNormalizer.NormalizeFromLegacyStrings(lines);
        }
        else
            rubricDoc = RubricNormalizer.NormalizeFromJson(null);

        StudioRagQuestionMapper.ApplyRubricToMeta(meta, rubricDoc);
        q.ScoringRubric = RubricNormalizer.ToDisplayText(rubricDoc);
        if (string.IsNullOrWhiteSpace(q.ScoringRubric))
            q.ScoringRubric = request.ScoringRubric;
        q.TagsJson = StudioRagQuestionMapper.SerializeMeta(meta);
        q.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return await MapQuestionWithImageSasAsync(q, ct);
    }

    public async Task DeleteQuestionAsync(Guid projectId, Guid questionId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var q = await dbContext.InterviewQuestions.FirstOrDefaultAsync(x => x.Id == questionId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("QUESTION_NOT_FOUND", 404, "Không tìm thấy câu hỏi.");
        q.IsActive = false;
        q.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
    }

    /// <summary>SCRUM-429: enqueue RAG async (1 slot) + AVOID_QUESTIONS; FE poll run.
    /// SCRUM-445: Free tối đa 2 lần regen / plan (không đụng túi 1/24h).</summary>
    public async Task<GenerationRunDto> RegenerateQuestionAsync(Guid projectId, Guid questionId, Guid userId, RegenerateQuestionRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var q = await dbContext.InterviewQuestions.FirstOrDefaultAsync(x => x.Id == questionId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("QUESTION_NOT_FOUND", 404, "Không tìm thấy câu hỏi.");

        if (q.InterviewPlanId == Guid.Empty)
            throw new StudioBusinessException("QUESTION_NO_PLAN", 422, "Câu hỏi không gắn plan — không thể regen bằng RAG.");

        await subscriptionGate.CheckQuestionRegenAsync(userId, q.InterviewPlanId);

        var plan = await dbContext.InterviewPlans.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == q.InterviewPlanId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("PLAN_NOT_FOUND", 404, "Không tìm thấy plan của câu hỏi.");

        if (string.IsNullOrWhiteSpace(plan.SourcePlanJson))
            throw new StudioBusinessException("PLAN_SOURCE_JSON_MISSING", 422,
                "Plan không có SourcePlanJson — hãy generate lại plan trước khi regen.");

        var jd = await dbContext.StudioJobDescriptions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("JD_REQUIRED", 422, "Cần Job Description để regen câu hỏi.");
        if (string.IsNullOrWhiteSpace(jd.Content))
            throw new StudioBusinessException("JD_EMPTY", 422, "Job Description đang trống.");

        // Chặn: đang regen cùng câu, hoặc đang generate full plan
        var busySameOrFull = await dbContext.QuestionGenerationRuns.AnyAsync(
            x => x.ProjectId == projectId && x.InterviewPlanId == plan.Id && x.IsActive
                 && x.Status == DomainLayer.Studio.Enums.QuestionGenerationStatus.Generating
                 && (x.TargetQuestionId == questionId || x.TargetQuestionId == null),
            ct);
        if (busySameOrFull)
            throw new StudioBusinessException("GENERATION_IN_PROGRESS", 409,
                "Đang có job sinh/regen câu hỏi — vui lòng đợi hoàn tất.");

        var settingsPref = await dbContext.StudioSettings.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.IsActive)
            .Select(x => new { x.Id, x.Language, x.ContentMode, x.CodeTemplatesJson })
            .FirstOrDefaultAsync(ct);

        var outputLanguage = StudioOutputLanguage.Normalize(settingsPref?.Language ?? plan.Language);
        var langInstruction = StudioOutputLanguage.RagInstruction(outputLanguage);
        var contentMode = string.IsNullOrWhiteSpace(settingsPref?.ContentMode) ? "Mixed" : settingsPref!.ContentMode.Trim();
        var codeTemplates = ParseCodeTemplatesOrDefault(settingsPref?.CodeTemplatesJson);

        var focusNames = await dbContext.PlanFocusAreas.AsNoTracking()
            .Where(x => x.InterviewPlanId == plan.Id && x.IsActive)
            .OrderBy(x => x.OrderIndex)
            .Select(x => x.Name)
            .ToListAsync(ct);
        if (focusNames.Count == 0 && settingsPref is not null)
        {
            focusNames = await dbContext.StudioFocusAreas.AsNoTracking()
                .Where(x => x.StudioSettingsId == settingsPref.Id && x.IsActive)
                .OrderBy(x => x.OrderIndex)
                .Select(x => x.Name)
                .ToListAsync(ct);
        }

        var siblingContents = await dbContext.InterviewQuestions.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.InterviewPlanId == plan.Id && x.IsActive && x.Id != questionId)
            .OrderBy(x => x.OrderIndex)
            .Select(x => x.Content)
            .ToListAsync(ct);
        var avoidNote = StudioQuestionRegenHelper.BuildAvoidQuestionsNote(siblingContents);

        var slot = StudioQuestionRegenHelper.ResolveSlot(q, plan.SourcePlanJson);
        var miniPlan = StudioQuestionRegenHelper.BuildSingleSlotApprovedPlan(plan.SourcePlanJson, slot);
        var hrNote = StudioQuestionRegenHelper.BuildRegenHrNote(
            projectId, plan.Id, q.Id, contentMode, codeTemplates, langInstruction, focusNames,
            request.Instruction, avoidNote);

        var run = new QuestionGenerationRun
        {
            ProjectId = projectId,
            InterviewPlanId = plan.Id,
            RequestedBy = userId,
            Status = DomainLayer.Studio.Enums.QuestionGenerationStatus.Generating,
            RequestedQuestionCount = 1,
            ReplaceExisting = false,
            IncludeSampleAnswers = request.IncludeSampleAnswers,
            IncludeScoringRubric = request.IncludeScoringRubric,
            GeneratorType = QuestionGeneratorType.Rag,
            TargetQuestionId = questionId,
            StartedAt = DateTime.UtcNow
        };
        dbContext.QuestionGenerationRuns.Add(run);
        await dbContext.SaveChangesAsync(ct);

        try
        {
            await ragService.EnqueueGenerateQuestionsFromPlanAsync(run.Id, new GenerateQuestionsFromPlanRequest
            {
                OwnerId = userId,
                JobDescription = jd.Content,
                ApprovedPlan = miniPlan,
                Language = outputLanguage,
                HrNote = hrNote
            }, ct);
        }
        catch (Exception ex)
        {
            var detail = ex is StructuredHttpException she && !string.IsNullOrWhiteSpace(she.Payload.Detail)
                ? she.Payload.Detail!
                : ex.Message;
            run.Status = DomainLayer.Studio.Enums.QuestionGenerationStatus.Failed;
            run.ErrorCode = "RAG_DISPATCH_FAILED";
            run.ErrorMessage = detail;
            run.FailedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            throw new StudioBusinessException("RAG_DISPATCH_FAILED", StatusCodes.Status502BadGateway,
                $"Không enqueue được RAG regen: {detail}");
        }

        return MapGenerationRunDto(run);
    }

    private static GenerationRunDto MapGenerationRunDto(QuestionGenerationRun run) =>
        new(run.Id, run.InterviewPlanId, run.Status, run.RequestedQuestionCount, run.GeneratedQuestionCount,
            run.StartedAt, run.CompletedAt, run.ErrorCode, run.ErrorMessage, run.TargetQuestionId);

    private static readonly HashSet<string> AllowedQuestionImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp"
    };
    private const long MaxQuestionImageBytes = 5 * 1024 * 1024;

    public async Task<StudioQuestionDto> UploadQuestionImageAsync(
        Guid projectId,
        Guid questionId,
        Guid userId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileLength,
        CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var q = await dbContext.InterviewQuestions.FirstOrDefaultAsync(x => x.Id == questionId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("QUESTION_NOT_FOUND", 404, "Không tìm thấy câu hỏi.");

        if (fileLength <= 0)
            throw new StudioBusinessException("IMAGE_EMPTY", 400, "File ảnh trống.");
        if (fileLength > MaxQuestionImageBytes)
            throw new StudioBusinessException("IMAGE_TOO_LARGE", 400, "Ảnh tối đa 5MB.");
        var ctNorm = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        if (!AllowedQuestionImageContentTypes.Contains(ctNorm)
            && !fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            throw new StudioBusinessException("IMAGE_INVALID_TYPE", 400, "Chỉ chấp nhận jpg/jpeg/png/webp.");

        var meta = StudioRagQuestionMapper.ParseMeta(q.TagsJson);
        if (!string.IsNullOrWhiteSpace(meta.AttachedImageBlobPath))
        {
            try { await blobStorage.DeleteAsync(meta.AttachedImageBlobPath, ct); }
            catch { /* bỏ qua nếu blob cũ không còn */ }
        }

        var blobPath = BlobPathHelper.BuildQuestionImagePath(projectId, questionId, fileName);
        await blobStorage.UploadAsync(fileStream, ctNorm, blobPath, ct);
        meta.AttachedImageBlobPath = blobPath;
        q.TagsJson = StudioRagQuestionMapper.SerializeMeta(meta);
        q.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return await MapQuestionWithImageSasAsync(q, ct);
    }

    public async Task<StudioQuestionDto> DeleteQuestionImageAsync(Guid projectId, Guid questionId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var q = await dbContext.InterviewQuestions.FirstOrDefaultAsync(x => x.Id == questionId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("QUESTION_NOT_FOUND", 404, "Không tìm thấy câu hỏi.");

        var meta = StudioRagQuestionMapper.ParseMeta(q.TagsJson);
        if (!string.IsNullOrWhiteSpace(meta.AttachedImageBlobPath))
        {
            try { await blobStorage.DeleteAsync(meta.AttachedImageBlobPath, ct); }
            catch { /* ignore */ }
            meta.AttachedImageBlobPath = null;
            q.TagsJson = StudioRagQuestionMapper.SerializeMeta(meta);
            q.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }

        return await MapQuestionWithImageSasAsync(q, ct);
    }

    private async Task<StudioQuestionDto> MapQuestionWithImageSasAsync(InterviewQuestion q, CancellationToken ct)
    {
        var meta = StudioRagQuestionMapper.ParseMeta(q.TagsJson);
        string? url = null;
        if (!string.IsNullOrWhiteSpace(meta.AttachedImageBlobPath))
        {
            try
            {
                url = await blobStorage.GenerateReadSasUrlAsync(meta.AttachedImageBlobPath, TimeSpan.FromHours(2), ct);
            }
            catch
            {
                url = null;
            }
        }
        return StudioRagQuestionMapper.MapToStudioQuestionDto(q, url);
    }

    public async Task<IReadOnlyList<GenerationRunDto>> ListRunsAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        return await dbContext.QuestionGenerationRuns
            .Where(x => x.ProjectId == projectId && x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new GenerationRunDto(x.Id, x.InterviewPlanId, x.Status, x.RequestedQuestionCount, x.GeneratedQuestionCount, x.StartedAt, x.CompletedAt, x.ErrorCode, x.ErrorMessage, x.TargetQuestionId))
            .ToListAsync(ct);
    }

    public async Task<GenerationRunDto> GetRunAsync(Guid projectId, Guid runId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        var row = await dbContext.QuestionGenerationRuns.FirstOrDefaultAsync(x => x.Id == runId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("GENERATION_RUN_NOT_FOUND", 404, "Không tìm thấy generation run.");
        return MapGenerationRunDto(row);
    }

    private static IReadOnlyList<string> ParseCodeTemplatesOrDefault(string? json)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new List<string>();
            var cleaned = items
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return cleaned.Count > 0
                ? cleaned
                : new[] { "BUG_DETECTION", "CODE_COMPLETION", "REFACTORING", "PERFORMANCE_ANALYSIS" };
        }
        catch
        {
            return new[] { "BUG_DETECTION", "CODE_COMPLETION", "REFACTORING", "PERFORMANCE_ANALYSIS" };
        }
    }
}

public sealed class AiChatService(AppDbContext dbContext, IInterviewProjectService projectService) : IAiChatService
{
    public async IAsyncEnumerable<SseEvent> StreamMessageAsync(Guid projectId, Guid userId, string message, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var session = await dbContext.AiChatSessions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId && x.IsActive, ct);
        if (session is null)
        {
            session = new AiChatSession { ProjectId = projectId, UserId = userId, SelectionMode = AiModelSelectionMode.Auto, SelectedModelDisplayName = "Mock Auto" };
            dbContext.AiChatSessions.Add(session);
            await dbContext.SaveChangesAsync(ct);
        }

        dbContext.AiChatMessages.Add(new AiChatMessage { SessionId = session.Id, Role = AiChatMessageRole.User, Content = message, Status = AiMessageStatus.Completed });
        var assistant = new AiChatMessage { SessionId = session.Id, Role = AiChatMessageRole.Assistant, Content = "Đang xử lý...", Status = AiMessageStatus.Streaming };
        dbContext.AiChatMessages.Add(assistant);
        await dbContext.SaveChangesAsync(ct);

        yield return new SseEvent("message.started", "Mock AI started");
        await Task.Delay(30, ct);
        yield return new SseEvent("message.delta", $"Received: {message}");
        await Task.Delay(30, ct);
        assistant.Content = $"Refined message: {message}";
        assistant.Status = AiMessageStatus.Completed;
        assistant.CompletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        yield return new SseEvent("message.completed", "Mock AI completed");
    }

    public async Task<ChatSessionDto> GetSessionAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        var session = await dbContext.AiChatSessions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId && x.IsActive, ct);
        if (session is null)
        {
            session = new AiChatSession { ProjectId = projectId, UserId = userId, SelectionMode = AiModelSelectionMode.Auto, SelectedModelDisplayName = "Mock Auto" };
            dbContext.AiChatSessions.Add(session);
            await dbContext.SaveChangesAsync(ct);
        }
        return new ChatSessionDto(session.Id, session.ProjectId, session.UserId, session.CreatedAt);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        var session = await dbContext.AiChatSessions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId && x.IsActive, ct);
        if (session is null) return Array.Empty<ChatMessageDto>();
        return await dbContext.AiChatMessages.Where(x => x.SessionId == session.Id && x.IsActive)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new ChatMessageDto(x.Id, x.SessionId, x.Role, x.Content, x.Status, x.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>SCRUM-376: Ghi cặp user/assistant vào transcript Studio.</summary>
    public async Task AppendUserAndAssistantAsync(
        Guid projectId,
        Guid userId,
        string userContent,
        string assistantContent,
        Guid? relatedPlanId,
        int? relatedPlanRevision,
        CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var session = await dbContext.AiChatSessions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId && x.IsActive, ct);
        if (session is null)
        {
            session = new AiChatSession
            {
                ProjectId = projectId,
                UserId = userId,
                SelectionMode = AiModelSelectionMode.Auto,
                SelectedModelDisplayName = "Studio"
            };
            dbContext.AiChatSessions.Add(session);
            await dbContext.SaveChangesAsync(ct);
        }

        var now = DateTime.UtcNow;
        dbContext.AiChatMessages.Add(new AiChatMessage
        {
            SessionId = session.Id,
            Role = AiChatMessageRole.User,
            Content = userContent.Trim(),
            Status = AiMessageStatus.Completed,
            RelatedPlanId = relatedPlanId,
            RelatedPlanRevision = relatedPlanRevision,
            CompletedAt = now
        });
        dbContext.AiChatMessages.Add(new AiChatMessage
        {
            SessionId = session.Id,
            Role = AiChatMessageRole.Assistant,
            Content = assistantContent.Trim(),
            Status = AiMessageStatus.Completed,
            RelatedPlanId = relatedPlanId,
            RelatedPlanRevision = relatedPlanRevision,
            ResolvedModelName = "Studio",
            CompletedAt = now
        });
        await dbContext.SaveChangesAsync(ct);
    }
}

public sealed class StudioMockAiService : IStudioMockAiService
{
    public (int TotalQuestions, int InterviewLengthMinutes, string Title) BuildInitialPlan(string? detectedRole, string? seniority)
    {
        var role = string.IsNullOrWhiteSpace(detectedRole) ? "Software Engineer" : detectedRole;
        var isSenior = string.Equals(seniority, "Senior", StringComparison.OrdinalIgnoreCase);
        return (15, isSenior ? 75 : 60, $"Interview Plan for {role}");
    }

    public (int TotalQuestions, int InterviewLengthMinutes, QuestionDifficulty Difficulty, string Note) ApplyRefineRule(int totalQuestions, int minutes, QuestionDifficulty difficulty, string instruction)
    {
        var lower = instruction.ToLowerInvariant();
        var note = "general";
        if (lower.Contains("add more system design"))
        {
            totalQuestions += 2;
            minutes += 10;
            note = "add-system-design";
        }
        if (lower.Contains("harder"))
        {
            difficulty = QuestionDifficulty.Hard;
            note = "harder";
        }
        if (lower.Contains("add one more behavioral"))
        {
            totalQuestions += 1;
            minutes += 5;
            note = "add-behavioral";
        }
        if (lower.Contains("reduce behavioral"))
        {
            totalQuestions = Math.Max(5, totalQuestions - 1);
            minutes = Math.Max(20, minutes - 5);
            note = "reduce-behavioral";
        }
        if (lower.Contains("focus on"))
        {
            note = "focus-area";
        }
        return (totalQuestions, minutes, difficulty, note);
    }

    public string GenerateQuestionText(string sectionName, int order)
    {
        var pool = new[]
        {
            "Giải thích sự khác nhau giữa Scoped/Singleton/Transient trong ASP.NET Core DI.",
            "Thiết kế order processing service dùng PostgreSQL, đảm bảo idempotency và retry.",
            "Điều tra tình huống latency API tăng từ 200ms lên 3s trong production.",
            "Bạn giao tiếp thế nào với stakeholder khi incident đang diễn ra?",
            "Nêu trade-off giữa eager loading và projection trong EF Core."
        };
        return $"[{sectionName}] {pool[(order - 1) % pool.Length]}";
    }
}

public sealed class StudioSettingsService(AppDbContext dbContext, IInterviewProjectService projectService) : IStudioSettingsService
{
    public async Task<StudioSettingsDto> GetAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        var settings = await dbContext.StudioSettings
            .Include(x => x.FocusAreas.Where(f => f.IsActive))
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        var readiness = await BuildReadinessAsync(projectId, settings?.AppliedPlanId, ct);
        if (settings is null)
        {
            return new StudioSettingsDto(
                projectId, null, 0, 0, QuestionDifficulty.Medium, "Professional", false, false, "Markdown",
                readiness, StudioQuestionTypesHelper.DefaultTypes, StudioOutputLanguage.Vietnamese,
                "Mixed", new[] { "BUG_DETECTION", "CODE_COMPLETION", "REFACTORING", "PERFORMANCE_ANALYSIS" });
        }
        return MapSettingsDto(projectId, settings, readiness);
    }

    public async Task<StudioSettingsDto> UpdateAsync(Guid projectId, Guid userId, UpdateStudioSettingsRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        // Studio Tạo câu hỏi: số câu 5–50
        if (request.NumberOfQuestions is < 5 or > 50)
            throw new StudioBusinessException("INVALID_QUESTION_COUNT", StatusCodes.Status400BadRequest, "Số câu phải từ 5–50.");
        if (request.InterviewLengthMinutes is < 15 or > 180)
            throw new StudioBusinessException("INVALID_INTERVIEW_MINUTES", StatusCodes.Status400BadRequest, "Thời lượng phải từ 15–180 phút.");

        var settings = await dbContext.StudioSettings
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (settings is null)
        {
            settings = new StudioSettings { ProjectId = projectId, AppliedPlanId = null };
            dbContext.StudioSettings.Add(settings);
        }

        List<string> types;
        if (request.QuestionDistribution is { Count: > 0 })
        {
            types = StudioQuestionTaxonomyMapper.ToLegacyQuestionTypes(request.QuestionDistribution).ToList();
            settings.QuestionDistributionJson = JsonSerializer.Serialize(request.QuestionDistribution);
        }
        else
        {
            types = StudioQuestionTypesHelper.Normalize(request.QuestionTypes);
            if (types.Count == 0) types = StudioQuestionTypesHelper.DefaultTypes.ToList();
            var (dist, stylesFromLegacy) = StudioQuestionTaxonomyMapper.FromLegacyQuestionTypes(types, request.NumberOfQuestions);
            settings.QuestionDistributionJson = JsonSerializer.Serialize(dist);
            if (request.QuestionStyles is null or { Count: 0 } && stylesFromLegacy.Count > 0)
                settings.QuestionStylesJson = JsonSerializer.Serialize(stylesFromLegacy);
        }
        StudioQuestionTypesHelper.EnsureValidOrThrow(types);

        if (request.QuestionStyles is { Count: > 0 })
        {
            var normalizedStyles = request.QuestionStyles
                .Select(StudioQuestionTaxonomyMapper.NormalizeStyle)
                .Where(s => s is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();
            settings.QuestionStylesJson = JsonSerializer.Serialize(normalizedStyles);
        }

        // FE gửi outputLanguage; một số client cũ gửi language
        var language = StudioOutputLanguage.Normalize(request.OutputLanguage ?? request.Language);

        settings.InterviewLengthMinutes = request.InterviewLengthMinutes;
        settings.NumberOfQuestions = request.NumberOfQuestions;
        settings.Difficulty = request.Difficulty;
        // Tone/format đã bỏ khỏi Studio UX — luôn cố định, bỏ qua input client
        settings.QuestionTone = "Professional";
        settings.IncludeSampleAnswers = request.IncludeSampleAnswers;
        settings.IncludeScoringRubric = request.IncludeScoringRubric;
        settings.OutputFormat = "StructuredInterviewKit";
        settings.Language = language;
        settings.QuestionTypesJson = StudioQuestionTypesHelper.ToJson(types);
        settings.ContentMode = string.IsNullOrWhiteSpace(request.ContentMode) ? "Mixed" : request.ContentMode.Trim();
        settings.CodeTemplatesJson = JsonSerializer.Serialize(
            (request.EnabledCodeTemplates ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray());

        settings.UpdatedAt = DateTime.UtcNow;
        // Phase 1: chỉ lưu scalar settings (không đụng focus navigation)
        await dbContext.SaveChangesAsync(ct);

        // Phase 2: thay focus qua ExecuteUpdate + AddRange (tách khỏi tracker settings)
        if (request.FocusAreas is { Count: > 0 })
        {
            dbContext.ChangeTracker.Clear();
            await StudioFocusAreaReplacementHelper.ReplaceViaDbSetAsync(
                dbContext, settings.Id, request.FocusAreas, ct);
        }

        settings = await dbContext.StudioSettings
            .AsNoTracking()
            .Include(x => x.FocusAreas.Where(f => f.IsActive))
            .FirstAsync(x => x.Id == settings.Id, ct);
        var readiness = await BuildReadinessAsync(projectId, settings.AppliedPlanId, ct);
        return MapSettingsDto(projectId, settings, readiness);
    }

    private static StudioSettingsDto MapSettingsDto(Guid projectId, StudioSettings settings, StudioReadinessDto readiness)
    {
        var types = StudioQuestionTypesHelper.ParseOrDefault(settings.QuestionTypesJson);
        var distribution = StudioAiConfigurationHelper.ParseQuestionDistribution(settings.QuestionDistributionJson);
        if (distribution.Count == 0 && settings.NumberOfQuestions > 0)
        {
            var (derived, _) = StudioQuestionTaxonomyMapper.FromLegacyQuestionTypes(types, settings.NumberOfQuestions);
            distribution = derived;
        }

        var styles = StudioAiConfigurationHelper.ParseQuestionStyles(settings.QuestionStylesJson);
        if (styles.Count == 0)
            styles = StudioQuestionTaxonomyMapper.ExtractStylesFromLegacyTypes(types);

        var focusAreas = StudioAiConfigurationHelper.MapFocusAreas(settings.FocusAreas.Where(x => x.IsActive));
        var recommended = StudioAiConfigurationHelper.ParseRecommendedConfiguration(settings.AiRecommendationJson);

        return new StudioSettingsDto(
            projectId,
            settings.AppliedPlanId,
            settings.InterviewLengthMinutes,
            settings.NumberOfQuestions,
            settings.Difficulty,
            settings.QuestionTone,
            settings.IncludeSampleAnswers,
            settings.IncludeScoringRubric,
            settings.OutputFormat,
            readiness,
            types,
            StudioOutputLanguage.Normalize(settings.Language),
            string.IsNullOrWhiteSpace(settings.ContentMode) ? "Mixed" : settings.ContentMode,
            ParseCodeTemplatesOrDefault(settings.CodeTemplatesJson),
            distribution,
            focusAreas,
            styles,
            recommended,
            settings.AiRecommendationGeneratedAt);
    }

    private async Task<StudioReadinessDto> BuildReadinessAsync(Guid projectId, Guid? appliedPlanId, CancellationToken ct)
    {
        var hasJd = await dbContext.StudioJobDescriptions.AnyAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        var hasSelectedDoc = await dbContext.StudioKnowledgeDocuments.AnyAsync(x => x.ProjectId == projectId && x.IsActive && x.IsSelected && x.ProcessingStatus == DocumentProcessingStatus.Completed, ct);
        var hasAwaiting = await dbContext.InterviewPlans.AnyAsync(x => x.ProjectId == projectId && x.IsActive && x.Status == InterviewPlanStatus.AwaitingApproval, ct);
        var hasApproved = await dbContext.InterviewPlans.AnyAsync(x => x.ProjectId == projectId && x.IsActive && x.Status == InterviewPlanStatus.Approved, ct);
        var canGenerate = hasApproved && appliedPlanId.HasValue && await dbContext.InterviewPlans.AnyAsync(x => x.Id == appliedPlanId && x.ProjectId == projectId && x.Status == InterviewPlanStatus.Approved && x.IsActive, ct);
        return new StudioReadinessDto(hasJd, hasSelectedDoc, hasAwaiting, hasApproved, canGenerate);
    }

    private static IReadOnlyList<string> ParseCodeTemplatesOrDefault(string? json)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new List<string>();
            var cleaned = items.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return cleaned.Count > 0 ? cleaned : new[] { "BUG_DETECTION", "CODE_COMPLETION", "REFACTORING", "PERFORMANCE_ANALYSIS" };
        }
        catch
        {
            return new[] { "BUG_DETECTION", "CODE_COMPLETION", "REFACTORING", "PERFORMANCE_ANALYSIS" };
        }
    }
}

public sealed class StudioShareService(AppDbContext dbContext, IInterviewProjectService projectService) : IStudioShareService
{
    public async Task<ShareLinkDto> CreateAsync(Guid projectId, Guid userId, CreateShareLinkRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var token = CreateToken();
        var row = new ProjectShare
        {
            ProjectId = projectId,
            Permission = request.Permission,
            ExpiresAt = request.ExpiresAt,
            CreatedBy = userId,
            Token = token
        };
        dbContext.ProjectShares.Add(row);
        await dbContext.SaveChangesAsync(ct);
        return new ShareLinkDto(row.Id, row.Token, row.Permission, row.ExpiresAt, row.IsActive);
    }

    public async Task RevokeAsync(Guid projectId, Guid shareId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var row = await dbContext.ProjectShares.FirstOrDefaultAsync(x => x.Id == shareId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("SHARE_LINK_NOT_FOUND", 404, "Không tìm thấy share link.");
        row.IsActive = false;
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<SharedProjectDto> ResolveAsync(string token, CancellationToken ct)
    {
        var row = await dbContext.ProjectShares.FirstOrDefaultAsync(x => x.Token == token && x.IsActive, ct)
            ?? throw new StudioBusinessException("SHARE_LINK_NOT_FOUND", 404, "Không tìm thấy share link.");
        if (row.ExpiresAt.HasValue && row.ExpiresAt.Value < DateTime.UtcNow)
            throw new StudioBusinessException("SHARE_LINK_EXPIRED", 410, "Share link đã hết hạn.");
        var project = await dbContext.InterviewProjects.FirstOrDefaultAsync(x => x.Id == row.ProjectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("PROJECT_NOT_FOUND", 404, "Không tìm thấy project.");
        return new SharedProjectDto(project.Id, project.Name, project.Description, project.Status);
    }

    private static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[48];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}

public sealed class StudioKnowledgeDocumentService(
    AppDbContext dbContext,
    IInterviewProjectService projectService,
    IDocumentTextExtractorFactory extractorFactory) : IStudioKnowledgeDocumentService
{
    public async Task<StudioDocumentDto> UploadAsync(Guid projectId, Guid userId, UploadStudioDocumentRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        if (request.Content.Length > 20 * 1024 * 1024)
            throw new StudioBusinessException("DOCUMENT_TOO_LARGE", 400, "File vượt quá 20MB.");
        var extractor = extractorFactory.Resolve(request.ContentType, request.FileName);
        var text = await extractor.ExtractTextAsync(request.Content, ct);
        var row = new StudioKnowledgeDocument
        {
            ProjectId = projectId,
            FileName = request.FileName,
            FileType = request.ContentType,
            FileSize = request.Content.LongLength,
            StoragePath = $"studio-documents/{projectId}/{Guid.NewGuid():N}-{request.FileName}",
            ExtractedText = text,
            IsSelected = request.IsSelected,
            ProcessingStatus = DocumentProcessingStatus.Completed
        };
        dbContext.StudioKnowledgeDocuments.Add(row);
        await dbContext.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<IReadOnlyList<StudioDocumentDto>> ListAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        return await dbContext.StudioKnowledgeDocuments.Where(x => x.ProjectId == projectId && x.IsActive)
            .OrderByDescending(x => x.CreatedAt).Select(x => Map(x)).ToListAsync(ct);
    }

    public async Task<StudioDocumentDto> SetSelectionAsync(Guid projectId, Guid documentId, Guid userId, bool isSelected, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var row = await dbContext.StudioKnowledgeDocuments.FirstOrDefaultAsync(x => x.Id == documentId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("DOCUMENT_NOT_FOUND", 404, "Không tìm thấy tài liệu.");
        if (row.ProcessingStatus != DocumentProcessingStatus.Completed && isSelected)
            throw new StudioBusinessException("DOCUMENT_NOT_READY", 422, "Tài liệu chưa xử lý xong.");
        row.IsSelected = isSelected;
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task DeleteAsync(Guid projectId, Guid documentId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var row = await dbContext.StudioKnowledgeDocuments.FirstOrDefaultAsync(x => x.Id == documentId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("DOCUMENT_NOT_FOUND", 404, "Không tìm thấy tài liệu.");
        row.IsActive = false;
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
    }

    // Legacy local-extract service — RAG flow dùng StudioKnowledgeRagService.
    public Task<StudioDocumentDto> ReingestAsync(Guid projectId, Guid documentId, Guid userId, CancellationToken ct)
        => throw new StudioBusinessException("DOCUMENT_REINGEST_UNSUPPORTED", 501, "Reingest chỉ hỗ trợ qua pipeline RAG (StudioKnowledgeRagService).");

    public Task<IReadOnlyList<StudioLibraryDocumentDto>> ListLibraryAsync(Guid projectId, Guid userId, CancellationToken ct)
        => throw new StudioBusinessException("DOCUMENT_LIBRARY_UNSUPPORTED", 501, "Library attach chỉ hỗ trợ qua StudioKnowledgeRagService.");

    public Task<IReadOnlyList<StudioDocumentDto>> AttachFromLibraryAsync(
        Guid projectId, Guid userId, AttachStudioDocumentsRequest request, CancellationToken ct)
        => throw new StudioBusinessException("DOCUMENT_LIBRARY_UNSUPPORTED", 501, "Library attach chỉ hỗ trợ qua StudioKnowledgeRagService.");

    public Task<IReadOnlyList<StudioKnowledgeSuggestionDto>> SuggestAttachAsync(
        Guid projectId, Guid userId, CancellationToken ct)
        => throw new StudioBusinessException("DOCUMENT_SUGGEST_UNSUPPORTED", 501, "Suggestions chỉ hỗ trợ qua StudioKnowledgeRagService.");

    public Task<StudioRetrievePreviewDto> RetrievePreviewAsync(
        Guid projectId, Guid userId, StudioRetrievePreviewRequest request, CancellationToken ct)
        => throw new StudioBusinessException("DOCUMENT_PREVIEW_UNSUPPORTED", 501, "Retrieve preview chỉ hỗ trợ qua StudioKnowledgeRagService.");

    private static StudioDocumentDto Map(StudioKnowledgeDocument x)
        => new(x.Id, x.FileName, x.FileType, x.FileSize, x.IsSelected, x.ProcessingStatus,
            x.ExtractedText is null ? null : x.ExtractedText[..Math.Min(300, x.ExtractedText.Length)],
            x.KnowledgeDocumentId);
}
