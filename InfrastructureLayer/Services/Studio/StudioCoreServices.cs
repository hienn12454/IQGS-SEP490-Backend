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
    {
        var project = await EnsureProjectAccessAsync(projectId, userId, true, ct);

        var interviewQuestions = await dbContext.InterviewQuestions
            .Where(q => q.ProjectId == projectId && q.IsActive)
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

        var title = FirstNonEmpty(plan?.Title, jd?.DetectedRole, jd?.Title, project.Name);
        var jdContent = string.IsNullOrWhiteSpace(jd?.Content)
            ? "(Studio) Job description trống."
            : jd!.Content.Trim();
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

            var criteriaJson = meta.EvaluationCriteria is { Count: > 0 }
                ? JsonSerializer.Serialize(meta.EvaluationCriteria, JsonOptions)
                : (string.IsNullOrWhiteSpace(q.ScoringRubric)
                    ? "[]"
                    : JsonSerializer.Serialize(
                        q.ScoringRubric.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                        JsonOptions));

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

    public async Task PublishFromProjectAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var detail = await GetAsync(projectId, userId, ct);
        var questionSetId = detail.QuestionSetId;
        if (questionSetId is null)
        {
            var saved = await SaveQuestionSetAsync(projectId, userId, ct);
            questionSetId = saved.QuestionSetId;
        }

        await questionSetService.PublishAsync(questionSetId.Value, userId);
    }

    public async Task UnpublishFromProjectAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var detail = await GetAsync(projectId, userId, ct);
        if (detail.QuestionSetId is null)
            throw new StudioBusinessException("SET_NOT_FOUND", StatusCodes.Status404NotFound, "Project chưa có bộ câu hỏi đã Save.");

        await questionSetService.UnpublishAsync(detail.QuestionSetId.Value, userId);
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
    IJobDescriptionAnalyzer analyzer) : IJobDescriptionService
{
    public async Task UpsertAsync(Guid projectId, Guid userId, UpsertJobDescriptionRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var content = request.Content.Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new StudioBusinessException("JOB_DESCRIPTION_EMPTY", StatusCodes.Status400BadRequest, "Nội dung JD không được rỗng.");

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
        await dbContext.SaveChangesAsync(ct);
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
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return summary;
    }

    public async Task<JobDescriptionContentDto?> GetContentAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        var row = await dbContext.StudioJobDescriptions.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (row is null) return null;

        string[] skills = [];
        if (!string.IsNullOrWhiteSpace(row.DetectedSkillsJson))
        {
            try { skills = System.Text.Json.JsonSerializer.Deserialize<string[]>(row.DetectedSkillsJson) ?? []; }
            catch { skills = []; }
        }

        AnalyzeJobDescriptionResponse? summary = null;
        if (!string.IsNullOrWhiteSpace(row.DetectedRole) || skills.Length > 0)
        {
            summary = new AnalyzeJobDescriptionResponse(row.DetectedRole, row.DetectedSeniority, row.DetectedLanguage, skills);
        }

        return new JobDescriptionContentDto(
            row.Content,
            row.SourceType,
            row.OriginalFileName,
            row.WordCount,
            row.CharacterCount,
            summary);
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
            plan.ConcurrencyVersion);
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

        var selectedDocs = await dbContext.StudioKnowledgeDocuments.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.IsActive && x.IsSelected
                        && x.ProcessingStatus == DocumentProcessingStatus.Completed
                        && x.KnowledgeDocumentId != null)
            .Select(x => new { x.KnowledgeDocumentId, x.FileName })
            .ToListAsync(ct);
        var selectedReady = selectedDocs.Count;
        var documentIds = selectedDocs.Select(x => x.KnowledgeDocumentId!.Value).Distinct().ToList();
        // Knowledge documents optional — chỉ JD là bắt buộc để lập plan (SCRUM: JD-only)

        var settings = await dbContext.StudioSettings.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        var numberOfQuestions = settings?.NumberOfQuestions > 0 ? settings.NumberOfQuestions : 15;
        var difficulty = (settings?.Difficulty ?? QuestionDifficulty.Medium).ToString().ToLowerInvariant();
        var skills = ParseSkillsJson(jd.DetectedSkillsJson);
        var preferredMinutes = settings?.InterviewLengthMinutes;
        var interviewMinutes = preferredMinutes is > 0 ? preferredMinutes.Value : Math.Clamp(numberOfQuestions * 4, 30, 120);
        // SCRUM-370: loại câu từ Studio settings (fallback 4 loại mặc định)
        var studioQuestionTypes = StudioQuestionTypesHelper.ParseOrDefault(settings?.QuestionTypesJson);
        var outputLanguage = StudioOutputLanguage.Normalize(settings?.Language);

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
                DocumentIds = documentIds.Count > 0 ? documentIds : null,
                HrNote = StudioRagPlanHrNoteBuilder.BuildInitial(
                    projectId, selectedReady, numberOfQuestions, interviewMinutes, outputLanguage)
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
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException("RAG_PLAN_MAP_FAILED", StatusCodes.Status502BadGateway,
                $"Không map được plan RAG: {ex.Message}");
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

        await usageMetering.MarkGenerateSuccessAsync(userId);
        return new PlanSummaryDto(plan.Id, plan.Revision, plan.Title, plan.Status, plan.TotalQuestions);
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
            return await PersistRefinedPlanAsync(
                project, source, userId, instruction, intent, localMapped, questionTypes, outputLanguage,
                usedLocalPatch: true, draftKey, ct);
        }

        var hrNote = StudioRagRefineHrNoteBuilder.Build(
            instruction, source, sectionTuples, numberOfQuestions, difficulty, questionTypes,
            outputLanguage, selectedDocNames, intent.FocusHints, intent.ExclusiveFocus);

        GeneratePlanResult ragResult;
        try
        {
            ragResult = await ragService.GeneratePlanAsync(new GeneratePlanRequest
            {
                OwnerId = userId,
                JobDescription = jd.Content,
                NumberOfQuestions = numberOfQuestions,
                Difficulty = difficulty,
                QuestionTypes = questionTypes,
                Skills = skills,
                Language = outputLanguage,
                DocumentIds = documentIds.Count > 0 ? documentIds : null,
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
            mapped = StudioRagPlanMapper.MapFromRagPlanObject(ragResult.Plan, numberOfQuestions, preferredMinutes);
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException("RAG_PLAN_MAP_FAILED", StatusCodes.Status502BadGateway,
                $"Không map được plan RAG: {ex.Message}");
        }

        var resolvedMinutes = StudioRefineInstructionParser.ResolveInterviewMinutes(
            intent, mapped.InterviewLengthMinutes, numberOfQuestions, preferredMinutes);
        mapped = mapped with { InterviewLengthMinutes = resolvedMinutes };
        if (intent.Difficulty is not null)
            mapped = mapped with { Difficulty = intent.Difficulty.Value };

        // SCRUM-389: exclusive → lọc coverage/focus ngoài ALLOWED_TOPICS; 1 lần retry nếu vẫn lệch
        if (intent.ExclusiveFocus && intent.FocusHints.Count > 0)
        {
            if (StudioExclusiveCoverageFilter.HasDisallowedFocus(mapped, intent.FocusHints))
            {
                try
                {
                    var retryNote = hrNote + "\nVALIDATION_RETRY: drop non-allowed coverage; ONLY ALLOWED_TOPICS.";
                    var retry = await ragService.GeneratePlanAsync(new GeneratePlanRequest
                    {
                        OwnerId = userId,
                        JobDescription = jd.Content,
                        NumberOfQuestions = numberOfQuestions,
                        Difficulty = difficulty,
                        QuestionTypes = questionTypes,
                        Skills = skills,
                        Language = outputLanguage,
                        DocumentIds = documentIds.Count > 0 ? documentIds : null,
                        HrNote = retryNote.Length <= 2000 ? retryNote : retryNote[..2000]
                    }, ct);
                    mapped = StudioRagPlanMapper.MapFromRagPlanObject(retry.Plan, numberOfQuestions, preferredMinutes);
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

        return await PersistRefinedPlanAsync(
            project, source, userId, instruction, intent, mapped, questionTypes, outputLanguage,
            usedLocalPatch: false, draftKey, ct);
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

    private async Task<PlanRefineResultDto> PersistRefinedPlanAsync(
        InterviewProject project,
        InterviewPlan source,
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
        if (source.Status is InterviewPlanStatus.AwaitingApproval or InterviewPlanStatus.Draft or InterviewPlanStatus.Rejected)
        {
            source.Status = InterviewPlanStatus.Superseded;
            source.UpdatedAt = DateTime.UtcNow;
        }

        var revision = await AllocateNextPlanRevisionAsync(project, ct);
        var sessionId = source.SessionId != Guid.Empty
            ? source.SessionId
            : await EnsureProjectChatSessionIdAsync(project.Id, userId, ct);
        if (source.SessionId != Guid.Empty
            && !await dbContext.AiChatSessions.AnyAsync(x => x.Id == source.SessionId && x.IsActive, ct))
        {
            sessionId = await EnsureProjectChatSessionIdAsync(project.Id, userId, ct);
        }

        var refined = new InterviewPlan
        {
            ProjectId = project.Id,
            SessionId = sessionId,
            Revision = revision,
            Status = InterviewPlanStatus.AwaitingApproval,
            Title = mapped.Title,
            TotalQuestions = mapped.TotalQuestions,
            InterviewLengthMinutes = mapped.InterviewLengthMinutes,
            Difficulty = mapped.Difficulty,
            QuestionTone = intent.QuestionTone ?? source.QuestionTone,
            Language = outputLanguage,
            SeniorityLevel = mapped.SeniorityLevel,
            IncludeSampleAnswers = intent.IncludeSampleAnswers ?? source.IncludeSampleAnswers,
            IncludeScoringRubric = intent.IncludeScoringRubric ?? source.IncludeScoringRubric,
            OutputFormat = intent.OutputFormat ?? source.OutputFormat,
            GeneratedByModelName = usedLocalPatch ? "StudioSettingsPatch" : "RAG",
            SourcePlanJson = mapped.SourcePlanJson
        };
        dbContext.InterviewPlans.Add(refined);
        await dbContext.SaveChangesAsync(ct);

        foreach (var s in mapped.Sections)
        {
            dbContext.PlanSections.Add(new PlanSection
            {
                InterviewPlanId = refined.Id,
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
                InterviewPlanId = refined.Id,
                Name = f.Name,
                Weight = f.Weight,
                OrderIndex = f.OrderIndex
            });
        }

        project.LatestPlanRevision = revision;
        project.Status = InterviewProjectStatus.AwaitingApproval;
        await SyncStudioSettingsFromPlanAsync(project.Id, mapped, questionTypes, ct, intent);
        await dbContext.SaveChangesAsync(ct);

        var citationFiles = usedLocalPatch
            ? StudioRagPlanMapper.ExtractCitationSourceFiles(mapped.SourcePlanJson).Take(8).ToList()
            : StudioRagPlanMapper.ExtractCitationSourceFiles(mapped.SourcePlanJson).Take(8).ToList();
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
            refined.Revision, refined.Title, refined.TotalQuestions, changedFields, citationFiles,
            intent.FocusHints, intent.ExclusiveFocus, usedLocalPatch);

        await aiChatService.AppendUserAndAssistantAsync(
            project.Id,
            userId,
            instruction.Trim(),
            assistantMessage,
            refined.Id,
            refined.Revision,
            ct);

        await usageMetering.IncrementAsync(userId, DomainLayer.Constants.UsageType.HrPlanRegenerate, draftKey);
        return new PlanRefineResultDto(
            refined.Id, refined.Revision, refined.Title, refined.Status, refined.TotalQuestions,
            settingsDto, changedFields, citationFiles, assistantMessage);
    }

    /// <summary>
    /// SCRUM-370 + SCRUM-376: Form Studio → patch plan local (không RAG),
    /// giữ focus/section/coverage từ chat refine trước đó.
    /// </summary>
    public async Task<PlanSummaryDto> ApplySettingsAsync(Guid projectId, Guid planId, Guid userId, ApplyPlanSettingsRequest request, CancellationToken ct)
    {
        StudioQuestionTypesHelper.EnsureValidOrThrow(request.QuestionTypes);
        var types = StudioQuestionTypesHelper.Normalize(request.QuestionTypes);
        if (request.NumberOfQuestions is < 5 or > 50)
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

        // Cập nhật settings trước (AppliedPlanId nullable — chưa gán đến khi approve / sync từ plan mới)
        var settings = await dbContext.StudioSettings.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (settings is null)
        {
            settings = new StudioSettings { ProjectId = projectId, AppliedPlanId = null };
            dbContext.StudioSettings.Add(settings);
        }
        settings.NumberOfQuestions = request.NumberOfQuestions;
        settings.Difficulty = request.Difficulty;
        settings.InterviewLengthMinutes = request.InterviewLengthMinutes;
        settings.QuestionTypesJson = StudioQuestionTypesHelper.ToJson(types);
        settings.UpdatedAt = DateTime.UtcNow;

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

        StudioRagPlanMapper.MappedPlan mapped;
        try
        {
            mapped = StudioPlanSettingsPatcher.Apply(
                source,
                sectionRows.Select(s => new StudioPlanSettingsPatcher.SectionInput(
                    s.Name, s.Description, s.OrderIndex, s.NumberOfQuestions, s.Difficulty, s.EstimatedMinutes)).ToList(),
                focusRows.Select(f => new StudioPlanSettingsPatcher.FocusInput(f.Name, f.Weight, f.OrderIndex)).ToList(),
                request.NumberOfQuestions,
                request.Difficulty,
                request.InterviewLengthMinutes,
                types);
        }
        catch (Exception ex)
        {
            throw new StudioBusinessException("PLAN_SETTINGS_PATCH_FAILED", StatusCodes.Status422UnprocessableEntity,
                $"Không áp dụng settings vào plan: {ex.Message}");
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
            Language = source.Language,
            SeniorityLevel = mapped.SeniorityLevel,
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

        // SCRUM-376: ghi transcript apply vào chat history
        await aiChatService.AppendUserAndAssistantAsync(
            projectId,
            userId,
            $"Áp dụng settings: {request.NumberOfQuestions}q, {request.Difficulty}, {request.InterviewLengthMinutes} phút, types=[{string.Join(", ", types)}]",
            $"Đã cập nhật plan (revision {patched.Revision}): {patched.Title} — {patched.TotalQuestions} câu hỏi (giữ focus/section từ chat trước đó).",
            patched.Id,
            patched.Revision,
            ct);

        return new PlanSummaryDto(patched.Id, patched.Revision, patched.Title, patched.Status, patched.TotalQuestions);
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
        settings.NumberOfQuestions = mapped.TotalQuestions;
        settings.Difficulty = mapped.Difficulty;
        settings.InterviewLengthMinutes = mapped.InterviewLengthMinutes;
        settings.SeniorityLevel = mapped.SeniorityLevel;
        settings.QuestionTypesJson = StudioQuestionTypesHelper.ToJson(questionTypes);
        // SCRUM-388: chỉ ghi đè preference khi user nêu trong chat
        if (intent?.Language is not null)
            settings.Language = StudioOutputLanguage.Normalize(intent.Language);
        if (intent?.QuestionTone is not null)
            settings.QuestionTone = intent.QuestionTone;
        if (intent?.OutputFormat is not null)
            settings.OutputFormat = intent.OutputFormat;
        if (intent?.IncludeSampleAnswers is not null)
            settings.IncludeSampleAnswers = intent.IncludeSampleAnswers.Value;
        if (intent?.IncludeScoringRubric is not null)
            settings.IncludeScoringRubric = intent.IncludeScoringRubric.Value;
        settings.UpdatedAt = DateTime.UtcNow;
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
            settings.NumberOfQuestions = plan.TotalQuestions;
            settings.InterviewLengthMinutes = plan.InterviewLengthMinutes;
            settings.SeniorityLevel = plan.SeniorityLevel;
            settings.Language = plan.Language;
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
    IBlobStorageService blobStorage) : IQuestionGenerationService
{
    public async Task<GenerationRunDto> GenerateAsync(Guid projectId, Guid userId, GenerateQuestionsRequest request, CancellationToken ct)
    {
        // SCRUM-371: giống luồng HR — enqueue RAG async, callback ghi InterviewQuestions
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

            await ragService.EnqueueGenerateQuestionsFromPlanAsync(run.Id, new GenerateQuestionsFromPlanRequest
            {
                OwnerId = userId,
                JobDescription = jd.Content,
                ApprovedPlan = approvedPlan,
                Language = outputLanguage,
                HrNote = $"STUDIO_UI=1; Studio project {projectId}; plan {plan.Id}; run {run.Id}; CONTENT_MODE={contentMode}; CODE_TEMPLATES={templatesSegment}; {langInstruction}"
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

        return new GenerationRunDto(
            run.Id, run.InterviewPlanId, run.Status, run.RequestedQuestionCount, run.GeneratedQuestionCount,
            run.StartedAt, run.CompletedAt, run.ErrorCode, run.ErrorMessage);
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
        q.ScoringRubric = request.ScoringRubric;
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

    public async Task<StudioQuestionDto> RegenerateQuestionAsync(Guid projectId, Guid questionId, Guid userId, RegenerateQuestionRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        var q = await dbContext.InterviewQuestions.FirstOrDefaultAsync(x => x.Id == questionId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("QUESTION_NOT_FOUND", 404, "Không tìm thấy câu hỏi.");
        q.Content = $"{mockAiService.GenerateQuestionText("Regenerated", q.OrderIndex)} (regen)";
        q.ExpectedAnswer = request.IncludeSampleAnswers ? "Regenerated expected answer." : null;
        q.ScoringRubric = request.IncludeScoringRubric ? "Regenerated rubric." : null;
        q.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        // Mock regen giữ TagsJson/citations cũ — chưa gọi RAG lại
        return await MapQuestionWithImageSasAsync(q, ct);
    }

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
            .Select(x => new GenerationRunDto(x.Id, x.InterviewPlanId, x.Status, x.RequestedQuestionCount, x.GeneratedQuestionCount, x.StartedAt, x.CompletedAt, x.ErrorCode, x.ErrorMessage))
            .ToListAsync(ct);
    }

    public async Task<GenerationRunDto> GetRunAsync(Guid projectId, Guid runId, Guid userId, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, false, ct);
        var row = await dbContext.QuestionGenerationRuns.FirstOrDefaultAsync(x => x.Id == runId && x.ProjectId == projectId && x.IsActive, ct)
            ?? throw new StudioBusinessException("GENERATION_RUN_NOT_FOUND", 404, "Không tìm thấy generation run.");
        return new GenerationRunDto(row.Id, row.InterviewPlanId, row.Status, row.RequestedQuestionCount, row.GeneratedQuestionCount, row.StartedAt, row.CompletedAt, row.ErrorCode, row.ErrorMessage);
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
        var settings = await dbContext.StudioSettings.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        var readiness = await BuildReadinessAsync(projectId, settings?.AppliedPlanId, ct);
        if (settings is null)
        {
            return new StudioSettingsDto(
                projectId, null, 0, 0, QuestionDifficulty.Medium, "Professional", false, false, "Markdown",
                readiness, StudioQuestionTypesHelper.DefaultTypes, StudioOutputLanguage.Vietnamese,
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

    public async Task<StudioSettingsDto> UpdateAsync(Guid projectId, Guid userId, UpdateStudioSettingsRequest request, CancellationToken ct)
    {
        await projectService.EnsureProjectAccessAsync(projectId, userId, true, ct);
        // Studio Tạo câu hỏi: số câu 5–50
        if (request.NumberOfQuestions is < 5 or > 50)
            throw new StudioBusinessException("INVALID_QUESTION_COUNT", StatusCodes.Status400BadRequest, "Số câu phải từ 5–50.");
        if (request.InterviewLengthMinutes is < 15 or > 180)
            throw new StudioBusinessException("INVALID_INTERVIEW_MINUTES", StatusCodes.Status400BadRequest, "Thời lượng phải từ 15–180 phút.");

        var settings = await dbContext.StudioSettings.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        if (settings is null)
        {
            settings = new StudioSettings { ProjectId = projectId, AppliedPlanId = null };
            dbContext.StudioSettings.Add(settings);
        }
        var types = StudioQuestionTypesHelper.Normalize(request.QuestionTypes);
        if (types.Count == 0) types = StudioQuestionTypesHelper.DefaultTypes.ToList();
        StudioQuestionTypesHelper.EnsureValidOrThrow(types);

        // FE gửi outputLanguage; một số client cũ gửi language
        var language = StudioOutputLanguage.Normalize(request.OutputLanguage ?? request.Language);

        settings.InterviewLengthMinutes = request.InterviewLengthMinutes;
        settings.NumberOfQuestions = request.NumberOfQuestions;
        settings.Difficulty = request.Difficulty;
        settings.QuestionTone = request.QuestionTone;
        settings.IncludeSampleAnswers = request.IncludeSampleAnswers;
        settings.IncludeScoringRubric = request.IncludeScoringRubric;
        settings.OutputFormat = request.OutputFormat;
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
        await dbContext.SaveChangesAsync(ct);
        var readiness = await BuildReadinessAsync(projectId, settings.AppliedPlanId, ct);
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
            language,
            settings.ContentMode,
            ParseCodeTemplatesOrDefault(settings.CodeTemplatesJson));
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

    private static StudioDocumentDto Map(StudioKnowledgeDocument x)
        => new(x.Id, x.FileName, x.FileType, x.FileSize, x.IsSelected, x.ProcessingStatus,
            x.ExtractedText is null ? null : x.ExtractedText[..Math.Min(300, x.ExtractedText.Length)],
            x.KnowledgeDocumentId);
}
