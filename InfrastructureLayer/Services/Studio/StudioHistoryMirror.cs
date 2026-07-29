using System.Text;
using System.Text.Json;
using ApplicationLayer.DTOs.Rag;
using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using InfrastructureLayer.Database;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Services.Studio;

/// <summary>
/// SCRUM-372: đồng bộ InterviewQuestions (Studio) → QuestionGenerationJob + GeneratedQuestion
/// để trang History (/hr/history) hiển thị bộ câu hỏi sinh từ Studio v2.
/// </summary>
internal static class StudioHistoryMirror
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static async Task MirrorCompletedRunAsync(
        AppDbContext dbContext,
        QuestionGenerationRun run,
        IReadOnlyList<RagGeneratedQuestionDto> ragQuestions,
        CancellationToken ct)
    {
        var project = await dbContext.InterviewProjects.FirstAsync(x => x.Id == run.ProjectId, ct);
        var plan = await dbContext.InterviewPlans.FirstAsync(x => x.Id == run.InterviewPlanId, ct);
        var jd = await dbContext.StudioJobDescriptions
            .FirstOrDefaultAsync(x => x.ProjectId == run.ProjectId && x.IsActive, ct);
        var settings = await dbContext.StudioSettings
            .FirstOrDefaultAsync(x => x.ProjectId == run.ProjectId && x.IsActive, ct);

        var focusNames = await dbContext.PlanFocusAreas
            .Where(x => x.InterviewPlanId == plan.Id && x.IsActive)
            .OrderBy(x => x.OrderIndex)
            .Select(x => x.Name)
            .ToListAsync(ct);

        // Regenerate tạo run mới — tái sử dụng MirroredJobId của run trước cùng project+plan
        var mirroredJobId = run.MirroredJobId
            ?? await dbContext.QuestionGenerationRuns
                .Where(x => x.ProjectId == run.ProjectId
                            && x.InterviewPlanId == run.InterviewPlanId
                            && x.Id != run.Id
                            && x.IsActive
                            && x.MirroredJobId != null)
                .OrderByDescending(x => x.CompletedAt ?? x.CreatedAt)
                .Select(x => x.MirroredJobId)
                .FirstOrDefaultAsync(ct);

        var ownerId = project.OwnerId != Guid.Empty ? project.OwnerId : run.RequestedBy;
        var jdContent = jd?.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(jdContent))
            jdContent = "(Studio) Job description không có nội dung.";

        var numberOfQuestions = settings?.NumberOfQuestions > 0
            ? settings.NumberOfQuestions
            : (plan.TotalQuestions > 0 ? plan.TotalQuestions : ragQuestions.Count);
        var difficulty = MapDifficulty(settings?.Difficulty ?? plan.Difficulty);
        var questionTypesJson = string.IsNullOrWhiteSpace(settings?.QuestionTypesJson)
            ? "[]"
            : settings!.QuestionTypesJson!;
        var skillsJson = JsonSerializer.Serialize(focusNames, JsonOptions);
        // Đảm bảo PlanJson có roleTitle thật (DetectedRole/Title) — tránh Insights hiện chuỗi skills.
        var planJson = EnsurePlanJsonRoleTitle(
            string.IsNullOrWhiteSpace(plan.SourcePlanJson) ? "{}" : plan.SourcePlanJson!,
            jd?.DetectedRole,
            jd?.Title,
            plan.Title,
            project.Name);
        var hrNote = $"STUDIO_MIRROR; project={run.ProjectId}; plan={run.InterviewPlanId}; run={run.Id}";

        QuestionGenerationJob? existingJob = null;
        if (mirroredJobId is Guid existingId)
        {
            existingJob = await dbContext.QuestionGenerationJobs
                .Include(j => j.Plan)
                .FirstOrDefaultAsync(j => j.Id == existingId, ct);
        }

        QuestionGenerationJob job;
        if (existingJob is not null)
        {
            job = existingJob;
            job.OwnerId = ownerId;
            job.JobDescription = jdContent;
            job.HrNote = hrNote;
            job.NumberOfQuestions = numberOfQuestions;
            job.Difficulty = difficulty;
            job.QuestionTypesJson = questionTypesJson;
            job.SkillsJson = skillsJson;
            job.Status = QuestionGenerationJobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorMessage = null;
            job.UpdatedAt = DateTime.UtcNow;

            if (job.Plan is null)
            {
                job.Plan = new QuestionGenerationPlan
                {
                    JobId = job.Id,
                    PlanJson = planJson,
                    IsApproved = true,
                    ApprovedAt = plan.ApprovedAt ?? DateTime.UtcNow
                };
                dbContext.QuestionGenerationPlans.Add(job.Plan);
            }
            else
            {
                job.Plan.PlanJson = planJson;
                job.Plan.IsApproved = true;
                job.Plan.ApprovedAt = plan.ApprovedAt ?? DateTime.UtcNow;
                job.Plan.UpdatedAt = DateTime.UtcNow;
            }

            var oldQuestions = await dbContext.GeneratedQuestions
                .Where(q => q.JobId == job.Id)
                .ToListAsync(ct);
            if (oldQuestions.Count > 0)
                dbContext.GeneratedQuestions.RemoveRange(oldQuestions);
        }
        else
        {
            job = CreateJob(dbContext, ownerId, jdContent, hrNote, numberOfQuestions,
                difficulty, questionTypesJson, skillsJson, planJson);
        }

        var entities = ragQuestions.Select((q, index) => new GeneratedQuestion
        {
            JobId = job.Id,
            Order = q.Order ?? index + 1,
            Question = q.Question,
            QuestionType = q.QuestionType,
            Difficulty = q.Difficulty,
            Skill = q.Skill,
            FocusArea = q.FocusArea,
            Rationale = q.Rationale,
            SampleAnswer = q.SampleAnswer,
            EvaluationCriteriaJson = JsonSerializer.Serialize(
                q.EvaluationCriteria ?? new List<string>(), JsonOptions),
            CitationsJson = JsonSerializer.Serialize(
                q.Citations ?? new List<object>(), JsonOptions)
        }).ToList();

        if (entities.Count > 0)
            dbContext.GeneratedQuestions.AddRange(entities);

        run.MirroredJobId = job.Id;
    }

    private static QuestionGenerationJob CreateJob(
        AppDbContext dbContext,
        Guid ownerId,
        string jdContent,
        string hrNote,
        int numberOfQuestions,
        string difficulty,
        string questionTypesJson,
        string skillsJson,
        string planJson)
    {
        var job = new QuestionGenerationJob
        {
            OwnerId = ownerId,
            JobDescription = jdContent,
            HrNote = hrNote,
            JdInputType = JdInputType.Text,
            NumberOfQuestions = numberOfQuestions,
            Difficulty = difficulty,
            QuestionTypesJson = questionTypesJson,
            SkillsJson = skillsJson,
            Status = QuestionGenerationJobStatus.Completed,
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = null
        };
        dbContext.QuestionGenerationJobs.Add(job);

        var planEntity = new QuestionGenerationPlan
        {
            JobId = job.Id,
            PlanJson = planJson,
            IsApproved = true,
            ApprovedAt = DateTime.UtcNow
        };
        dbContext.QuestionGenerationPlans.Add(planEntity);
        job.Plan = planEntity;
        return job;
    }

    private static string MapDifficulty(QuestionDifficulty difficulty)
        => difficulty switch
        {
            QuestionDifficulty.Easy => "easy",
            QuestionDifficulty.Hard => "hard",
            _ => "medium"
        };

    /// <summary>
    /// Ghi/đè roleTitle ngắn vào PlanJson mirror. Không ghi đè nếu đã có roleTitle hợp lệ.
    /// </summary>
    private static string EnsurePlanJsonRoleTitle(
        string planJson,
        string? detectedRole,
        string? jdTitle,
        string? planTitle,
        string? projectName)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(planJson) ? "{}" : planJson);
            var root = doc.RootElement;
            string? existing = null;
            foreach (var key in new[] { "roleTitle", "role_title", "jobTitle", "title" })
            {
                if (root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String)
                {
                    existing = el.GetString();
                    if (!string.IsNullOrWhiteSpace(existing) && existing!.Length <= 80 && existing.Count(c => c == ',') < 2)
                        return planJson;
                }
            }

            var role = FirstNonEmpty(detectedRole, jdTitle, planTitle, projectName, existing);
            if (string.IsNullOrWhiteSpace(role))
                return planJson;

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("roleTitle", role.Trim());
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.NameEquals("roleTitle") || prop.NameEquals("role_title"))
                        continue;
                    prop.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            var role = FirstNonEmpty(detectedRole, jdTitle, planTitle, projectName);
            if (string.IsNullOrWhiteSpace(role))
                return "{}";
            return JsonSerializer.Serialize(new { roleTitle = role.Trim() }, JsonOptions);
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
