using System.Text;
using System.Text.Json;
using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.Coach;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using ApplicationLayer.Services.Coach;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services;

/// <summary>SCRUM-447: orchestration Coach competency (context → diagnostic → report → roadmap → drill → re-assessment).</summary>
public class CoachCompetencyService : ICoachCompetencyService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ICandidateProfileRepository _profiles;
    private readonly ICompetencyFrameworkRepository _frameworks;
    private readonly ICompetencyFrameworkResolver _frameworkResolver;
    private readonly ICompetencyRoleFamilyRepository _roleFamilies;
    private readonly ICompetencyResolver _competencyResolver;
    private readonly IAdaptiveBlueprintBuilder _adaptiveBlueprints;
    private readonly ICandidateAssessmentRepository _assessments;
    private readonly ICandidateRoadmapRepository _roadmaps;
    private readonly ICandidatePersonalSetJobRepository _jobs;
    private readonly IAiFeedbackRepository _feedbacks;
    private readonly ICandidateAnswerRepository _answers;
    private readonly ICandidateMarketplaceRepository _marketplace;
    private readonly IPracticeSessionRepository _practiceSessions;
    private readonly ISubscriptionGateService _gate;
    private readonly IJobScheduler _scheduler;
    private readonly ICompetencyProfileService _profile;
    private readonly IRoadmapRecommendationService _roadmapRecommendations;
    private readonly ICoachKnowledgeViewService _knowledgeView;
    private readonly IKnowledgeDocumentRepository _knowledgeDocs;
    private readonly IRoadmapNodeRepository _roadmapNodes;
    private readonly ILogger<CoachCompetencyService> _logger;

    public CoachCompetencyService(
        ICandidateProfileRepository profiles,
        ICompetencyFrameworkRepository frameworks,
        ICompetencyFrameworkResolver frameworkResolver,
        ICompetencyRoleFamilyRepository roleFamilies,
        ICompetencyResolver competencyResolver,
        IAdaptiveBlueprintBuilder adaptiveBlueprints,
        ICandidateAssessmentRepository assessments,
        ICandidateRoadmapRepository roadmaps,
        ICandidatePersonalSetJobRepository jobs,
        IAiFeedbackRepository feedbacks,
        ICandidateAnswerRepository answers,
        ICandidateMarketplaceRepository marketplace,
        IPracticeSessionRepository practiceSessions,
        ISubscriptionGateService gate,
        IJobScheduler scheduler,
        ICompetencyProfileService profile,
        IRoadmapRecommendationService roadmapRecommendations,
        ICoachKnowledgeViewService knowledgeView,
        IKnowledgeDocumentRepository knowledgeDocs,
        IRoadmapNodeRepository roadmapNodes,
        ILogger<CoachCompetencyService>? logger = null)
    {
        _profiles = profiles;
        _frameworks = frameworks;
        _frameworkResolver = frameworkResolver;
        _roleFamilies = roleFamilies;
        _competencyResolver = competencyResolver;
        _adaptiveBlueprints = adaptiveBlueprints;
        _assessments = assessments;
        _roadmaps = roadmaps;
        _jobs = jobs;
        _feedbacks = feedbacks;
        _answers = answers;
        _marketplace = marketplace;
        _practiceSessions = practiceSessions;
        _gate = gate;
        _scheduler = scheduler;
        _profile = profile;
        _roadmapRecommendations = roadmapRecommendations;
        _knowledgeView = knowledgeView;
        _knowledgeDocs = knowledgeDocs;
        _roadmapNodes = roadmapNodes;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CoachCompetencyService>.Instance;
    }

    public async Task<CoachContextDto> GetContextAsync(Guid candidateUserId)
    {
        var profile = await _profiles.GetByUserIdAsync(candidateUserId)
            ?? throw new BadRequestException("Chưa có hồ sơ ứng viên.");
        var (skills, summary) = ParseCvJson(profile.CvEvaluationJson);
        if (skills.Count == 0)
            skills = SkillMatchHelper.UnionCvSkills(profile.TechStack, profile.CvEvaluationJson).ToList();

        var resolution = await _competencyResolver.ResolveAsync(
            profile.TargetRole ?? profile.SuggestedRole,
            profile.TargetLevel ?? CoachSeniorityLevel.Junior,
            skills);
        var fw = resolution.Framework;
        var catalog = await GetFrameworkCatalogAsync();
        var roleFamilies = await GetRoleFamilyCatalogAsync();

        return new CoachContextDto
        {
            MatchedFrameworkTechnology = fw?.Technology,
            FrameworkResolved = resolution.IsFramework,
            FrameworkLevelFallback = resolution.UsedLevelFallback,
            AvailableFrameworks = catalog,
            ResolutionMode = resolution.ResolutionMode,
            RoleFamilyKey = resolution.RoleFamilyKey,
            RoleFamilyDisplay = resolution.RoleFamilyDisplay,
            ResolutionConfidence = resolution.Confidence,
            ResolutionReason = resolution.Reason,
            SupportedRoles = resolution.SupportedRoles,
            AvailableRoleFamilies = roleFamilies,
            DetectedSkills = resolution.DetectedSkills,
            SuggestedRole = profile.SuggestedRole,
            TargetRole = profile.TargetRole ?? profile.SuggestedRole,
            SelfAssessedLevel = profile.SelfAssessedLevel ?? profile.SeniorityLevel,
            TargetLevel = profile.TargetLevel ?? "Junior",
            YearsOfExperience = profile.YearsOfExperience,
            InterviewGoal = profile.InterviewGoal,
            Summary = summary,
            Skills = skills,
            ContextConfirmed = profile.CoachContextConfirmed,
            ContextConfirmedAt = profile.CoachContextConfirmedAt,
            // SCRUM-483: chỉ coi đã có CV khi có file thật — không fallback TechStack/skills
            HasCv = !string.IsNullOrWhiteSpace(profile.CvBlobPath),
            MatchedFrameworkId = fw?.Id,
            MatchedFrameworkRole = fw?.DisplayRole ?? (resolution.IsAdaptive ? resolution.NormalizedRole : null),
            MatchedFrameworkLevel = fw?.TargetLevel ?? (resolution.IsUnsupported ? null : resolution.TargetLevel)
        };
    }

    public async Task<CoachContextDto> UpdateContextAsync(Guid candidateUserId, UpdateCoachContextDto dto)
    {
        var profile = await _profiles.GetByUserIdAsync(candidateUserId)
            ?? throw new BadRequestException("Chưa có hồ sơ ứng viên.");

        ValidateLevel(dto.SelfAssessedLevel, nameof(dto.SelfAssessedLevel));
        ValidateLevel(dto.TargetLevel, nameof(dto.TargetLevel));
        ValidateLevelOrder(dto.SelfAssessedLevel, dto.TargetLevel);

        // SCRUM-494: TargetRole phải thuộc Role Family catalog (DisplayName hoặc FamilyKey)
        if (!string.IsNullOrWhiteSpace(dto.TargetRole))
        {
            var normalizedDisplay = await ResolveCatalogTargetRoleDisplayAsync(dto.TargetRole.Trim());
            profile.TargetRole = normalizedDisplay;
        }
        if (!string.IsNullOrWhiteSpace(dto.SelfAssessedLevel))
            profile.SelfAssessedLevel = dto.SelfAssessedLevel.Trim();
        if (!string.IsNullOrWhiteSpace(dto.TargetLevel))
            profile.TargetLevel = dto.TargetLevel.Trim();
        if (dto.YearsOfExperience is not null)
            profile.YearsOfExperience = dto.YearsOfExperience;
        // SCRUM-458: không ghi InterviewGoal / Skills từ Confirm Goal.
        // Skill đánh giá lấy từ CV (UnionCvSkills) + framework/KB lúc StartDiagnostic.

        profile.CoachContextConfirmed = true;
        profile.CoachContextConfirmedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;
        await _profiles.UpdateAsync(profile);
        return await GetContextAsync(candidateUserId);
    }

    /// <summary>SCRUM-463: candidate chỉnh skill trên Phân tích CV — không Confirm Goal.</summary>
    public async Task<CoachContextDto> UpdateCoachSkillsAsync(Guid candidateUserId, UpdateCoachSkillsDto dto)
    {
        var profile = await _profiles.GetByUserIdAsync(candidateUserId)
            ?? throw new BadRequestException("Chưa có hồ sơ ứng viên.");

        var normalized = NormalizeCoachSkills(dto.Skills);
        if (normalized.Count == 0)
            throw new BadRequestException("Cần ít nhất 1 công nghệ.");
        // SCRUM-504: bỏ cap 40 — CV thật dễ có nhiều hơn 40 kỹ năng và trước đây ứng viên bị kẹt
        // ở bước Phân tích CV. An toàn vì số skill đưa vào đề đã bị giới hạn sẵn
        // (SelectCoreSkills 3-5 với framework, AdaptiveBlueprintBuilder tối đa 8).
        // Guard còn lại chỉ để chặn payload bất thường, không phải giới hạn nghiệp vụ.
        if (normalized.Count > MaxCoachSkillPayload)
            throw new BadRequestException(
                $"Danh sách công nghệ quá dài (tối đa {MaxCoachSkillPayload} mục). Hãy bỏ bớt mục không liên quan.");

        // SCRUM-491: format từng skill (hybrid — không whitelist cứng)
        EnsureCoachSkillFormats(normalized);

        // SCRUM-466: skill set phải nghiêng IT
        CoachItDomainGate.EnsureItSkills(
            normalized,
            summary: null,
            suggestedRole: profile.SuggestedRole,
            targetRole: profile.TargetRole);

        profile.TechStack = normalized.ToArray();
        profile.CvEvaluationJson = MergeCvEvaluationSkills(profile.CvEvaluationJson, normalized);
        profile.UpdatedAt = DateTime.UtcNow;
        // Không đụng CoachContextConfirmed — Confirm Goal vẫn là bước riêng.
        await _profiles.UpdateAsync(profile);
        return await GetContextAsync(candidateUserId);
    }

    /// <summary>SCRUM-504: guard chống payload bất thường, không phải giới hạn nghiệp vụ.</summary>
    public const int MaxCoachSkillPayload = 200;

    /// <summary>SCRUM-493: sanitize + dedupe (giữ casing đầu) trước khi lưu / validate.</summary>
    public static List<string> NormalizeCoachSkills(IEnumerable<string>? skills)
        => CoachSkillFormat.SanitizeList(skills);

    /// <summary>SCRUM-491: reject nếu bất kỳ skill nào sai format.</summary>
    public static void EnsureCoachSkillFormats(IReadOnlyList<string> skills)
    {
        var invalid = new List<string>();
        string? firstMsg = null;
        foreach (var s in skills)
        {
            var err = CoachSkillFormat.Validate(s);
            if (err is null) continue;
            firstMsg ??= err;
            invalid.Add(s);
        }
        if (invalid.Count == 0) return;
        throw new BadRequestException(
            $"{firstMsg} Không hợp lệ: {string.Join(", ", invalid.Take(5))}.");
    }

    /// <summary>Cập nhật mảng skills trong CvEvaluationJson, giữ summary / suggestedRole / field khác.</summary>
    public static string MergeCvEvaluationSkills(string? existingJson, IReadOnlyList<string> skills)
    {
        Dictionary<string, JsonElement>? rootDict = null;
        if (!string.IsNullOrWhiteSpace(existingJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(existingJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    rootDict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                        rootDict[prop.Name] = prop.Value.Clone();
                }
            }
            catch (JsonException)
            {
                rootDict = null;
            }
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("skills");
            writer.WriteStartArray();
            foreach (var s in skills)
                writer.WriteStringValue(s);
            writer.WriteEndArray();

            if (rootDict is not null)
            {
                foreach (var (key, value) in rootDict)
                {
                    if (string.Equals(key, "skills", StringComparison.OrdinalIgnoreCase))
                        continue;
                    writer.WritePropertyName(key);
                    value.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>SCRUM-459: soft-reset vòng Coach — về Confirm Goal, giữ CV/Target Role/Level.</summary>
    public async Task<CoachContextDto> ResetCoachRunAsync(Guid candidateUserId)
    {
        var profile = await _profiles.GetByUserIdAsync(candidateUserId)
            ?? throw new BadRequestException("Chưa có hồ sơ ứng viên.");

        await FailPendingCoachJobsAsync(candidateUserId, "Đã reset bởi Chạy Coach mới.");
        await AbandonIncompleteAssessmentsAsync(candidateUserId);
        await SupersedeScoredAssessmentsAsync(candidateUserId);
        await ArchiveAllRoadmapsAsync(candidateUserId);

        profile.CoachContextConfirmed = false;
        profile.CoachContextConfirmedAt = null;
        profile.UpdatedAt = DateTime.UtcNow;
        await _profiles.UpdateAsync(profile);

        return await GetContextAsync(candidateUserId);
    }

    public async Task<CandidatePersonalSetJobDto> StartDiagnosticAsync(Guid candidateUserId, CancellationToken ct = default)
    {
        await _gate.CheckCoachGenerationAsync(candidateUserId);
        var profile = await RequireConfirmedContextAsync(candidateUserId);
        // SCRUM-483: bắt buộc file CV trước diagnostic — không cho vào bằng TechStack thủ công
        if (string.IsNullOrWhiteSpace(profile.CvBlobPath))
            throw new BadRequestException("Hãy upload CV trước khi dùng AI Coach.");
        var skills = SkillMatchHelper.UnionCvSkills(profile.TechStack, profile.CvEvaluationJson).ToList();
        if (skills.Count == 0)
            throw new BadRequestException("CV chưa có kỹ năng để đánh giá. Hãy upload lại CV hoặc chỉnh skills sau khi phân tích.");

        // SCRUM-466: skills + summary + role phải thuộc IT
        var summary = SkillMatchHelper.ParseEvaluationSummary(profile.CvEvaluationJson);
        CoachItDomainGate.EnsureItSkills(skills, summary, profile.SuggestedRole, profile.TargetRole);

        // Message phải generic theo catalog/family thật, không gợi ý cứng một stack nào.
        var resolution = await _competencyResolver.ResolveAsync(profile.TargetRole, profile.TargetLevel, skills);
        if (resolution.IsUnsupported)
            throw new BadRequestException(BuildUnsupportedMessage(profile.TargetRole, resolution.SupportedRoles));

        var policy = await _frameworks.GetPolicyAsync();
        CompetencyBlueprint competencyBlueprint;
        if (resolution.IsFramework && resolution.Framework is not null)
        {
            var fw = resolution.Framework;
            var coreSkills = SelectCoreSkills(
                fw, skills, CoachDiagnosticPolicy.MinSkills(policy), CoachDiagnosticPolicy.MaxSkills(policy));
            competencyBlueprint = FrameworkBlueprintBuilder.Build(fw, profile.TargetRole, coreSkills, policy);
        }
        else
        {
            competencyBlueprint = await _adaptiveBlueprints.BuildAsync(resolution, skills, policy, ct);
        }

        // Chạy lại: huỷ job treo, abandon assessment chưa xong, supersede báo cáo cũ, archive roadmap thế hệ trước
        await FailPendingCoachJobsAsync(candidateUserId, "Đã thay thế bởi diagnostic mới.");
        await AbandonIncompleteAssessmentsAsync(candidateUserId);
        await SupersedeScoredAssessmentsAsync(candidateUserId);
        await ArchiveAllRoadmapsAsync(candidateUserId);

        var perSkill = CoachDiagnosticPolicy.ResolveDiagnosticQuestionsPerSkill(
            policy, competencyBlueprint.Competencies.Count);
        var diagnosticPlan = DiagnosticBlueprintBuilder.BuildDiagnostic(
            competencyBlueprint, new DiagnosticBlueprintBuilder.Options(QuestionsPerSkill: perSkill));
        var scopeSkills = competencyBlueprint.Competencies.Select(s => s.SkillName).ToList();
        var snapshot = JsonSerializer.Serialize(new
        {
            targetRole = profile.TargetRole,
            targetLevel = profile.TargetLevel,
            selfAssessedLevel = profile.SelfAssessedLevel,
            skills,
            coreSkills = scopeSkills,
            frameworkId = competencyBlueprint.FrameworkId,
            resolutionMode = resolution.ResolutionMode,
            roleFamilyKey = resolution.RoleFamilyKey
        }, JsonOpts);

        var assessment = new CandidateAssessment
        {
            CandidateUserId = candidateUserId,
            FrameworkId = competencyBlueprint.FrameworkId,
            Kind = CandidateAssessmentKind.Diagnostic,
            Status = CandidateAssessmentStatus.PendingGeneration,
            ContextSnapshotJson = snapshot,
            ScopeSkillsJson = JsonSerializer.Serialize(scopeSkills, JsonOpts),
            ResolutionMode = resolution.ResolutionMode,
            RoleFamilyKey = resolution.RoleFamilyKey,
            BlueprintJson = CompetencyBlueprintJson.Serialize(competencyBlueprint),
            BlueprintSchemaVersion = competencyBlueprint.SchemaVersion
        };
        await _assessments.AddAsync(assessment);

        var jd = CvCoachPromptBuilder.BuildSyntheticJd(
            profile.TargetRole, profile.TargetLevel, summary, scopeSkills);

        var job = new CandidatePersonalSetJob
        {
            CandidateUserId = candidateUserId,
            Status = CandidatePersonalSetJobStatus.Queued,
            Purpose = CandidatePersonalSetPurpose.CvDiagnostic,
            JobDescription = jd,
            CvSkillsJson = JsonSerializer.Serialize(scopeSkills, JsonOpts),
            GapSkillsJson = "[]",
            FocusSkillsJson = "[]",
            PlanJson = JsonSerializer.Serialize(diagnosticPlan, JsonOpts),
            AssessmentId = assessment.Id
        };
        await _jobs.AddAsync(job);

        assessment.PersonalSetJobId = job.Id;
        await _assessments.UpdateAsync(assessment);

        _scheduler.EnqueueCandidatePersonalSet(job.Id);
        return MapJob(job);
    }

    public async Task<CandidatePersonalSetJobDto> StartDrillForRoadmapItemAsync(
        Guid candidateUserId, Guid roadmapId, Guid itemId, CancellationToken ct = default)
    {
        await _gate.CheckCoachGenerationAsync(candidateUserId);
        var roadmap = await _roadmaps.GetByIdAsync(roadmapId)
            ?? throw new NotFoundException("Không tìm thấy roadmap.");
        if (roadmap.CandidateUserId != candidateUserId)
            throw new ForbiddenException("Không được truy cập roadmap của ứng viên khác.");
        EnsureRoadmapAccepted(roadmap);

        var item = roadmap.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new NotFoundException("Không tìm thấy roadmap item.");
        if (item.IsReassessmentGate)
            throw new BadRequestException("Item này là cổng Re-assessment, không phải drill.");
        if (!item.IsIncluded)
            throw new BadRequestException("Topic này đã tắt khỏi lộ trình — không thể drill.");

        // SCRUM-485: đã có bộ drill chưa làm xong → trả job/set hiện có, không sinh mới
        if (item.DrillQuestionSetId is Guid existingSetId
            && item.Status == CandidateRoadmapItemStatus.InProgress
            && item.DrillSessionId is null)
        {
            var existingJob = await _jobs.GetByQuestionSetIdIncludingInactiveAsync(existingSetId);
            if (existingJob is null || existingJob.CandidateUserId != candidateUserId)
            {
                existingJob = (await _jobs.ListByCandidateAsync(candidateUserId))
                    .Where(j =>
                        j.RoadmapItemId == item.Id
                        && j.QuestionSetId == existingSetId)
                    .OrderByDescending(j => j.UpdatedAt ?? j.CreatedAt)
                    .FirstOrDefault();
            }

            if (existingJob is not null
                && existingJob.CandidateUserId == candidateUserId
                && existingJob.Status == CandidatePersonalSetJobStatus.Completed)
                return MapJob(existingJob);

            // Set đã gắn — trả payload Completed để FE/API mở practice, không enqueue job mới
            return new CandidatePersonalSetJobDto
            {
                Id = existingJob?.Id ?? existingSetId,
                Status = CandidatePersonalSetJobStatus.Completed,
                Purpose = CandidatePersonalSetPurpose.CvDrill,
                QuestionSetId = existingSetId,
                CreatedAt = existingJob?.CreatedAt ?? DateTime.UtcNow
            };
        }

        // Idempotent retry: fail job treo gắn item này rồi chạy lại
        await FailJobsForRoadmapItemAsync(candidateUserId, item.Id, "Đã thay thế bởi drill mới.");

        var profile = await RequireConfirmedContextAsync(candidateUserId);
        var policy = await _frameworks.GetPolicyAsync()
            ?? new CompetencyScoringPolicy { Id = CompetencyScoringPolicy.SingletonId };
        var focus = new[] { roadmap.Skill };
        var jd = CvCoachPromptBuilder.BuildSyntheticJd(
            profile.TargetRole, profile.TargetLevel, null, focus);
        jd += $"\nFocus topic: {item.Topic}. Current competency context score: {roadmap.CurrentScore ?? 0}.";

        // SCRUM-488: số câu theo band + remix câu yếu
        var questionCount = CoachDrillPassPolicy.ResolveQuestionCount(
            policy, roadmap.CurrentScore, roadmap.TargetScore);
        if (policy.DrillRemixEnabled && policy.DrillRemixRatio > 0)
        {
            var weakTexts = await _feedbacks.ListWeakQuestionTextsAsync(
                candidateUserId,
                roadmap.Skill,
                policy.DrillWeakAnswerScoreMaxExclusive,
                Math.Max(3, (int)Math.Ceiling(questionCount * policy.DrillRemixRatio)));
            if (weakTexts.Count > 0)
            {
                var pct = (int)Math.Round(policy.DrillRemixRatio * 100);
                jd += $"\nAbout {pct}% of questions MUST be conceptual variants of these weak prior answers "
                      + "(same skill/topic idea, DIFFERENT wording/scenario — do not copy):\n- "
                      + string.Join("\n- ", weakTexts);
            }
        }

        var plan = DiagnosticBlueprintBuilder.BuildDrill(
            roadmap.Skill,
            item.Topic,
            roadmap.CurrentScore,
            roadmap.TargetScore,
            questionCount,
            policy.DrillWeakBandRatio);
        var job = new CandidatePersonalSetJob
        {
            CandidateUserId = candidateUserId,
            Status = CandidatePersonalSetJobStatus.Queued,
            Purpose = CandidatePersonalSetPurpose.CvDrill,
            JobDescription = jd,
            CvSkillsJson = JsonSerializer.Serialize(focus, JsonOpts),
            GapSkillsJson = "[]",
            FocusSkillsJson = JsonSerializer.Serialize(focus, JsonOpts),
            PlanJson = JsonSerializer.Serialize(plan, JsonOpts),
            RoadmapItemId = item.Id
        };
        await _jobs.AddAsync(job);

        item.Status = CandidateRoadmapItemStatus.InProgress;
        await _roadmaps.UpdateAsync(roadmap);

        _scheduler.EnqueueCandidatePersonalSet(job.Id);
        return MapJob(job);
    }

    public async Task<CandidatePersonalSetJobDto> StartReassessmentAsync(
        Guid candidateUserId, Guid roadmapId, CancellationToken ct = default)
    {
        await _gate.CheckCoachGenerationAsync(candidateUserId);
        var roadmap = await _roadmaps.GetByIdAsync(roadmapId)
            ?? throw new NotFoundException("Không tìm thấy roadmap.");
        if (roadmap.CandidateUserId != candidateUserId)
            throw new ForbiddenException("Không được truy cập roadmap của ứng viên khác.");
        EnsureRoadmapAccepted(roadmap);

        var practiceItems = roadmap.Items.Where(i => !i.IsReassessmentGate && i.IsIncluded).ToList();
        if (practiceItems.Count == 0 || practiceItems.Any(i => i.Status != CandidateRoadmapItemStatus.Completed))
            throw new BadRequestException("Hoàn thành mọi topic drill trước khi Re-assessment.");

        var gate = roadmap.Items.FirstOrDefault(i => i.IsReassessmentGate)
            ?? throw new BadRequestException("Roadmap thiếu cổng Re-assessment.");

        await FailJobsForRoadmapItemAsync(candidateUserId, gate.Id, "Đã thay thế bởi re-assessment mới.");

        var profile = await RequireConfirmedContextAsync(candidateUserId);
        var sourceAssessment = roadmap.SourceAssessmentId is Guid prevRoadmapAid
            ? await _assessments.GetByIdAsync(prevRoadmapAid)
            : await _assessments.GetLatestScoredAsync(candidateUserId);
        var blueprint = CompetencyBlueprintJson.Deserialize(sourceAssessment?.BlueprintJson);

        CompetencyFramework? fw = null;
        if (roadmap.FrameworkId is Guid fwId)
            fw = await _frameworks.GetByIdWithSkillsAsync(fwId);

        CompetencyBlueprint skillBlueprint;
        if (blueprint is { Competencies.Count: > 0 })
        {
            var one = blueprint.Competencies.FirstOrDefault(c =>
                CompetencyScoringService.NormalizeSkill(c.SkillName)
                == CompetencyScoringService.NormalizeSkill(roadmap.Skill))
                ?? throw new BadRequestException("Skill không thuộc blueprint đang Active.");
            skillBlueprint = new CompetencyBlueprint
            {
                SchemaVersion = blueprint.SchemaVersion,
                SourceMode = blueprint.SourceMode,
                RoleKey = blueprint.RoleKey,
                TargetRole = blueprint.TargetRole,
                TargetLevel = blueprint.TargetLevel,
                FrameworkKey = blueprint.FrameworkKey,
                FrameworkId = blueprint.FrameworkId,
                Competencies = [one]
            };
        }
        else if (fw is not null)
        {
            var fwSkill = fw.Skills.FirstOrDefault(s =>
                CompetencyScoringService.NormalizeSkill(s.Skill)
                == CompetencyScoringService.NormalizeSkill(roadmap.Skill))
                ?? throw new BadRequestException("Skill không thuộc framework.");
            var policy = await _frameworks.GetPolicyAsync();
            skillBlueprint = FrameworkBlueprintBuilder.Build(fw, profile.TargetRole, [fwSkill], policy);
        }
        else
            throw new BadRequestException("Không có blueprint/framework để Re-assessment skill này.");

        var previous = sourceAssessment;

        // SCRUM-503: đề đo lại phải bám topic ứng viên vừa luyện trong lộ trình,
        // không dùng topic mặc định của blueprint (nguyên nhân đề "lạc đề" dù đúng skill).
        var practicedTopics = practiceItems
            .OrderBy(i => i.SortOrder)
            .Select(i => string.IsNullOrWhiteSpace(i.Subtopic)
                ? i.Topic?.Trim()
                : $"{i.Topic?.Trim()} - {i.Subtopic.Trim()}")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (practicedTopics.Count > 0 && skillBlueprint.Competencies.Count == 1)
            skillBlueprint.Competencies[0].Topics = practicedTopics;

        // SCRUM-508: số câu Đánh giá lại độc lập diagnostic (mặc định 3).
        var reassessPolicy = await _frameworks.GetPolicyAsync();
        var reassessPerSkill = CoachDiagnosticPolicy.ReassessmentQuestionsPerSkill(reassessPolicy);
        var diagnosticPlan = DiagnosticBlueprintBuilder.BuildDiagnostic(
            skillBlueprint,
            new DiagnosticBlueprintBuilder.Options(QuestionsPerSkill: reassessPerSkill));
        var snapshot = JsonSerializer.Serialize(new
        {
            targetRole = profile.TargetRole,
            targetLevel = profile.TargetLevel,
            skill = roadmap.Skill,
            previousAssessmentId = previous?.Id,
            roadmapId = roadmap.Id
        }, JsonOpts);

        var assessment = new CandidateAssessment
        {
            CandidateUserId = candidateUserId,
            FrameworkId = skillBlueprint.FrameworkId ?? fw?.Id,
            Kind = CandidateAssessmentKind.Reassessment,
            Status = CandidateAssessmentStatus.PendingGeneration,
            ContextSnapshotJson = snapshot,
            PreviousAssessmentId = previous?.Id,
            ScopeSkillsJson = JsonSerializer.Serialize(new[] { roadmap.Skill }, JsonOpts),
            ResolutionMode = sourceAssessment?.ResolutionMode
                             ?? (fw is null ? CompetencyResolutionMode.Adaptive : CompetencyResolutionMode.Framework),
            RoleFamilyKey = sourceAssessment?.RoleFamilyKey,
            BlueprintJson = CompetencyBlueprintJson.Serialize(skillBlueprint),
            BlueprintSchemaVersion = skillBlueprint.SchemaVersion
        };
        await _assessments.AddAsync(assessment);

        var jd = CvCoachPromptBuilder.BuildSyntheticJd(
            profile.TargetRole, profile.TargetLevel, null, new[] { roadmap.Skill });
        jd += "\nThis is a RE-ASSESSMENT. Generate NEW questions, do not duplicate prior diagnostic wording.";
        // SCRUM-503: chốt cứng phạm vi cho LLM — chỉ skill + topic đã luyện, cấm công nghệ gần tên.
        jd += CvCoachPromptBuilder.ReassessmentScopeNote(roadmap.Skill, practicedTopics);

        // Avoid-list: gửi kèm câu hỏi đã dùng ở các lần đo trước để RAG không hỏi lại y nguyên.
        var previousQuestions = await CollectPreviousQuestionTextsAsync(candidateUserId, roadmap.Skill);
        if (previousQuestions.Count > 0)
        {
            jd += "\nDo NOT reuse or paraphrase these previously asked questions:\n- "
                  + string.Join("\n- ", previousQuestions);
        }

        var job = new CandidatePersonalSetJob
        {
            CandidateUserId = candidateUserId,
            Status = CandidatePersonalSetJobStatus.Queued,
            Purpose = CandidatePersonalSetPurpose.CvReassessment,
            JobDescription = jd,
            CvSkillsJson = JsonSerializer.Serialize(new[] { roadmap.Skill }, JsonOpts),
            GapSkillsJson = "[]",
            FocusSkillsJson = JsonSerializer.Serialize(new[] { roadmap.Skill }, JsonOpts),
            PlanJson = JsonSerializer.Serialize(diagnosticPlan, JsonOpts),
            AssessmentId = assessment.Id,
            RoadmapItemId = gate.Id
        };
        await _jobs.AddAsync(job);
        assessment.PersonalSetJobId = job.Id;
        await _assessments.UpdateAsync(assessment);

        gate.Status = CandidateRoadmapItemStatus.InProgress;
        await _roadmaps.UpdateAsync(roadmap);

        _scheduler.EnqueueCandidatePersonalSet(job.Id);
        return MapJob(job);
    }

    public async Task<CoachScreeningPreviewDto> GetScreeningPreviewAsync(
        Guid candidateUserId, CancellationToken ct = default)
    {
        var policy = await _frameworks.GetPolicyAsync();
        if (!CoachDiagnosticPolicy.ScreeningEnabled(policy))
        {
            return new CoachScreeningPreviewDto
            {
                Enabled = false,
                Message = "Bài sàng lọc đang tắt trên cấu hình Admin."
            };
        }

        var ctx = await TryBuildScreeningContextAsync(candidateUserId, policy);
        if (ctx is null)
        {
            return new CoachScreeningPreviewDto
            {
                Enabled = true,
                Message = "Hãy hoàn thành bài chẩn đoán trước khi mở rộng độ phủ."
            };
        }

        var perSkill = CoachDiagnosticPolicy.ScreeningQuestionsPerSkill(policy);
        return new CoachScreeningPreviewDto
        {
            Enabled = true,
            Available = ctx.Skills.Count > 0,
            QuestionCount = ctx.Skills.Count * perSkill,
            QuestionsPerSkill = perSkill,
            Skills = ctx.Skills,
            MeasuredSkills = ctx.Measured,
            RemainingUnmeasured = ctx.RemainingUnmeasured,
            Message = ctx.Skills.Count == 0
                ? "Mọi kỹ năng trên CV đã được đo hoặc đã có lộ trình."
                : null
        };
    }

    public async Task<CandidatePersonalSetJobDto> StartScreeningAsync(
        Guid candidateUserId, CancellationToken ct = default)
    {
        await _gate.CheckCoachGenerationAsync(candidateUserId);
        var policy = await _frameworks.GetPolicyAsync();
        if (!CoachDiagnosticPolicy.ScreeningEnabled(policy))
            throw new BadRequestException("Bài sàng lọc đang tắt trên cấu hình Admin.");

        var ctx = await TryBuildScreeningContextAsync(candidateUserId, policy)
            ?? throw new BadRequestException("Hãy hoàn thành bài chẩn đoán trước khi mở rộng độ phủ.");
        if (ctx.Skills.Count == 0)
            throw new BadRequestException("Không còn kỹ năng CV nào để sàng lọc.");

        var profile = await RequireConfirmedContextAsync(candidateUserId);
        var perSkill = CoachDiagnosticPolicy.ScreeningQuestionsPerSkill(policy);
        var targetScore = CompetencyTargetScorePolicy.Resolve(policy, profile.TargetLevel);
        var sourceMode = ctx.Framework is null
            ? CompetencyResolutionMode.Adaptive
            : CompetencyResolutionMode.Framework;
        var skillBlueprint = DiagnosticBlueprintBuilder.BuildScreeningBlueprint(
            ctx.Skills,
            profile.TargetRole,
            profile.TargetLevel,
            targetScore,
            ctx.Framework?.Id,
            sourceMode);

        if (ctx.Framework is not null)
        {
            foreach (var c in skillBlueprint.Competencies)
            {
                var fwSkill = ctx.Framework.Skills.FirstOrDefault(s =>
                    CompetencyScoringService.NormalizeSkill(s.Skill)
                    == CompetencyScoringService.NormalizeSkill(c.SkillName));
                if (fwSkill is null) continue;
                var topics = CompetencyTopicParser.ParseTopicNames(fwSkill.TopicsJson);
                if (topics.Count > 0) c.Topics = topics.Take(3).ToList();
                if (fwSkill.TargetScore > 0) c.TargetScore = fwSkill.TargetScore;
                c.Weight = fwSkill.ImportanceWeight;
                c.Source = CompetencySourceMode.Framework;
            }
            AdaptiveBlueprintBuilder.RenormalizeWeights(skillBlueprint.Competencies);
        }

        // Chỉ huỷ job sàng lọc treo — giữ diagnostic đã chấm và drill đang chạy.
        await FailPendingScreeningJobsAsync(candidateUserId, "Đã thay thế bởi bài sàng lọc mới.");
        var pendingScreening = (await _assessments.ListByCandidateAsync(candidateUserId))
            .Where(a => a.Kind == CandidateAssessmentKind.Screening
                        && a.Status is CandidateAssessmentStatus.PendingGeneration
                            or CandidateAssessmentStatus.ReadyToPractice
                            or CandidateAssessmentStatus.InProgress)
            .ToList();
        foreach (var a in pendingScreening)
        {
            a.Status = CandidateAssessmentStatus.Abandoned;
            await _assessments.UpdateAsync(a);
        }

        var screeningPlan = DiagnosticBlueprintBuilder.BuildScreening(skillBlueprint, perSkill);
        var snapshot = JsonSerializer.Serialize(new
        {
            targetRole = profile.TargetRole,
            targetLevel = profile.TargetLevel,
            skills = ctx.CvSkills,
            screeningSkills = ctx.Skills,
            sourceAssessmentId = ctx.Source.Id,
            kind = CandidateAssessmentKind.Screening
        }, JsonOpts);

        var assessment = new CandidateAssessment
        {
            CandidateUserId = candidateUserId,
            FrameworkId = skillBlueprint.FrameworkId,
            Kind = CandidateAssessmentKind.Screening,
            Status = CandidateAssessmentStatus.PendingGeneration,
            ContextSnapshotJson = snapshot,
            ScopeSkillsJson = JsonSerializer.Serialize(ctx.Skills, JsonOpts),
            ResolutionMode = ctx.Source.ResolutionMode ?? sourceMode,
            RoleFamilyKey = ctx.Source.RoleFamilyKey,
            BlueprintJson = CompetencyBlueprintJson.Serialize(skillBlueprint),
            BlueprintSchemaVersion = skillBlueprint.SchemaVersion
        };
        await _assessments.AddAsync(assessment);

        var jd = CvCoachPromptBuilder.BuildSyntheticJd(
            profile.TargetRole, profile.TargetLevel, null, ctx.Skills);
        jd += "\nThis is a SCREENING test: one question per skill. Stay inside the listed skills only.";

        var job = new CandidatePersonalSetJob
        {
            CandidateUserId = candidateUserId,
            Status = CandidatePersonalSetJobStatus.Queued,
            Purpose = CandidatePersonalSetPurpose.CvScreening,
            JobDescription = jd,
            CvSkillsJson = JsonSerializer.Serialize(ctx.Skills, JsonOpts),
            GapSkillsJson = "[]",
            FocusSkillsJson = JsonSerializer.Serialize(ctx.Skills, JsonOpts),
            PlanJson = JsonSerializer.Serialize(screeningPlan, JsonOpts),
            AssessmentId = assessment.Id
        };
        await _jobs.AddAsync(job);
        assessment.PersonalSetJobId = job.Id;
        await _assessments.UpdateAsync(assessment);

        _scheduler.EnqueueCandidatePersonalSet(job.Id);
        return MapJob(job);
    }

    /// <summary>SCRUM-507: tổng kết vòng Coach — chỉ đầy đủ khi mọi roadmap Accepted đã Completed.</summary>
    public async Task<CoachWrapUpDto> GetWrapUpAsync(Guid candidateUserId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var profile = await _profile.GetProfileAsync(candidateUserId);
        var planItems = profile?.Items?.ToList() ?? new List<CandidateSkillPlanItem>();

        var roadmaps = await _roadmaps.ListByCandidateAsync(candidateUserId);
        var accepted = roadmaps.Where(r => r.AcceptedAt is not null).ToList();

        var policy = await _frameworks.GetPolicyAsync();
        var passMin = policy.DrillPassScoreExclusiveMin > 0
            ? policy.DrillPassScoreExclusiveMin
            : CoachDrillPassPolicy.DefaultPassScoreExclusiveMin;

        CoachWrapUpSnapshot? snapshot = null;
        var latest = await _assessments.GetLatestScoredAsync(candidateUserId);
        if (latest is not null)
        {
            var mapped = await MapAssessmentAsync(latest);
            snapshot = new CoachWrapUpSnapshot(
                mapped.OverallReadiness,
                mapped.OverallDelta,
                mapped.AchievedLevel,
                mapped.TargetReadinessStatus,
                mapped.SuggestedNextLevel,
                mapped.SuggestedNextLevelAvailable,
                mapped.SuggestedNextLevelMessage);
        }

        // Điểm mọi lần nộp drill theo item — dùng để nhận topic hay sai dù lần sau đã pass.
        var attemptScoresByItemId = new Dictionary<Guid, IReadOnlyList<double>>();
        var setIds = accepted
            .SelectMany(r => r.Items)
            .Where(i => !i.IsReassessmentGate && i.DrillQuestionSetId is Guid)
            .Select(i => i.DrillQuestionSetId!.Value)
            .Distinct()
            .ToList();
        if (setIds.Count > 0)
        {
            var attempts = await _practiceSessions.ListCompletedByQuestionSetIdsAsync(candidateUserId, setIds);
            var bySet = attempts
                .GroupBy(a => a.QuestionSetId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<double>)g
                        .Where(a => a.Score is not null)
                        .Select(a => a.Score!.Value)
                        .ToList());

            foreach (var item in accepted.SelectMany(r => r.Items)
                         .Where(i => !i.IsReassessmentGate && i.DrillQuestionSetId is Guid))
            {
                if (bySet.TryGetValue(item.DrillQuestionSetId!.Value, out var scores) && scores.Count > 0)
                    attemptScoresByItemId[item.Id] = scores;
            }
        }

        return CoachWrapUpBuilder.Build(
            planItems,
            accepted,
            passMin,
            snapshot,
            attemptScoresByItemId);
    }

    private sealed record ScreeningContext(
        CandidateAssessment Source,
        CompetencyFramework? Framework,
        List<string> CvSkills,
        List<string> Measured,
        List<string> Skills,
        int RemainingUnmeasured);

    private async Task<ScreeningContext?> TryBuildScreeningContextAsync(
        Guid candidateUserId, CompetencyScoringPolicy policy)
    {
        var list = await _assessments.ListByCandidateAsync(candidateUserId);
        var source = list
            .Where(a => a.Status == CandidateAssessmentStatus.Scored
                        && a.Kind == CandidateAssessmentKind.Diagnostic)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefault();
        if (source is null)
            return null;

        var profile = await _profiles.GetByUserIdAsync(candidateUserId);
        if (profile is null) return null;

        var cvSkills = SkillMatchHelper.UnionCvSkills(profile.TechStack, profile.CvEvaluationJson).ToList();
        var measured = ParseScopeSkills(source.ScopeSkillsJson).ToList();
        if (measured.Count == 0)
            measured = source.SkillResults.Select(r => r.Skill).ToList();

        var roadmaps = await _roadmaps.ListByCandidateAsync(candidateUserId);
        var roadmapSkills = roadmaps.Select(r => r.Skill).ToList();

        CompetencyFramework? fw = null;
        if (source.FrameworkId is Guid fwId)
            fw = await _frameworks.GetByIdWithSkillsAsync(fwId);

        var max = CoachDiagnosticPolicy.ScreeningMaxSkills(policy);
        var selected = CoachScreeningPlanner.SelectSkills(cvSkills, measured, roadmapSkills, fw, max);
        var remaining = CoachScreeningPlanner.SelectSkills(cvSkills, measured, roadmapSkills, fw, 999).Count;
        return new ScreeningContext(source, fw, cvSkills, measured, selected, remaining);
    }

    public async Task<CoachAssessmentDto?> GetLatestReportAsync(Guid candidateUserId)
    {
        var a = await _assessments.GetLatestScoredAsync(candidateUserId);
        if (a is null) return null;

        var profile = await _profile.GetProfileAsync(candidateUserId);
        if (profile is null || profile.Items.Count == 0)
            return await MapAssessmentAsync(a);

        return await MapReportFromProfileAsync(a, profile);
    }

    public async Task<IReadOnlyList<CoachAssessmentDto>> GetHistoryAsync(Guid candidateUserId)
    {
        var list = await _assessments.ListByCandidateAsync(candidateUserId);
        // SCRUM-459: lịch sử gồm Scored hiện tại + Superseded (vòng cũ).
        var scored = list
            .Where(a => a.Status is CandidateAssessmentStatus.Scored
                or CandidateAssessmentStatus.Superseded)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .ToList();
        var result = new List<CoachAssessmentDto>(scored.Count);
        foreach (var a in scored)
            result.Add(await MapAssessmentAsync(a));
        return result;
    }

    public async Task<CoachAssessmentDto?> GetAssessmentAsync(Guid candidateUserId, Guid assessmentId)
    {
        var a = await _assessments.GetByIdAsync(assessmentId);
        if (a is null) return null;
        if (a.CandidateUserId != candidateUserId)
            throw new ForbiddenException("Không được xem assessment của ứng viên khác.");
        return await MapAssessmentAsync(a);
    }

    public async Task<IReadOnlyList<CoachRoadmapDto>> ListRoadmapsAsync(Guid candidateUserId)
    {
        var rows = await _roadmaps.ListByCandidateAsync(candidateUserId);
        var assessments = await _assessments.ListByCandidateAsync(candidateUserId);
        // SCRUM-484: DisplayOrder (candidate sắp xếp) rồi PriorityScore.
        var ordered = rows
            .OrderBy(r => r.DisplayOrder)
            .ThenByDescending(r => r.PriorityScore)
            .ToList();
        var result = new List<CoachRoadmapDto>(ordered.Count);
        foreach (var row in ordered)
            result.Add(await MapRoadmapHydratedAsync(row, assessments));
        return result;
    }

    public async Task<CoachRoadmapDto> StartRoadmapAsync(Guid candidateUserId, Guid roadmapId)
    {
        var roadmap = await _roadmaps.GetByIdAsync(roadmapId)
            ?? throw new NotFoundException("Không tìm thấy roadmap.");
        if (roadmap.CandidateUserId != candidateUserId)
            throw new ForbiddenException("Không được truy cập roadmap của ứng viên khác.");
        EnsureRoadmapAccepted(roadmap);
        roadmap.Status = CandidateRoadmapStatus.Active;
        if (roadmap.Items.Where(i => i.IsIncluded && !i.IsReassessmentGate)
            .All(i => i.Status == CandidateRoadmapItemStatus.Pending))
        {
            var first = roadmap.Items
                .Where(i => !i.IsReassessmentGate && i.IsIncluded)
                .OrderBy(i => i.SortOrder)
                .FirstOrDefault();
            if (first is not null)
                first.Status = CandidateRoadmapItemStatus.InProgress;
        }
        await _roadmaps.UpdateAsync(roadmap);
        return await MapRoadmapHydratedAsync(roadmap, await _assessments.ListByCandidateAsync(candidateUserId));
    }

    /// <summary>SCRUM-462 / SCRUM-484: toggle + reorder topic/skill trên draft Suggested chưa Accept.</summary>
    public async Task<IReadOnlyList<CoachRoadmapDto>> UpdateRoadmapDraftAsync(
        Guid candidateUserId, UpdateRoadmapDraftDto dto)
    {
        var hasItemPatches = dto.Items.Count > 0;
        var hasRoadmapPatches = dto.Roadmaps.Count > 0;
        if (!hasItemPatches && !hasRoadmapPatches)
            return await ListRoadmapsAsync(candidateUserId);

        var roadmaps = await _roadmaps.ListByCandidateAsync(candidateUserId);
        var drafts = roadmaps
            .Where(r => r.Status == CandidateRoadmapStatus.Suggested && r.AcceptedAt is null)
            .ToList();
        if (drafts.Count == 0)
            throw new BadRequestException("Không có lộ trình draft để chỉnh. Hãy Accept hoặc làm diagnostic lại.");

        var draftById = drafts.ToDictionary(r => r.Id);
        var itemMap = drafts
            .SelectMany(r => r.Items.Select(i => (Roadmap: r, Item: i)))
            .ToDictionary(x => x.Item.Id, x => x);

        foreach (var patch in dto.Items)
        {
            if (!itemMap.TryGetValue(patch.ItemId, out var pair))
                throw new BadRequestException($"Item {patch.ItemId} không thuộc lộ trình draft của bạn.");
            if (pair.Item.IsReassessmentGate)
            {
                // Gate luôn included; không cho reorder.
                if (patch.IsIncluded == false)
                    throw new BadRequestException("Không thể tắt cổng Re-assessment.");
                pair.Item.IsIncluded = true;
                continue;
            }
            if (patch.IsIncluded is bool included)
                pair.Item.IsIncluded = included;
            if (patch.SortOrder is int sortOrder)
                pair.Item.SortOrder = sortOrder;
        }

        // Gate luôn đứng cuối mỗi skill sau khi reorder topic.
        foreach (var roadmap in drafts)
        {
            var learnMax = roadmap.Items
                .Where(i => !i.IsReassessmentGate)
                .Select(i => i.SortOrder)
                .DefaultIfEmpty(0)
                .Max();
            foreach (var gate in roadmap.Items.Where(i => i.IsReassessmentGate))
                gate.SortOrder = Math.Max(gate.SortOrder, learnMax + 1);
        }

        foreach (var patch in dto.Roadmaps)
        {
            if (!draftById.TryGetValue(patch.RoadmapId, out var roadmap))
                throw new BadRequestException($"Roadmap {patch.RoadmapId} không thuộc lộ trình draft của bạn.");
            roadmap.DisplayOrder = patch.DisplayOrder;
        }

        foreach (var roadmap in drafts)
            await _roadmaps.UpdateAsync(roadmap);

        return await ListRoadmapsAsync(candidateUserId);
    }

    /// <summary>SCRUM-462: Accept draft → Active; skill không còn topic included → archive.</summary>
    public async Task<IReadOnlyList<CoachRoadmapDto>> AcceptRoadmapsAsync(
        Guid candidateUserId, AcceptRoadmapsDto? dto = null)
    {
        var roadmaps = await _roadmaps.ListByCandidateAsync(candidateUserId);
        var drafts = roadmaps
            .Where(r => r.Status == CandidateRoadmapStatus.Suggested && r.AcceptedAt is null)
            .ToList();
        if (drafts.Count == 0)
            throw new BadRequestException("Không có lộ trình Suggested để chấp nhận.");

        // ≥1 skill còn ít nhất 1 topic học (không tính gate).
        var learnable = drafts
            .Where(r => r.Items.Any(i => !i.IsReassessmentGate && i.IsIncluded))
            .ToList();
        if (learnable.Count == 0)
            throw new BadRequestException("Cần chọn ít nhất 1 skill còn topic để học trước khi chấp nhận lộ trình.");

        var now = DateTime.UtcNow;
        foreach (var roadmap in drafts)
        {
            var hasLearnTopic = roadmap.Items.Any(i => !i.IsReassessmentGate && i.IsIncluded);
            if (!hasLearnTopic)
            {
                // Soft-remove skill khỏi lộ trình học.
                roadmap.IsActive = false;
                await _roadmaps.UpdateAsync(roadmap);
                continue;
            }

            // Gate luôn included.
            foreach (var gate in roadmap.Items.Where(i => i.IsReassessmentGate))
                gate.IsIncluded = true;

            roadmap.AcceptedAt = now;
            roadmap.Status = CandidateRoadmapStatus.Active;
            var first = roadmap.Items
                .Where(i => !i.IsReassessmentGate && i.IsIncluded)
                .OrderBy(i => i.SortOrder)
                .FirstOrDefault();
            if (first is not null && first.Status == CandidateRoadmapItemStatus.Pending)
                first.Status = CandidateRoadmapItemStatus.InProgress;
            await _roadmaps.UpdateAsync(roadmap);
        }

        return await ListRoadmapsAsync(candidateUserId);
    }

    private static void EnsureRoadmapAccepted(CandidateRoadmap roadmap)
    {
        if (roadmap.AcceptedAt is null)
            throw new BadRequestException("Hãy chấp nhận lộ trình trước khi bắt đầu luyện tập.");
    }

    public async Task<CoachRoadmapDto> GetRoadmapAsync(Guid candidateUserId, Guid roadmapId)
    {
        var roadmap = await _roadmaps.GetByIdAsync(roadmapId)
            ?? throw new NotFoundException("Không tìm thấy roadmap.");
        if (roadmap.CandidateUserId != candidateUserId)
            throw new ForbiddenException("Không được truy cập roadmap của ứng viên khác.");
        return await MapRoadmapHydratedAsync(roadmap, await _assessments.ListByCandidateAsync(candidateUserId));
    }

    public async Task<CoachScoreResult> ScoreAssessmentFromSessionAsync(PracticeSession session)
    {
        var assessment = await _assessments.GetByQuestionSetIdAsync(session.QuestionSetId);
        if (assessment is null)
        {
            var job = await _jobs.GetByQuestionSetIdIncludingInactiveAsync(session.QuestionSetId);
            if (job?.AssessmentId is Guid aid)
                assessment = await _assessments.GetByIdAsync(aid);
        }
        if (assessment is null)
        {
            var owned = await _assessments.ListByCandidateAsync(session.CandidateUserId);
            assessment = owned.FirstOrDefault(a => a.QuestionSetId == session.QuestionSetId);
        }
        if (assessment is null)
            return CoachScoreResult.Skipped("ASSESSMENT_NOT_FOUND");
        if (assessment.CandidateUserId != session.CandidateUserId)
            return CoachScoreResult.Skipped("ASSESSMENT_OWNER_MISMATCH");
        if (assessment.Kind is not (CandidateAssessmentKind.Diagnostic
            or CandidateAssessmentKind.Reassessment
            or CandidateAssessmentKind.Screening))
            return CoachScoreResult.Skipped($"KIND_NOT_SCOREABLE:{assessment.Kind}");

        CompetencyFramework? fw = assessment.Framework;
        if (fw is null && assessment.FrameworkId is Guid fwId)
            fw = await _frameworks.GetByIdWithSkillsAsync(fwId);
        var blueprint = CompetencyBlueprintJson.Deserialize(assessment.BlueprintJson);

        var policy = await _frameworks.GetPolicyAsync();
        var questions = await _marketplace.GetQuestionsSnapshotAsync(session.QuestionSetId);
        var answers = await _answers.GetEntitiesBySessionIdAsync(session.Id);
        var feedbacks = await _feedbacks.GetBySessionIdAsync(session.Id);
        var fbByAnswer = feedbacks.ToDictionary(f => f.CandidateAnswerId);

        var inputs = new List<CompetencyScoringService.AnswerInput>();
        foreach (var ans in answers)
        {
            var q = questions.FirstOrDefault(x => x.Id == ans.QuestionSetQuestionId);
            if (q is null) continue;
            fbByAnswer.TryGetValue(ans.Id, out var fb);
            var succeeded = fb?.EvaluationStatus == AiFeedbackEvaluationStatus.Succeeded;
            Dictionary<string, double>? dims = null;
            if (!string.IsNullOrWhiteSpace(fb?.DimensionScoresJson))
            {
                try
                {
                    dims = JsonSerializer.Deserialize<Dictionary<string, double>>(fb.DimensionScoresJson, JsonOpts);
                }
                catch (JsonException) { /* ignore */ }
            }
            dims = CompetencyScoringService.ExtractDimensions(dims);
            // Fallback: nếu thiếu dimension, map score LLM vào correctness (legacy)
            if (dims is null && succeeded && fb?.Score is double legacy)
            {
                dims = new Dictionary<string, double>
                {
                    ["correctness"] = legacy,
                    ["relevance"] = legacy,
                    ["clarity"] = legacy
                };
            }

            inputs.Add(new CompetencyScoringService.AnswerInput(
                q.Skill ?? "general",
                q.Difficulty ?? "medium",
                dims?.GetValueOrDefault("correctness"),
                dims?.GetValueOrDefault("relevance"),
                dims?.GetValueOrDefault("clarity"),
                succeeded));
        }

        // Assessment chỉ chấm các skill nằm trong phạm vi đo (ScopeSkillsJson);
        // Overall của toàn bộ năng lực được tính lại ở CompetencyProfileService trên profile tích luỹ.
        var scope = ParseScopeSkills(assessment.ScopeSkillsJson);
        if (scope.Count == 0)
            scope = inputs
                .Select(i => CompetencyScoringService.NormalizeSkill(i.Skill))
                .Where(s => s.Length > 0)
                .ToHashSet();

        var scoringSkills = CompetencyProfileService.ResolveScoringSkills(fw, blueprint);
        if (scoringSkills.Count == 0)
            scoringSkills = CompetencyProfileService.FallbackScoringSkills(
                scope, policy, blueprint?.TargetLevel ?? fw?.TargetLevel);
        if (scoringSkills.Count == 0 && questions.Count > 0)
            scoringSkills = CompetencyProfileService.FallbackScoringSkills(
                questions.Select(q => q.Skill ?? "general"),
                policy,
                blueprint?.TargetLevel ?? fw?.TargetLevel);
        if (scoringSkills.Count == 0)
            return CoachScoreResult.Skipped("NO_SCORING_SKILLS");

        var skillsToScore = scoringSkills
            .Where(s => scope.Contains(CompetencyScoringService.NormalizeSkill(s.Skill)))
            .ToList();
        if (skillsToScore.Count == 0)
            skillsToScore = scoringSkills.ToList();

        var overall = CompetencyScoringService.ComputeOverall(policy, skillsToScore, inputs);

        var newSkillResults = overall.Skills
            .GroupBy(s => CompetencyScoringService.NormalizeSkill(s.Skill))
            .Select(g => g.First())
            .Select(r =>
            {
                var skillName = r.Skill.Length > 200 ? r.Skill[..200] : r.Skill;
                return new CandidateAssessmentSkillResult
                {
                    AssessmentId = assessment.Id,
                    Skill = skillName,
                    SkillScore = r.SkillScore,
                    TargetScore = r.TargetScore,
                    Gap = r.Gap,
                    ImportanceWeight = r.ImportanceWeight,
                    DemonstratedDifficulty = r.DemonstratedDifficulty,
                    EvidenceJson = JsonSerializer.Serialize(r.Evidence ?? Array.Empty<object>(), JsonOpts)
                };
            })
            .ToList();

        assessment.PracticeSessionId = session.Id;
        assessment.QuestionSetId = session.QuestionSetId;
        assessment.Status = CandidateAssessmentStatus.Scored;
        if (assessment.ScopeSkillsJson is null)
            assessment.ScopeSkillsJson = JsonSerializer.Serialize(
                overall.Skills.Select(s => s.Skill).ToList(), JsonOpts);

        // Persist Scored + skill results qua RemoveRange/Add — tránh DbUpdateConcurrencyException.
        await _assessments.SaveScoredAssessmentAsync(assessment, newSkillResults);

        CandidateSkillPlan profileForRoadmap;
        var isScreening = assessment.Kind == CandidateAssessmentKind.Screening;
        if (isScreening)
        {
            // SCRUM-506: sàng lọc không merge profile — level/điểm tổng giữ nguyên từ diagnostic.
            assessment.OverallReadiness = overall.OverallReadiness;
            assessment.ReadinessStatus = overall.ReadinessStatus;
            assessment.ExplanationJson = JsonSerializer.Serialize(new
            {
                kind = CandidateAssessmentKind.Screening,
                scopeSkills = skillsToScore.Select(s => s.Skill).ToList(),
                note = "Screening không cập nhật level."
            }, JsonOpts);
            try
            {
                await _assessments.UpdateReadinessAsync(
                    assessment.Id,
                    assessment.OverallReadiness,
                    assessment.ReadinessStatus,
                    assessment.ExplanationJson);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Không cập nhật OverallReadiness cho screening {AssessmentId}", assessment.Id);
            }
            profileForRoadmap = new CandidateSkillPlan
            {
                Items = assessment.SkillResults.Select(r => new CandidateSkillPlanItem
                {
                    Skill = r.Skill,
                    CurrentScore = r.SkillScore,
                    TargetScore = r.TargetScore,
                    ImportanceWeight = r.ImportanceWeight
                }).ToList()
            };
        }
        else
        try
        {
            var merge = await _profile.MergeAsync(session.CandidateUserId, assessment, fw, blueprint);
            assessment.OverallReadiness = merge.Profile.OverallReadiness ?? overall.OverallReadiness;
            assessment.ReadinessStatus = merge.Profile.ReadinessStatus ?? overall.ReadinessStatus;
            assessment.ExplanationJson = JsonSerializer.Serialize(new
            {
                readinessStatus = assessment.ReadinessStatus,
                achievedLevel = merge.AchievedLevel,
                estimatedBand = merge.AchievedLevel,
                levelExplanation = merge.LevelExplanation,
                levelCriteria = merge.LevelCriteria,
                coverageRatio = merge.CoverageRatio,
                scopeSkills = skillsToScore.Select(s => s.Skill).ToList(),
                previousOverall = merge.PreviousOverall,
                overallDelta = merge.OverallDelta,
                skillDeltas = merge.SkillDeltas,
                targetLevel = blueprint?.TargetLevel ?? fw?.TargetLevel
            }, JsonOpts);
            await _assessments.UpdateReadinessAsync(
                assessment.Id,
                assessment.OverallReadiness,
                assessment.ReadinessStatus,
                assessment.ExplanationJson);
            profileForRoadmap = merge.Profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Merge competency profile thất bại sau khi assessment {AssessmentId} đã Scored — vẫn dựng roadmap từ skill results.",
                assessment.Id);
            assessment.OverallReadiness ??= overall.OverallReadiness;
            assessment.ReadinessStatus ??= overall.ReadinessStatus;
            try
            {
                await _assessments.UpdateReadinessAsync(
                    assessment.Id,
                    assessment.OverallReadiness,
                    assessment.ReadinessStatus,
                    assessment.ExplanationJson);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Không cập nhật OverallReadiness cho assessment {AssessmentId}", assessment.Id);
            }
            profileForRoadmap = new CandidateSkillPlan
            {
                Items = assessment.SkillResults.Select(r => new CandidateSkillPlanItem
                {
                    Skill = r.Skill,
                    CurrentScore = r.SkillScore,
                    TargetScore = r.TargetScore,
                    ImportanceWeight = r.ImportanceWeight
                }).ToList()
            };
        }

        var roadmapUpdated = false;
        try
        {
            if (assessment.Kind == CandidateAssessmentKind.Reassessment)
            {
                await _roadmapRecommendations.RefreshAfterReassessmentAsync(
                    session.CandidateUserId, assessment, fw, profileForRoadmap);
                await CompleteReassessmentRoadmapAsync(session);
            }
            else if (assessment.Kind == CandidateAssessmentKind.Screening)
            {
                await _roadmapRecommendations.AddFromScreeningAsync(
                    session.CandidateUserId, assessment, fw, profileForRoadmap);
            }
            else
            {
                await _roadmapRecommendations.RebuildFromDiagnosticAsync(
                    session.CandidateUserId, assessment, fw, profileForRoadmap);
            }
            roadmapUpdated = true;
        }
        catch (Exception ex)
        {
            // Báo cáo đã persist. Lỗi roadmap không rollback Scored — Rescore phải rebuild lại được.
            _logger.LogError(ex,
                "Dựng roadmap thất bại sau assessment {AssessmentId} (session {SessionId}) — report vẫn Scored.",
                assessment.Id, session.Id);
        }

        return CoachScoreResult.Success(assessment.Id, assessment.Status, roadmapUpdated);
    }

    public async Task HandleDrillSessionCompletedAsync(PracticeSession session)
    {
        var job = await _jobs.GetByQuestionSetIdAsync(session.QuestionSetId);
        if (job is null || job.Purpose != CandidatePersonalSetPurpose.CvDrill)
            return;
        if (job.RoadmapItemId is not Guid itemId)
            return;

        var roadmaps = await _roadmaps.ListByCandidateAsync(session.CandidateUserId);
        var roadmap = roadmaps.FirstOrDefault(r => r.Items.Any(i => i.Id == itemId));
        var item = roadmap?.Items.FirstOrDefault(i => i.Id == itemId);
        if (roadmap is null || item is null) return;

        item.DrillSessionId = session.Id;
        item.DrillQuestionSetId = session.QuestionSetId;
        item.DrillScore = session.OverallScore;

        // SCRUM-487/488: chỉ qua topic khi điểm > ngưỡng Admin (DrillPassScoreExclusiveMin)
        var policy = await _frameworks.GetPolicyAsync()
            ?? new CompetencyScoringPolicy { Id = CompetencyScoringPolicy.SingletonId };
        var passMin = policy.DrillPassScoreExclusiveMin > 0
            ? policy.DrillPassScoreExclusiveMin
            : CoachDrillPassPolicy.DefaultPassScoreExclusiveMin;
        if (!CoachDrillPassPolicy.IsPassing(session.OverallScore, passMin))
        {
            item.Status = CandidateRoadmapItemStatus.InProgress;
            await _roadmaps.UpdateAsync(roadmap);
            return;
        }

        item.Status = CandidateRoadmapItemStatus.Completed;

        var remaining = roadmap.Items.Where(i => !i.IsReassessmentGate && i.IsIncluded).ToList();
        if (remaining.Count > 0 && remaining.All(i => i.Status == CandidateRoadmapItemStatus.Completed))
        {
            var gate = roadmap.Items.FirstOrDefault(i => i.IsReassessmentGate);
            if (gate is not null)
                gate.Status = CandidateRoadmapItemStatus.ReadyForReassessment;
            roadmap.Status = CandidateRoadmapStatus.Active;
        }

        await _roadmaps.UpdateAsync(roadmap);
        // Drill score KHÔNG ghi competency / skill plan
    }

    private async Task<List<string>> CollectPreviousQuestionTextsAsync(Guid candidateUserId, string skill)
    {
        var key = CompetencyScoringService.NormalizeSkill(skill);
        var assessments = await _assessments.ListByCandidateAsync(candidateUserId);
        var texts = new List<string>();
        foreach (var a in assessments.Where(x =>
                     x.Status == CandidateAssessmentStatus.Scored && x.QuestionSetId is Guid))
        {
            var questions = await _marketplace.GetQuestionsSnapshotAsync(a.QuestionSetId!.Value);
            foreach (var q in questions)
            {
                if (CompetencyScoringService.NormalizeSkill(q.Skill ?? "") != key) continue;
                var text = (q.Question ?? "").Trim();
                if (text.Length == 0) continue;
                if (text.Length > 240) text = text[..240];
                if (!texts.Contains(text, StringComparer.OrdinalIgnoreCase))
                    texts.Add(text);
                if (texts.Count >= 12) return texts;
            }
        }
        return texts;
    }

    public async Task<CandidatePersonalSetJobDto> CancelActiveJobAsync(Guid candidateUserId, Guid jobId)
    {
        var job = await _jobs.GetByIdAsync(jobId)
            ?? throw new NotFoundException("Không tìm thấy job.");
        if (job.CandidateUserId != candidateUserId)
            throw new ForbiddenException("Không được huỷ job của ứng viên khác.");

        var status = job.Status;
        if (status is not (CandidatePersonalSetJobStatus.Queued or CandidatePersonalSetJobStatus.Generating))
            throw new BadRequestException("Chỉ huỷ được job đang xếp hàng hoặc đang sinh đề.");

        job.Status = CandidatePersonalSetJobStatus.Failed;
        job.ErrorMessage = "Đã huỷ bởi người dùng.";
        await _jobs.UpdateAsync(job);
        await ApplyGenerationSideEffectsAsync(job, abandonAssessment: true);
        return MapJob(job);
    }

    public async Task MarkGenerationFailedAsync(Guid jobId)
    {
        var job = await _jobs.GetByIdAsync(jobId);
        if (job is null) return;
        // Job đã Failed trong catch của PersonalSetService — chỉ reset assessment/item
        await ApplyGenerationSideEffectsAsync(job, abandonAssessment: false);
    }

    public async Task AttachQuestionSetToRoadmapItemAsync(
        Guid candidateUserId, Guid roadmapItemId, Guid questionSetId)
    {
        var roadmaps = await _roadmaps.ListByCandidateAsync(candidateUserId);
        var roadmap = roadmaps.FirstOrDefault(r => r.Items.Any(i => i.Id == roadmapItemId));
        var item = roadmap?.Items.FirstOrDefault(i => i.Id == roadmapItemId);
        if (roadmap is null || item is null) return;

        item.DrillQuestionSetId = questionSetId;
        // Giữ InProgress nếu đang chờ làm bài; không đổi Completed.
        if (item.Status is CandidateRoadmapItemStatus.Pending
            or CandidateRoadmapItemStatus.ReadyForReassessment)
            item.Status = CandidateRoadmapItemStatus.InProgress;

        await _roadmaps.UpdateAsync(roadmap);
    }

    /// <summary>
    /// abandonAssessment=true → Abandoned (user cancel / diagnostic mới).
    /// abandonAssessment=false → Failed (RAG lỗi).
    /// </summary>
    private async Task ApplyGenerationSideEffectsAsync(CandidatePersonalSetJob job, bool abandonAssessment)
    {
        if (job.AssessmentId is Guid assessmentId)
        {
            var assessment = await _assessments.GetByIdAsync(assessmentId);
            if (assessment is not null
                && assessment.Status is CandidateAssessmentStatus.PendingGeneration
                    or CandidateAssessmentStatus.ReadyToPractice
                    or CandidateAssessmentStatus.InProgress)
            {
                assessment.Status = abandonAssessment
                    ? CandidateAssessmentStatus.Abandoned
                    : CandidateAssessmentStatus.Failed;
                await _assessments.UpdateAsync(assessment);
            }
        }

        if (job.RoadmapItemId is Guid itemId)
            await ResetRoadmapItemAfterFailedJobAsync(job.CandidateUserId, itemId);
    }

    private async Task ResetRoadmapItemAfterFailedJobAsync(Guid candidateUserId, Guid itemId)
    {
        var roadmaps = await _roadmaps.ListByCandidateAsync(candidateUserId);
        var roadmap = roadmaps.FirstOrDefault(r => r.Items.Any(i => i.Id == itemId));
        var item = roadmap?.Items.FirstOrDefault(i => i.Id == itemId);
        if (roadmap is null || item is null) return;

        if (item.IsReassessmentGate)
        {
            var othersDone = roadmap.Items
                .Where(i => !i.IsReassessmentGate)
                .All(i => i.Status == CandidateRoadmapItemStatus.Completed);
            item.Status = othersDone
                ? CandidateRoadmapItemStatus.ReadyForReassessment
                : CandidateRoadmapItemStatus.Pending;
        }
        else if (item.Status == CandidateRoadmapItemStatus.InProgress
                 && item.DrillSessionId is null)
        {
            item.Status = CandidateRoadmapItemStatus.Pending;
        }

        await _roadmaps.UpdateAsync(roadmap);
    }

    private async Task FailPendingCoachJobsAsync(Guid candidateUserId, string reason)
    {
        var jobs = await _jobs.ListByCandidateAsync(candidateUserId);
        foreach (var row in jobs.Where(j =>
                     IsCoachPurpose(j.Purpose)
                     && j.Status is CandidatePersonalSetJobStatus.Queued
                         or CandidatePersonalSetJobStatus.Generating))
        {
            var tracked = await _jobs.GetByIdAsync(row.Id);
            if (tracked is null) continue;
            tracked.Status = CandidatePersonalSetJobStatus.Failed;
            tracked.ErrorMessage = reason;
            await _jobs.UpdateAsync(tracked);
            await ApplyGenerationSideEffectsAsync(tracked, abandonAssessment: true);
        }
    }

    private async Task FailJobsForRoadmapItemAsync(Guid candidateUserId, Guid roadmapItemId, string reason)
    {
        var jobs = await _jobs.ListByCandidateAsync(candidateUserId);
        foreach (var row in jobs.Where(j =>
                     j.RoadmapItemId == roadmapItemId
                     && j.Status is CandidatePersonalSetJobStatus.Queued
                         or CandidatePersonalSetJobStatus.Generating))
        {
            var tracked = await _jobs.GetByIdAsync(row.Id);
            if (tracked is null) continue;
            tracked.Status = CandidatePersonalSetJobStatus.Failed;
            tracked.ErrorMessage = reason;
            await _jobs.UpdateAsync(tracked);
            await ApplyGenerationSideEffectsAsync(tracked, abandonAssessment: true);
        }
    }

    private async Task AbandonIncompleteAssessmentsAsync(Guid candidateUserId)
    {
        var list = await _assessments.ListByCandidateAsync(candidateUserId);
        foreach (var a in list.Where(x =>
                     x.Status is CandidateAssessmentStatus.PendingGeneration
                         or CandidateAssessmentStatus.ReadyToPractice
                         or CandidateAssessmentStatus.InProgress))
        {
            a.Status = CandidateAssessmentStatus.Abandoned;
            await _assessments.UpdateAsync(a);
        }
    }

    /// <summary>SCRUM-459: đánh dấu assessment đã Scored thành Superseded để GetLatestReport trả null.</summary>
    private async Task SupersedeScoredAssessmentsAsync(Guid candidateUserId)
    {
        var list = await _assessments.ListByCandidateAsync(candidateUserId);
        foreach (var a in list.Where(x => x.Status == CandidateAssessmentStatus.Scored))
        {
            a.Status = CandidateAssessmentStatus.Superseded;
            a.UpdatedAt = DateTime.UtcNow;
            await _assessments.UpdateAsync(a);
        }
    }

    private Task ArchiveAllRoadmapsAsync(Guid candidateUserId)
        => _roadmaps.ArchiveActiveByCandidateAsync(candidateUserId);

    private static bool IsCoachPurpose(string purpose)
        => CandidatePersonalSetPurpose.IsCoach(purpose);

    private async Task FailPendingScreeningJobsAsync(Guid candidateUserId, string reason)
    {
        var jobs = await _jobs.ListByCandidateAsync(candidateUserId);
        foreach (var row in jobs.Where(j =>
                     j.Purpose == CandidatePersonalSetPurpose.CvScreening
                     && j.Status is CandidatePersonalSetJobStatus.Queued
                         or CandidatePersonalSetJobStatus.Generating))
        {
            var tracked = await _jobs.GetByIdAsync(row.Id);
            if (tracked is null) continue;
            tracked.Status = CandidatePersonalSetJobStatus.Failed;
            tracked.ErrorMessage = reason;
            await _jobs.UpdateAsync(tracked);
            await ApplyGenerationSideEffectsAsync(tracked, abandonAssessment: true);
        }
    }

    private async Task CompleteReassessmentRoadmapAsync(PracticeSession session)
    {
        var job = await _jobs.GetByQuestionSetIdAsync(session.QuestionSetId);
        if (job?.RoadmapItemId is not Guid itemId) return;
        var roadmaps = await _roadmaps.ListByCandidateAsync(session.CandidateUserId);
        var roadmap = roadmaps.FirstOrDefault(r => r.Items.Any(i => i.Id == itemId));
        var item = roadmap?.Items.FirstOrDefault(i => i.Id == itemId);
        if (roadmap is null || item is null) return;
        item.Status = CandidateRoadmapItemStatus.Completed;
        item.DrillSessionId = session.Id;
        item.DrillScore = session.OverallScore;
        roadmap.Status = CandidateRoadmapStatus.Completed;
        await _roadmaps.UpdateAsync(roadmap);
    }

    private async Task<CoachAssessmentDto> MapReportFromProfileAsync(
        CandidateAssessment latest, CandidateSkillPlan profile)
    {
        var dto = await MapAssessmentAsync(latest);
        dto.OverallReadiness = profile.OverallReadiness ?? dto.OverallReadiness;
        dto.ReadinessStatus = profile.ReadinessStatus ?? dto.ReadinessStatus;
        dto.AchievedLevel = profile.AchievedLevel;
        dto.Skills = profile.Items
            .Where(i => i.CurrentScore is not null)
            .Select(i =>
            {
                var score = i.CurrentScore ?? 0;
                var gap = Math.Round(i.TargetScore - score, 2);
                string band;
                if (score >= i.TargetScore) band = "strength";
                else if (gap >= 15) band = "critical_gap";
                else band = "needs_improvement";
                return new CoachSkillResultDto
                {
                    Skill = i.Skill,
                    SkillScore = score,
                    TargetScore = i.TargetScore,
                    Gap = gap,
                    ImportanceWeight = i.ImportanceWeight,
                    DemonstratedDifficulty = i.DemonstratedDifficulty,
                    Band = band,
                    Source = i.UpdatedFromKind
                };
            })
            .OrderByDescending(s => s.Gap)
            .ToList();

        try
        {
            if (!string.IsNullOrWhiteSpace(latest.ExplanationJson))
                ApplyExplanationFields(dto, latest.ExplanationJson);
        }
        catch (JsonException)
        {
            // explanation json không phải object — giữ DTO đã map.
        }

        // Không bao giờ trả raw ExplanationJson ra FE (tránh dump JSON trên báo cáo).
        dto.Explanation = null;

        await ApplyTargetReadinessAsync(dto, latest);
        return dto;
    }

    private async Task<CandidateProfile> RequireConfirmedContextAsync(Guid candidateUserId)
    {
        var profile = await _profiles.GetByUserIdAsync(candidateUserId)
            ?? throw new BadRequestException("Chưa có hồ sơ ứng viên.");
        if (!profile.CoachContextConfirmed)
            throw new BadRequestException("Hãy xác nhận mục tiêu Coach (target role/level/skills) trước khi bắt đầu diagnostic.");
        return profile;
    }

    private static List<CompetencyFrameworkSkill> SelectCoreSkills(
        CompetencyFramework fw, IReadOnlyList<string> cvSkills, int min, int max)
    {
        var cvNorm = cvSkills.Select(CompetencyScoringService.NormalizeSkill).ToHashSet();
        var matched = fw.Skills
            .Where(s => cvNorm.Contains(CompetencyScoringService.NormalizeSkill(s.Skill)))
            .OrderByDescending(s => s.ImportanceWeight)
            .ToList();
        if (matched.Count >= min)
            return matched.Take(max).ToList();

        // Bổ sung skill framework còn thiếu theo importance
        var extra = fw.Skills
            .OrderByDescending(s => s.ImportanceWeight)
            .Where(s => matched.All(m => m.Id != s.Id));
        matched.AddRange(extra);
        return matched.Take(Math.Max(min, Math.Min(max, matched.Count))).ToList();
    }

    private static void ValidateLevel(string? level, string field)
    {
        if (string.IsNullOrWhiteSpace(level)) return;
        var ok = new[] { "Fresher", "Junior", "Middle", "Senior" };
        if (!ok.Any(x => string.Equals(x, level.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new BadRequestException($"{field} phải là Fresher/Junior/Middle/Senior.");
    }

    /// <summary>
    /// Mục tiêu coaching không được thấp hơn cấp tự đánh giá
    /// (vd. hiện tại Junior mà mục tiêu Fresher là ngược logic).
    /// </summary>
    private static void ValidateLevelOrder(string? selfAssessed, string? target)
    {
        if (string.IsNullOrWhiteSpace(selfAssessed) || string.IsNullOrWhiteSpace(target))
            return;
        if (LevelRank(target) < LevelRank(selfAssessed))
            throw new BadRequestException(
                "Cấp độ mục tiêu phải bằng hoặc cao hơn cấp độ hiện tại.");
    }

    private static int LevelRank(string level)
    {
        var order = new[] { "Fresher", "Junior", "Middle", "Senior" };
        for (var i = 0; i < order.Length; i++)
        {
            if (string.Equals(order[i], level.Trim(), StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }

    private static (List<string> Skills, string? Summary) ParseCvJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (new List<string>(), null);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var skills = new List<string>();
            if (root.TryGetProperty("skills", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in arr.EnumerateArray())
                {
                    var s = x.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(s)) skills.Add(s);
                }
            }
            string? summary = root.TryGetProperty("summary", out var sum) ? sum.GetString() : null;
            return (skills, summary);
        }
        catch (JsonException)
        {
            return (new List<string>(), null);
        }
    }

    /// <summary>
    /// Đọc TopicsJson của framework skill. Hỗ trợ cả 2 dạng dữ liệu:
    /// mảng string cũ ["OOP","LINQ"] và dạng có subtopic [{ "topic": "...", "subtopics": [...] }].
    /// Nhờ vậy import curated data mới không cần backfill dữ liệu đã seed.
    /// </summary>
    private static List<string> ParseTopics(string? json)
        => CompetencyTopicParser.ParseTopicNames(json);

    /// <summary>
    /// Đọc ScopeSkillsJson của assessment (full vs partial). Skill được normalize
    /// để khớp với framework bất kể viết hoa/thường.
    /// </summary>
    private static HashSet<string> ParseScopeSkills(string? json)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? new List<string>();
            foreach (var s in list)
                if (!string.IsNullOrWhiteSpace(s))
                    result.Add(CompetencyScoringService.NormalizeSkill(s));
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
        return result;
    }

    public Task<List<CoachFrameworkOptionDto>> ListFrameworkCatalogAsync() => GetFrameworkCatalogAsync();

    /// <summary>SCRUM-494: catalog Role Family cho FE dropdown.</summary>
    private async Task<List<CoachRoleFamilyOptionDto>> GetRoleFamilyCatalogAsync()
    {
        var families = await _roleFamilies.ListActiveAsync() ?? new List<CompetencyRoleFamily>();
        return families.Select(f => new CoachRoleFamilyOptionDto
        {
            FamilyKey = f.FamilyKey,
            DisplayName = f.DisplayName,
            GroupName = f.GroupName
        }).ToList();
    }

    /// <summary>
    /// Map TargetRole (FamilyKey hoặc DisplayName) → DisplayName catalog.
    /// Throw nếu không thuộc catalog Active.
    /// </summary>
    private async Task<string> ResolveCatalogTargetRoleDisplayAsync(string raw)
    {
        var families = await _roleFamilies.ListActiveAsync() ?? new List<CompetencyRoleFamily>();
        if (families.Count == 0)
            throw new BadRequestException(
                "Chưa cấu hình catalog vai trò (Role Family). Liên hệ Admin seed trước khi xác nhận mục tiêu.");

        var match = families.FirstOrDefault(f =>
            string.Equals(f.DisplayName, raw, StringComparison.OrdinalIgnoreCase)
            || string.Equals(f.FamilyKey, raw, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            throw new BadRequestException(
                "Vị trí mục tiêu phải chọn từ catalog vai trò được hỗ trợ (không nhập tự do).");

        return match.DisplayName;
    }

    /// <summary>Catalog framework để FE gợi ý Target Role và để message lỗi không hardcode stack.</summary>
    private async Task<List<CoachFrameworkOptionDto>> GetFrameworkCatalogAsync()
    {
        var entries = await _frameworkResolver.GetCatalogAsync();
        return entries.Select(e => new CoachFrameworkOptionDto
        {
            RoleKey = e.RoleKey,
            DisplayRole = e.DisplayRole,
            Technology = e.Technology,
            Levels = e.Levels,
            Provenance = e.Provenance
        }).ToList();
    }

    private static string BuildNoFrameworkMessage(string? targetRole, List<CoachFrameworkOptionDto> catalog)
    {
        var role = string.IsNullOrWhiteSpace(targetRole) ? "(chưa chọn)" : targetRole.Trim();
        if (catalog.Count == 0)
            return $"Chưa có competency framework nào được cấu hình cho vai trò \"{role}\". "
                   + "Liên hệ Admin để import framework trước khi dùng AI Coach.";

        var options = string.Join("; ", catalog.Select(c =>
            $"{c.DisplayRole} ({string.Join("/", c.Levels)})"));
        return $"Chưa có framework cho vai trò \"{role}\". "
               + $"Hãy chọn một trong các vai trò đang hỗ trợ: {options}. "
               + "Hoặc liên hệ Admin bổ sung framework cho vai trò của bạn.";
    }

    private static string BuildUnsupportedMessage(string? targetRole, IReadOnlyList<string> supported)
    {
        var role = string.IsNullOrWhiteSpace(targetRole) ? "(chưa chọn)" : targetRole.Trim();
        var list = supported.Count == 0
            ? "các vai trò software engineering đã cấu hình"
            : string.Join("; ", supported);
        return $"Vai trò \"{role}\" hiện ngoài domain software engineering mà IQGS hỗ trợ. "
               + $"Các nhóm đang hỗ trợ: {list}.";
    }

    private async Task ApplyTargetReadinessAsync(CoachAssessmentDto dto, CandidateAssessment a)
    {
        var policy = await _frameworks.GetPolicyAsync();
        var blueprint = CompetencyBlueprintJson.Deserialize(a.BlueprintJson);
        var targetLevel = blueprint?.TargetLevel ?? a.Framework?.TargetLevel;
        string? band = dto.AchievedLevel ?? dto.EstimatedBand;
        dto.ResolutionMode = a.ResolutionMode;
        TargetReadinessMapper.Apply(dto, policy, targetLevel, band);

        // SCRUM-461: gợi ý level kế khi READY (confirm trên FE → PUT context + diagnostic).
        var next = NextLevelSuggestion.TryGetNextLevel(dto.TargetLevel);
        var nextExists = false;
        if (next is not null
            && string.Equals(a.ResolutionMode, CompetencyResolutionMode.Framework, StringComparison.OrdinalIgnoreCase))
        {
            var roleKey = a.Framework?.RoleKey
                ?? blueprint?.RoleKey;
            if (!string.IsNullOrWhiteSpace(roleKey))
            {
                var fwNext = await _frameworks.GetByRoleAndLevelAsync(roleKey!, next);
                nextExists = fwNext is not null;
            }
        }

        var suggestion = NextLevelSuggestion.Build(
            dto.TargetReadinessStatus,
            dto.TargetLevel,
            a.ResolutionMode,
            nextExists);
        NextLevelSuggestion.ApplyToDto(dto, suggestion);
    }

    private async Task<CoachAssessmentDto> MapAssessmentAsync(CandidateAssessment a)
    {
        CoachAssessmentDto? prev = null;
        if (a.PreviousAssessmentId is Guid pid)
        {
            var p = await _assessments.GetByIdAsync(pid);
            if (p is not null)
                prev = new CoachAssessmentDto { OverallReadiness = p.OverallReadiness };
        }

        string Band(CandidateAssessmentSkillResult r)
        {
            if (r.SkillScore >= r.TargetScore) return "strength";
            if (r.Gap >= 15) return "critical_gap";
            return "needs_improvement";
        }

        var dto = new CoachAssessmentDto
        {
            Id = a.Id,
            Kind = a.Kind,
            Status = a.Status,
            JobId = a.PersonalSetJobId,
            QuestionSetId = a.QuestionSetId,
            PracticeSessionId = a.PracticeSessionId,
            OverallReadiness = a.OverallReadiness,
            ReadinessStatus = a.ReadinessStatus,
            MeetsJuniorReadyRule = a.ExplanationJson?.Contains("true", StringComparison.OrdinalIgnoreCase) == true
                && a.ExplanationJson.Contains("meetsJuniorReadyRule", StringComparison.OrdinalIgnoreCase),
            FrameworkDisplayRole = a.Framework?.DisplayRole,
            FrameworkTargetLevel = a.Framework?.TargetLevel,
            ResolutionMode = a.ResolutionMode,
            Skills = a.SkillResults.Select(r => new CoachSkillResultDto
            {
                Skill = r.Skill,
                SkillScore = r.SkillScore,
                TargetScore = r.TargetScore,
                Gap = r.Gap,
                ImportanceWeight = r.ImportanceWeight,
                DemonstratedDifficulty = r.DemonstratedDifficulty,
                Band = Band(r)
            }).OrderByDescending(s => s.Gap).ToList(),
            // ExplanationJson là blob nội bộ — parse sang field có cấu trúc, không dump JSON.
            Explanation = null,
            PreviousOverallReadiness = prev?.OverallReadiness,
            OverallDelta = a.OverallReadiness is double cur && prev?.OverallReadiness is double old
                ? Math.Round(cur - old, 2)
                : null,
            AchievedLevel = null,
            LevelExplanation = null
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(a.ExplanationJson))
                ApplyExplanationFields(dto, a.ExplanationJson);
        }
        catch (JsonException)
        {
            // Không phải JSON object — bỏ qua.
        }

        await ApplyTargetReadinessAsync(dto, a);
        return dto;
    }

    /// <summary>
    /// Tách ExplanationJson (merge snapshot) ra các field DTO cho báo cáo FE.
    /// Không gán raw JSON vào Explanation.
    /// </summary>
    private static void ApplyExplanationFields(CoachAssessmentDto dto, string explanationJson)
    {
        using var doc = JsonDocument.Parse(explanationJson);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return;

        if (root.TryGetProperty("levelExplanation", out var exp))
            dto.LevelExplanation = exp.GetString();
        if (root.TryGetProperty("levelCriteria", out var crit) && crit.ValueKind == JsonValueKind.Object)
            dto.LevelCriteria = ParseLevelCriteria(crit);
        if (root.TryGetProperty("coverageRatio", out var cov) && cov.TryGetDouble(out var c))
            dto.CoverageRatio = c;
        if (root.TryGetProperty("previousOverall", out var prev) && prev.TryGetDouble(out var p))
            dto.PreviousOverallReadiness ??= p;
        if (root.TryGetProperty("overallDelta", out var d) && d.TryGetDouble(out var delta))
            dto.OverallDelta ??= delta;
        if (root.TryGetProperty("achievedLevel", out var lvl) && string.IsNullOrWhiteSpace(dto.AchievedLevel))
            dto.AchievedLevel = lvl.GetString();
        if (root.TryGetProperty("estimatedBand", out var band) && string.IsNullOrWhiteSpace(dto.EstimatedBand))
            dto.EstimatedBand = band.GetString();
        if (root.TryGetProperty("targetLevel", out var tl) && string.IsNullOrWhiteSpace(dto.TargetLevel))
            dto.TargetLevel = tl.GetString();
        if (root.TryGetProperty("readinessStatus", out var rs) && string.IsNullOrWhiteSpace(dto.ReadinessStatus))
            dto.ReadinessStatus = rs.GetString();
    }

    /// <summary>SCRUM-509: đọc levelCriteria từ ExplanationJson (report cũ thiếu → null).</summary>
    private static CoachLevelCriteriaDto? ParseLevelCriteria(JsonElement crit)
    {
        static double? Num(JsonElement el, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (el.TryGetProperty(k, out var p) && p.TryGetDouble(out var v))
                    return v;
            }
            return null;
        }

        var overall = Num(crit, "overall", "Overall");
        var overallTh = Num(crit, "overallThreshold", "OverallThreshold");
        if (overall is null && overallTh is null) return null;

        return new CoachLevelCriteriaDto
        {
            Overall = overall ?? 0,
            OverallThreshold = overallTh ?? 0,
            TargetMetRatio = Num(crit, "targetMetRatio", "TargetMetRatio") ?? 0,
            TargetMetThreshold = Num(crit, "targetMetThreshold", "TargetMetThreshold") ?? 0,
            RequiredRatio = Num(crit, "requiredRatio", "RequiredRatio") ?? 0,
            RequiredThreshold = Num(crit, "requiredThreshold", "RequiredThreshold") ?? 0,
            HardRatio = Num(crit, "hardRatio", "HardRatio") ?? 0,
            HardThreshold = Num(crit, "hardThreshold", "HardThreshold") ?? 0
        };
    }

    private static CoachRoadmapDto MapRoadmap(CandidateRoadmap r) => MapRoadmapCore(r, null);

    /// <summary>
    /// Hydrate DrillQuestionSetId cho cổng Re-assessment đang InProgress nếu sinh đề xong
    /// nhưng chưa gắn (job cũ trước khi AttachQuestionSet). FE cần id để hiện CTA mở bài.
    /// SCRUM-486/501: resolve canViewSource từ AllowCandidateView; auto-link node thiếu KnowledgeDocumentId.
    /// </summary>
    private async Task<CoachRoadmapDto> MapRoadmapHydratedAsync(
        CandidateRoadmap r, IReadOnlyList<CandidateAssessment> assessments)
    {
        // SCRUM-501: Admin đã bật AllowCandidateView nhưng node chưa gắn doc → link theo filename rồi map.
        await TryLinkAllowViewDocsForRoadmapAsync(r);

        var docIds = r.Items
            .Select(i => i.RoadmapNode?.KnowledgeDocumentId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var allowMap = await _knowledgeView.GetAllowCandidateViewMapAsync(docIds);

        var dto = MapRoadmapCore(r, allowMap);
        foreach (var item in dto.Items.Where(i =>
                     i.IsReassessmentGate
                     && i.Status == CandidateRoadmapItemStatus.InProgress
                     && i.DrillQuestionSetId is null))
        {
            var ready = assessments
                .Where(a =>
                    a.Kind == CandidateAssessmentKind.Reassessment
                    && a.Status == CandidateAssessmentStatus.ReadyToPractice
                    && a.QuestionSetId is not null
                    && a.IsActive)
                .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
                .FirstOrDefault(a => AssessmentMatchesRoadmapSkill(a, r.Skill));
            if (ready?.QuestionSetId is Guid qid)
                item.DrillQuestionSetId = qid;
        }

        // SCRUM-489: hydrate mọi phiên COMPLETED trên DrillQuestionSetId (xem lại lần 1…N)
        var setIds = dto.Items
            .Where(i => i.DrillQuestionSetId is Guid)
            .Select(i => i.DrillQuestionSetId!.Value)
            .Distinct()
            .ToList();
        if (setIds.Count > 0)
        {
            var attempts = await _practiceSessions.ListCompletedByQuestionSetIdsAsync(
                r.CandidateUserId, setIds);
            var bySet = attempts
                .GroupBy(a => a.QuestionSetId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderBy(a => a.CompletedAt ?? DateTime.MinValue)
                        .ThenBy(a => a.SessionId)
                        .Select(a => new CoachDrillAttemptDto
                        {
                            SessionId = a.SessionId,
                            Score = a.Score,
                            CompletedAt = a.CompletedAt
                        }).ToList());
            foreach (var item in dto.Items)
            {
                if (item.DrillQuestionSetId is Guid sid
                    && bySet.TryGetValue(sid, out var list))
                    item.DrillAttempts = list;
            }
        }

        // SCRUM-488: FE hiển thị hint pass theo ngưỡng Admin
        var policy = await _frameworks.GetPolicyAsync()
            ?? new CompetencyScoringPolicy { Id = CompetencyScoringPolicy.SingletonId };
        dto.DrillPassScoreExclusiveMin = policy.DrillPassScoreExclusiveMin > 0
            ? policy.DrillPassScoreExclusiveMin
            : CoachDrillPassPolicy.DefaultPassScoreExclusiveMin;
        return dto;
    }

    private static bool AssessmentMatchesRoadmapSkill(CandidateAssessment a, string skill)
    {
        if (string.IsNullOrWhiteSpace(a.ScopeSkillsJson)) return true;
        try
        {
            var scope = JsonSerializer.Deserialize<List<string>>(a.ScopeSkillsJson, JsonOpts) ?? new();
            if (scope.Count == 0) return true;
            var key = CompetencyScoringService.NormalizeSkill(skill);
            return scope.Any(s => CompetencyScoringService.NormalizeSkill(s) == key);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static CoachRoadmapDto MapRoadmapCore(
        CandidateRoadmap r,
        IReadOnlyDictionary<Guid, bool>? allowViewByDocId)
    {
        var (skillSource, outsideCvReason) =
            RoadmapRecommendationService.ParseSkillProvenanceFromJson(r.ExplanationJson);
        var adaptive = string.Equals(r.SourceMode, CompetencySourceMode.RagDynamic, StringComparison.OrdinalIgnoreCase);
        return new()
        {
            Id = r.Id,
            Skill = r.Skill,
            CurrentScore = r.CurrentScore,
            TargetScore = r.TargetScore,
            Gap = r.Gap,
            PriorityScore = r.PriorityScore,
            Kind = r.Kind,
            Priority = r.Priority,
            Status = r.Status,
            SourceMode = r.SourceMode,
            Explanation = FormatRoadmapExplanation(r.ExplanationJson),
            KbSource = ParseRoadmapKbSource(r.ExplanationJson),
            AcceptedAt = r.AcceptedAt,
            SkillSource = skillSource,
            OutsideCvReason = outsideCvReason,
            DisplayOrder = r.DisplayOrder,
            Confidence = ParseRoadmapConfidence(r.ExplanationJson),
            Items = r.Items.OrderBy(i => i.SortOrder).Select(i =>
            {
                var docId = i.RoadmapNode?.KnowledgeDocumentId;
                var canView = docId is Guid kid
                    && allowViewByDocId is not null
                    && allowViewByDocId.TryGetValue(kid, out var allowed)
                    && allowed;
                return new CoachRoadmapItemDto
                {
                    Id = i.Id,
                    Topic = i.Topic,
                    Subtopic = i.Subtopic,
                    SortOrder = i.SortOrder,
                    Status = i.Status,
                    IsReassessmentGate = i.IsReassessmentGate,
                    IsIncluded = i.IsIncluded,
                    TopicReason = i.IsReassessmentGate
                        ? null
                        : (!string.IsNullOrWhiteSpace(i.SourceTitle)
                            ? i.SourceTitle
                            : adaptive
                                ? $"Suy từ blueprint cho skill {r.Skill}"
                                : $"Topic curated cho skill {r.Skill}"),
                    DrillScore = i.DrillScore,
                    DrillQuestionSetId = i.DrillQuestionSetId,
                    DrillSessionId = i.DrillSessionId,
                    SourceUrl = i.SourceUrl ?? i.RoadmapNode?.SourceUrl,
                    SourceTitle = i.SourceTitle ?? i.RoadmapNode?.SourceTitle,
                    KnowledgeDocumentId = docId,
                    CanViewSource = canView,
                    Prerequisites = ParseStringList(i.RoadmapNode?.PrerequisitesJson),
                    NextTopics = ParseStringList(i.RoadmapNode?.NextTopicsJson)
                };
            }).ToList()
        };
    }

    /// <summary>
    /// SCRUM-501: nếu item gắn RoadmapNode nhưng chưa có KnowledgeDocumentId,
    /// thử link SYSTEM doc AllowCandidateView theo FileName khớp SourceUrl/SourceTitle.
    /// </summary>
    private async Task TryLinkAllowViewDocsForRoadmapAsync(CandidateRoadmap r)
    {
        var needsLink = r.Items.Any(i =>
            i.RoadmapNode != null && !i.RoadmapNode.KnowledgeDocumentId.HasValue
            && (!string.IsNullOrWhiteSpace(i.RoadmapNode.SourceUrl)
                || !string.IsNullOrWhiteSpace(i.RoadmapNode.SourceTitle)
                || !string.IsNullOrWhiteSpace(i.SourceUrl)
                || !string.IsNullOrWhiteSpace(i.SourceTitle)));
        if (!needsLink) return;

        IReadOnlyList<(Guid Id, string FileName)> allowDocs;
        try
        {
            allowDocs = await _knowledgeDocs.ListSystemAllowCandidateViewAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SCRUM-501: không tải được danh sách AllowCandidateView docs.");
            return;
        }

        if (allowDocs.Count == 0) return;

        foreach (var doc in allowDocs)
        {
            try
            {
                var linked = await _roadmapNodes.LinkByFilenameAsync(doc.Id, doc.FileName);
                if (linked.Count == 0) continue;

                foreach (var item in r.Items)
                {
                    if (item.RoadmapNode != null
                        && linked.Contains(item.RoadmapNode.Id)
                        && !item.RoadmapNode.KnowledgeDocumentId.HasValue)
                    {
                        item.RoadmapNode.KnowledgeDocumentId = doc.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "SCRUM-501: link-by-filename thất bại cho doc {DocId} / {File}",
                    doc.Id, doc.FileName);
            }
        }
    }

    /// <summary>
    /// ExplanationJson lưu {"reason":"..."} — FE chỉ cần chuỗi reason, không dump JSON.
    /// </summary>
    private static string? FormatRoadmapExplanation(string? explanationJson)
    {
        if (string.IsNullOrWhiteSpace(explanationJson)) return null;
        var trimmed = explanationJson.Trim();
        if (!trimmed.StartsWith('{'))
            return trimmed;
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.TryGetProperty("reason", out var reason))
            {
                var text = reason.GetString()?.Trim();
                if (!string.IsNullOrEmpty(text)) return text;
            }
            if (doc.RootElement.TryGetProperty("explanation", out var exp))
            {
                var text = exp.GetString()?.Trim();
                if (!string.IsNullOrEmpty(text)) return text;
            }
        }
        catch (JsonException)
        {
            /* giữ nguyên nếu không parse được */
        }
        return trimmed;
    }

    /// <summary>SCRUM-506: screening = tín hiệu 1 câu.</summary>
    private static string? ParseRoadmapConfidence(string? explanationJson)
    {
        if (string.IsNullOrWhiteSpace(explanationJson)) return null;
        var trimmed = explanationJson.Trim();
        if (!trimmed.StartsWith('{')) return null;
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.TryGetProperty("confidence", out var c)
                || doc.RootElement.TryGetProperty("Confidence", out c))
            {
                var v = c.GetString()?.Trim();
                return string.IsNullOrEmpty(v) ? null : v;
            }
        }
        catch (JsonException) { /* roadmap cũ không có field */ }
        return null;
    }

    /// <summary>Đọc kbSource từ ExplanationJson; thiếu → inferred (an toàn cho roadmap cũ).</summary>
    private static string ParseRoadmapKbSource(string? explanationJson)
    {
        if (string.IsNullOrWhiteSpace(explanationJson))
            return CoachRoadmapKnowledgeFolder.KbSourceInferred;
        var trimmed = explanationJson.Trim();
        if (!trimmed.StartsWith('{'))
            return CoachRoadmapKnowledgeFolder.KbSourceInferred;
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.TryGetProperty("kbSource", out var ks) ||
                doc.RootElement.TryGetProperty("KbSource", out ks))
            {
                var v = ks.GetString()?.Trim().ToLowerInvariant();
                if (v == CoachRoadmapKnowledgeFolder.KbSourceSystem)
                    return CoachRoadmapKnowledgeFolder.KbSourceSystem;
            }
        }
        catch (JsonException)
        {
            /* inferred */
        }
        return CoachRoadmapKnowledgeFolder.KbSourceInferred;
    }

    private static List<string> ParseStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static CandidatePersonalSetJobDto MapJob(CandidatePersonalSetJob job) => new()
    {
        Id = job.Id,
        Status = job.Status,
        Purpose = job.Purpose,
        QuestionSetId = job.QuestionSetId,
        ErrorMessage = job.ErrorMessage,
        KbSource = CoachDiagnosticKnowledgeFolder.ParseKbSourceFromGapSkillsJson(job.GapSkillsJson),
        CreatedAt = job.CreatedAt
    };

    /// <summary>SCRUM-486: ủy quyền xem nguồn KB cho Candidate.</summary>
    public Task<CoachKnowledgeViewDto> GetKnowledgeSourceViewAsync(
        Guid candidateUserId, Guid documentId, CancellationToken ct = default)
        => _knowledgeView.GetViewForCandidateAsync(candidateUserId, documentId, ct);
}
