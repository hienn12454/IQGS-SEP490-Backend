using System.Text.Json;
using ApplicationLayer.DTOs.Candidate;
using ApplicationLayer.DTOs.Rag;
using ApplicationLayer.Helpers;
using ApplicationLayer.Interfaces.Jobs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Exceptions;

namespace ApplicationLayer.Services;

public class CandidatePersonalSetService : ICandidatePersonalSetService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ICandidateProfileRepository _profiles;
    private readonly ICandidatePersonalSetJobRepository _jobs;
    private readonly IQuestionSetRepository _questionSets;
    private readonly ICandidateMarketplaceRepository _marketplace;
    private readonly IPracticeSessionRepository _sessions;
    private readonly IRagService _rag;
    private readonly ISubscriptionGateService _gate;
    private readonly IUsageMeteringService _metering;
    private readonly IJobScheduler _scheduler;
    private readonly ICandidateSkillPlanRepository _skillPlans;

    public CandidatePersonalSetService(
        ICandidateProfileRepository profiles,
        ICandidatePersonalSetJobRepository jobs,
        IQuestionSetRepository questionSets,
        ICandidateMarketplaceRepository marketplace,
        IPracticeSessionRepository sessions,
        IRagService rag,
        ISubscriptionGateService gate,
        IUsageMeteringService metering,
        IJobScheduler scheduler,
        ICandidateSkillPlanRepository skillPlans)
    {
        _profiles = profiles;
        _jobs = jobs;
        _questionSets = questionSets;
        _marketplace = marketplace;
        _sessions = sessions;
        _rag = rag;
        _gate = gate;
        _metering = metering;
        _scheduler = scheduler;
        _skillPlans = skillPlans;
    }

    public async Task<CandidatePersonalSetJobDto> CreateFromTextAsync(
        Guid candidateUserId, CreatePersonalSetFromTextDto dto, CancellationToken ct = default)
    {
        var jd = JobDescriptionValidator.Validate(dto.JobDescription);
        return await EnqueueAsync(
            candidateUserId, jd, ClampCount(dto.NumberOfQuestions),
            CandidatePersonalSetPurpose.JdGap, Array.Empty<string>(), ct);
    }

    public async Task<CandidatePersonalSetJobDto> CreateFromFileAsync(
        Guid candidateUserId, Stream file, string fileName, int numberOfQuestions, CancellationToken ct = default)
    {
        var parsed = await _rag.ParseJdAsync(file, fileName, ct);
        if (!parsed.Success || string.IsNullOrWhiteSpace(parsed.JobDescription))
            throw new BadRequestException(parsed.Error ?? "Không đọc được Job Description từ file.");
        var jd = JobDescriptionValidator.Validate(parsed.JobDescription, fileName);
        return await EnqueueAsync(
            candidateUserId, jd, ClampCount(numberOfQuestions),
            CandidatePersonalSetPurpose.JdGap, Array.Empty<string>(), ct);
    }

    public async Task<CandidatePersonalSetJobDto> StartCvDiagnosticAsync(Guid candidateUserId, CancellationToken ct = default)
    {
        var profile = await RequireProfileWithCvSkillsAsync(candidateUserId);
        var cvSkills = SkillMatchHelper.UnionCvSkills(profile.TechStack, profile.CvEvaluationJson).ToList();
        var summary = SkillMatchHelper.ParseEvaluationSummary(profile.CvEvaluationJson);
        var jdSkills = cvSkills.Take(6).ToList();
        var jd = CvCoachPromptBuilder.BuildSyntheticJd(profile.TargetRole, profile.SeniorityLevel, summary, jdSkills);
        return await EnqueueAsync(
            candidateUserId, jd, 10,
            CandidatePersonalSetPurpose.CvDiagnostic, Array.Empty<string>(), ct, profile, cvSkills);
    }

    public async Task<CandidatePersonalSetJobDto> StartCvDrillAsync(
        Guid candidateUserId, string skill, CancellationToken ct = default)
    {
        var key = SkillFitCalculator.Normalize(skill);
        if (key.Length == 0)
            throw new BadRequestException("Skill không hợp lệ.");

        var plan = await _skillPlans.GetByCandidateUserIdAsync(candidateUserId)
            ?? throw new BadRequestException("Hãy hoàn thành bài diagnostic từ CV trước khi drill.");
        var item = plan.Items.FirstOrDefault(i => SkillFitCalculator.Normalize(i.Skill) == key)
            ?? throw new BadRequestException("Skill không thuộc kế hoạch luyện tập.");
        if (item.Status == CandidateSkillPlanItemStatus.Done)
            throw new BadRequestException("Skill này đã đạt mục tiêu.");

        var profile = await RequireProfileWithCvSkillsAsync(candidateUserId);
        var focus = new[] { item.Skill };
        var jd = CvCoachPromptBuilder.BuildSyntheticJd(
            profile.TargetRole, profile.SeniorityLevel, null, focus);
        return await EnqueueAsync(
            candidateUserId, jd, 6,
            CandidatePersonalSetPurpose.CvDrill, focus, ct, profile);
    }

    public async Task<CandidatePersonalSetJobDto> GetJobAsync(Guid jobId, Guid candidateUserId)
    {
        var job = await _jobs.GetByIdAsync(jobId)
            ?? throw new NotFoundException("Không tìm thấy job sinh bộ câu hỏi.");
        if (job.CandidateUserId != candidateUserId)
            throw new ForbiddenException("Không được xem job của ứng viên khác.");
        await FailIfStuckGeneratingAsync(job);
        return MapJob(job);
    }

    public async Task<CandidatePersonalSetJobDto?> GetLatestPendingCoachJobAsync(Guid candidateUserId)
    {
        var jobs = await _jobs.ListByCandidateAsync(candidateUserId);
        var pending = jobs.FirstOrDefault(j =>
            (j.Purpose == CandidatePersonalSetPurpose.CvDiagnostic
             || j.Purpose == CandidatePersonalSetPurpose.CvDrill)
            && (j.Status == CandidatePersonalSetJobStatus.Queued
                || j.Status == CandidatePersonalSetJobStatus.Generating));
        if (pending is null) return null;
        var tracked = await _jobs.GetByIdAsync(pending.Id);
        if (tracked is null) return null;
        await FailIfStuckGeneratingAsync(tracked);
        if (tracked.Status is not CandidatePersonalSetJobStatus.Queued
            and not CandidatePersonalSetJobStatus.Generating)
            return null;
        return MapJob(tracked);
    }

    public async Task<IReadOnlyList<CandidatePersonalSetListItemDto>> ListMineAsync(Guid candidateUserId)
    {
        var rows = await _marketplace.ListPersonalByOwnerAsync(candidateUserId);
        var ids = rows.Select(r => r.Id).ToList();
        var last = (await _sessions.ListLatestCompletedScoresAsync(candidateUserId, ids))
            .ToDictionary(x => x.QuestionSetId);
        return rows.Select(r => new CandidatePersonalSetListItemDto
        {
            Id = r.Id,
            Title = string.IsNullOrWhiteSpace(r.Title) ? "Bộ của tôi" : r.Title!,
            Skills = r.QuestionSkills,
            TotalQuestions = r.TotalQuestions,
            MyLastScore = last.TryGetValue(r.Id, out var s) ? s.Score : null,
            MyLastCompletedAt = last.TryGetValue(r.Id, out var s2) ? s2.CompletedAt : null,
            CreatedAt = r.PublishedAt
        }).ToList();
    }

    public async Task ExecuteGenerationAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _jobs.GetByIdAsync(jobId)
            ?? throw new NotFoundException("Không tìm thấy job sinh bộ câu hỏi.");
        if (job.Status == CandidatePersonalSetJobStatus.Completed)
            return;

        job.Status = CandidatePersonalSetJobStatus.Generating;
        job.ErrorMessage = null;
        await _jobs.UpdateAsync(job);

        try
        {
            var cvSkills = DeserializeStringList(job.CvSkillsJson);
            var focusSkills = DeserializeStringList(job.FocusSkillsJson);
            var (count, skills, planNote, questionNote, titleFallback) = ResolveGenerationHints(job, cvSkills, focusSkills);
            var isCoach = job.Purpose == CandidatePersonalSetPurpose.CvDiagnostic
                || job.Purpose == CandidatePersonalSetPurpose.CvDrill;

            List<RagGeneratedQuestionDto> generated;
            string planJson;

            if (isCoach)
            {
                using var ragCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                // Model cloud lớn (vd. gemma 31b) dễ > 90s cho 4–6 câu; cắt quá sớm → job kẹt GENERATING rồi watchdog báo quá hạn.
                ragCts.CancelAfter(TimeSpan.FromMinutes(5));
                var syntheticPlan = BuildCoachPlan(skills, count, titleFallback, questionNote ?? planNote);
                var planObj = JsonSerializer.SerializeToElement(syntheticPlan, JsonOpts);
                planJson = planObj.GetRawText();
                job.PlanJson = planJson;
                job.GapSkillsJson = "[]";

                var qFast = await _rag.GenerateCandidateQuestionsFromPlanAsync(new GenerateQuestionsFromPlanRequest
                {
                    OwnerId = job.CandidateUserId,
                    JobDescription = job.JobDescription,
                    ApprovedPlan = syntheticPlan,
                    HrNote = questionNote ?? planNote,
                    Audience = "coach",
                    CvContext = job.JobDescription,
                    CandidateNote = questionNote ?? planNote
                }, ragCts.Token);
                if (!qFast.Success || qFast.Questions.Count == 0)
                    throw new ServerFailureException(qFast.Error ?? "RAG không sinh được câu hỏi.");
                generated = qFast.Questions;
            }
            else
            {
                var planResult = await _rag.GenerateCandidatePlanAsync(new GeneratePlanRequest
                {
                    OwnerId = job.CandidateUserId,
                    JobDescription = job.JobDescription,
                    NumberOfQuestions = count,
                    Difficulty = "medium",
                    QuestionTypes = ["technical", "behavioral", "system-design", "problem-solving"],
                    Skills = skills.ToList(),
                    HrNote = planNote,
                    Audience = "jd_practice",
                    CandidateNote = planNote
                }, ct);

                if (!planResult.Success || planResult.Plan is null)
                    throw new ServerFailureException(planResult.Error ?? "RAG không sinh được plan.");

                planJson = JsonSerializer.Serialize(planResult.Plan, JsonOpts);
                job.PlanJson = planJson;
                var planSkills = ExtractPlanSkills(planJson);
                var gap = SkillMatchHelper.Compute(planSkills, cvSkills).Missing;
                job.GapSkillsJson = JsonSerializer.Serialize(gap, JsonOpts);
                questionNote = gap.Count > 0
                    ? $"Ưu tiên câu hỏi cho skill còn thiếu so với CV: {string.Join(", ", gap)}."
                    : null;

                var qResult = await _rag.GenerateCandidateQuestionsFromPlanAsync(new GenerateQuestionsFromPlanRequest
                {
                    OwnerId = job.CandidateUserId,
                    JobDescription = job.JobDescription,
                    ApprovedPlan = planResult.Plan,
                    HrNote = questionNote,
                    Audience = "jd_practice",
                    CandidateNote = questionNote
                }, ct);

                if (!qResult.Success || qResult.Questions.Count == 0)
                    throw new ServerFailureException(qResult.Error ?? "RAG không sinh được câu hỏi.");

                generated = qResult.Questions;
            }

            var title = ResolveSetTitle(job, planJson, focusSkills, titleFallback);
            var set = new QuestionSet
            {
                OwnerId = job.CandidateUserId,
                Status = QuestionSetStatus.Published,
                Kind = QuestionSetKind.Personal,
                Title = title.Length > 500 ? title[..500] : title,
                JobDescription = job.JobDescription,
                HrNote = job.GapSkillsJson,
                PlanJson = planJson,
                GeneratedAt = DateTime.UtcNow,
                PublishedAt = DateTime.UtcNow
            };

            var questions = generated.Select((q, i) => new QuestionSetQuestion
            {
                QuestionSetId = set.Id,
                Order = q.Order ?? i + 1,
                Question = q.Question,
                QuestionType = string.IsNullOrWhiteSpace(q.QuestionType) ? "technical" : q.QuestionType,
                Difficulty = string.IsNullOrWhiteSpace(q.Difficulty) ? "medium" : q.Difficulty,
                Skill = q.Skill,
                FocusArea = q.FocusArea,
                Rationale = q.Rationale,
                SampleAnswer = q.SampleAnswer,
                AnswerMethod = string.IsNullOrWhiteSpace(q.AnswerMethod) ? "Text" : q.AnswerMethod,
                EvaluationCriteriaJson = JsonSerializer.Serialize(q.EvaluationCriteria ?? new List<string>(), JsonOpts),
                CitationsJson = JsonSerializer.Serialize(q.Citations ?? new List<object>(), JsonOpts)
            }).ToList();

            await _questionSets.AddAsync(set, questions);
            job.QuestionSetId = set.Id;
            job.Status = CandidatePersonalSetJobStatus.Completed;
            await _jobs.UpdateAsync(job);
        }
        catch (Exception ex)
        {
            if (job.Status != CandidatePersonalSetJobStatus.Completed)
            {
                job.Status = CandidatePersonalSetJobStatus.Failed;
                var msg = ex is OperationCanceledException
                    ? "Hết thời gian chờ RAG khi sinh câu hỏi. Thử lại."
                    : ex.Message;
                job.ErrorMessage = msg.Length > 4000 ? msg[..4000] : msg;
                await _jobs.UpdateAsync(job);
            }
            if (ex is not OperationCanceledException)
                throw;
        }
    }

    private static readonly TimeSpan GeneratingTimeout = TimeSpan.FromMinutes(10);

    private async Task FailIfStuckGeneratingAsync(CandidatePersonalSetJob job)
    {
        if (job.Status != CandidatePersonalSetJobStatus.Generating)
            return;
        var started = job.UpdatedAt ?? job.CreatedAt;
        if (DateTime.UtcNow - started < GeneratingTimeout)
            return;
        job.Status = CandidatePersonalSetJobStatus.Failed;
        job.ErrorMessage = "Sinh câu hỏi quá hạn (RAG/Hangfire không trả kết quả). Hãy thử lại.";
        await _jobs.UpdateAsync(job);
    }

    private async Task<CandidateProfile> RequireProfileWithCvSkillsAsync(Guid candidateUserId)
    {
        var profile = await _profiles.GetByUserIdAsync(candidateUserId)
            ?? throw new BadRequestException("Chưa có hồ sơ ứng viên.");
        var cvSkills = SkillMatchHelper.UnionCvSkills(profile.TechStack, profile.CvEvaluationJson);
        if (cvSkills.Count == 0)
            throw new BadRequestException("Hãy upload CV (hoặc thêm TechStack) trước khi dùng AI Coach.");
        return profile;
    }

    private async Task<CandidatePersonalSetJobDto> EnqueueAsync(
        Guid candidateUserId,
        string jobDescription,
        int numberOfQuestions,
        string purpose,
        IReadOnlyList<string> focusSkills,
        CancellationToken ct,
        CandidateProfile? profile = null,
        IReadOnlyList<string>? cvSkillsOverride = null)
    {
        _ = numberOfQuestions;
        var isCoach = purpose == CandidatePersonalSetPurpose.CvDiagnostic
            || purpose == CandidatePersonalSetPurpose.CvDrill;
        if (isCoach)
            await _gate.CheckCoachGenerationAsync(candidateUserId);
        else
            await _gate.CheckGeneratePersonalSetAsync(candidateUserId);
        profile ??= await _profiles.GetByUserIdAsync(candidateUserId)
            ?? throw new BadRequestException("Chưa có hồ sơ ứng viên.");
        var cvSkills = (cvSkillsOverride ?? SkillMatchHelper.UnionCvSkills(profile.TechStack, profile.CvEvaluationJson)).ToList();
        if (cvSkills.Count == 0)
            throw new BadRequestException("Hãy upload CV (hoặc thêm TechStack) trước khi sinh bộ câu hỏi.");

        var job = new CandidatePersonalSetJob
        {
            CandidateUserId = candidateUserId,
            Status = CandidatePersonalSetJobStatus.Queued,
            Purpose = purpose,
            JobDescription = jobDescription,
            CvSkillsJson = JsonSerializer.Serialize(cvSkills, JsonOpts),
            GapSkillsJson = "[]",
            FocusSkillsJson = JsonSerializer.Serialize(focusSkills, JsonOpts)
        };
        await _jobs.AddAsync(job);
        if (!isCoach)
            await _metering.IncrementAsync(candidateUserId, UsageType.CandidatePersonalSet);

        if (isCoach)
        {
            await ExecuteGenerationAsync(job.Id, ct);
            var updated = await _jobs.GetByIdAsync(job.Id) ?? job;
            return MapJob(updated);
        }

        _scheduler.EnqueueCandidatePersonalSet(job.Id);
        return MapJob(job);
    }

    private static object BuildCoachPlan(
        IReadOnlyList<string> skills, int count, string title, string? note)
    {
        count = Math.Max(count, 10);
        var diffs = new[]
        {
            new { difficulty = "easy", count = CountForBand(count, 0) },
            new { difficulty = "medium", count = CountForBand(count, 1) },
            new { difficulty = "hard", count = CountForBand(count, 2) }
        };
        var types = new[] { new { type = "technical", count, reason = "Đánh giá skill khai báo trên CV" } };
        var outline = Enumerable.Range(0, count).Select(i =>
        {
            var skill = skills.Count == 0 ? "general" : skills[i % skills.Count];
            var difficulty = RampDifficulty(i, count);
            return new
            {
                order = i + 1,
                type = "technical",
                difficulty,
                skill,
                focusArea = skill,
                goal = difficulty switch
                {
                    "easy" => "Kiểm tra khái niệm / khi nào dùng skill trên CV",
                    "hard" => "Tình huống / trade-off / vận dụng sâu skill trên CV",
                    _ => "Vận dụng và so sánh skill trên CV"
                }
            };
        }).ToList();
        return new
        {
            roleTitle = string.IsNullOrWhiteSpace(title) ? "CV knowledge check" : title,
            summary = "Bộ đánh giá kiến thức tăng dần độ khó, chỉ bám skill trên CV.",
            difficulty = "medium",
            experienceLevel = "junior",
            totalQuestions = count,
            skills,
            questionTypeDistribution = types,
            difficultyDistribution = diffs,
            recommendedQuestionOutline = outline,
            notes = note ?? ""
        };
    }

    /// <summary>0=easy (~30%), 1=medium (~40%), 2=hard (phần còn lại) — tổng = count.</summary>
    private static int CountForBand(int total, int band)
    {
        var easy = Math.Max(1, (int)Math.Round(total * 0.3));
        var medium = Math.Max(1, (int)Math.Round(total * 0.4));
        if (easy + medium >= total)
        {
            easy = Math.Max(1, total / 3);
            medium = Math.Max(1, total / 3);
        }
        var hard = Math.Max(1, total - easy - medium);
        return band == 0 ? easy : band == 1 ? medium : hard;
    }

    private static string RampDifficulty(int zeroBasedIndex, int total)
    {
        var easy = CountForBand(total, 0);
        var medium = CountForBand(total, 1);
        if (zeroBasedIndex < easy) return "easy";
        if (zeroBasedIndex < easy + medium) return "medium";
        return "hard";
    }

    private static (int Count, IReadOnlyList<string> Skills, string PlanNote, string? QuestionNote, string TitleFallback)
        ResolveGenerationHints(CandidatePersonalSetJob job, List<string> cvSkills, List<string> focusSkills)
    {
        if (job.Purpose == CandidatePersonalSetPurpose.CvDiagnostic)
        {
            var capped = cvSkills.Take(10).ToList();
            return (10, capped, CvCoachPromptBuilder.DiagnosticHrNote(capped),
                CvCoachPromptBuilder.DiagnosticHrNote(capped), "CV check");
        }

        if (job.Purpose == CandidatePersonalSetPurpose.CvDrill)
        {
            var skill = focusSkills.FirstOrDefault() ?? "skill";
            var drillSkills = focusSkills.Count > 0 ? focusSkills : cvSkills.Take(1).ToList();
            return (4, drillSkills, CvCoachPromptBuilder.DrillHrNote(skill),
                CvCoachPromptBuilder.DrillHrNote(skill), $"Drill — {skill}");
        }

        var gapNote = BuildGapNote(cvSkills, job.JobDescription);
        return (10, cvSkills, gapNote, null, "Bộ luyện tập từ JD");
    }

    private static string ResolveSetTitle(
        CandidatePersonalSetJob job, string planJson, List<string> focusSkills, string fallback)
    {
        if (job.Purpose == CandidatePersonalSetPurpose.CvDiagnostic)
        {
            var role = ExtractRoleTitle(planJson);
            return string.IsNullOrWhiteSpace(role) ? "CV check" : $"CV check — {role}";
        }

        if (job.Purpose == CandidatePersonalSetPurpose.CvDrill)
        {
            var skill = focusSkills.FirstOrDefault();
            return string.IsNullOrWhiteSpace(skill) ? "Drill" : $"Drill — {skill}";
        }

        return ExtractRoleTitle(planJson) ?? fallback;
    }

    private static int ClampCount(int n) => Math.Clamp(n <= 0 ? 10 : n, 8, 12);

    private static CandidatePersonalSetJobDto MapJob(CandidatePersonalSetJob job) => new()
    {
        Id = job.Id,
        Status = job.Status,
        Purpose = job.Purpose,
        QuestionSetId = job.QuestionSetId,
        ErrorMessage = job.ErrorMessage,
        CvSkills = DeserializeStringList(job.CvSkillsJson),
        GapSkills = DeserializeStringList(job.GapSkillsJson),
        FocusSkills = DeserializeStringList(job.FocusSkillsJson),
        CreatedAt = job.CreatedAt
    };

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static string BuildGapNote(IReadOnlyList<string> cvSkills, string jd)
    {
        var cv = string.Join(", ", cvSkills);
        return $"CV skills: {cv}. Sinh câu hỏi bám JD; ưu tiên kỹ năng JD mà CV chưa có.";
    }

    private static List<string> ExtractPlanSkills(string planJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(planJson);
            if (doc.RootElement.TryGetProperty("skills", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                return arr.EnumerateArray()
                    .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .ToList();
            }
        }
        catch (JsonException)
        {
            /* ignore */
        }
        return new List<string>();
    }

    private static string? ExtractRoleTitle(string planJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(planJson);
            foreach (var name in new[] { "roleTitle", "title", "role" })
            {
                if (doc.RootElement.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
                    return el.GetString();
            }
        }
        catch (JsonException)
        {
            /* ignore */
        }
        return null;
    }
}
