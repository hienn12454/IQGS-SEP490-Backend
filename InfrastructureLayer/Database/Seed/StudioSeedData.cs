using DomainLayer.Constants;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Database.Seed;

public static class StudioSeedData
{
    public static async Task SeedAsync(AppDbContext context, ILogger logger)
    {
        if (await context.InterviewProjects.AnyAsync())
            return;

        var hrUser = await context.Users.FirstOrDefaultAsync(x => x.RoleId == UserRole.HRId && x.IsActive);
        if (hrUser is null)
            return;

        var project = new InterviewProject
        {
            OwnerId = hrUser.Id,
            Name = "Studio Demo - Senior .NET Backend Engineer",
            Description = "Seed data cho Interview Plan Studio",
            Status = InterviewProjectStatus.AwaitingApproval,
            LatestPlanRevision = 1
        };
        context.InterviewProjects.Add(project);

        var jd = new JobDescription
        {
            ProjectId = project.Id,
            Title = "Senior .NET Backend Engineer",
            Content = "We are looking for a Senior .NET Backend Engineer with C#, PostgreSQL, Azure, System Design.",
            SourceType = JobDescriptionSourceType.PastedText,
            DetectedRole = "Backend Engineer",
            DetectedSeniority = "Senior",
            DetectedLanguage = "English",
            DetectedSkillsJson = "[\".NET\",\"C#\",\"PostgreSQL\",\"Azure\",\"System Design\"]",
            WordCount = 15,
            CharacterCount = 100
        };
        context.StudioJobDescriptions.Add(jd);

        var docs = Enumerable.Range(1, 4).Select(i => new StudioKnowledgeDocument
        {
            ProjectId = project.Id,
            FileName = $"doc-{i}.txt",
            FileType = "text/plain",
            FileSize = 100 + i,
            StoragePath = $"studio-documents/{project.Id}/doc-{i}.txt",
            ExtractedText = $"Document {i} content",
            IsSelected = i <= 2,
            ProcessingStatus = DocumentProcessingStatus.Completed
        }).ToList();
        context.StudioKnowledgeDocuments.AddRange(docs);

        var session = new AiChatSession { ProjectId = project.Id, UserId = hrUser.Id, SelectionMode = AiModelSelectionMode.Auto, SelectedModelDisplayName = "Mock Auto" };
        context.AiChatSessions.Add(session);

        var plan = new InterviewPlan
        {
            ProjectId = project.Id,
            SessionId = session.Id,
            Revision = 1,
            Status = InterviewPlanStatus.AwaitingApproval,
            Title = "Interview Plan for Senior .NET Backend Engineer",
            TotalQuestions = 15,
            InterviewLengthMinutes = 75,
            SeniorityLevel = "Senior",
            Language = "English",
            Difficulty = QuestionDifficulty.Medium
        };
        context.InterviewPlans.Add(plan);
        await context.SaveChangesAsync();

        var sections = new[]
        {
            new PlanSection { InterviewPlanId = plan.Id, Name = "Technical Foundations", OrderIndex = 1, NumberOfQuestions = 3, Difficulty = QuestionDifficulty.Medium, EstimatedMinutes = 15 },
            new PlanSection { InterviewPlanId = plan.Id, Name = "System Design", OrderIndex = 2, NumberOfQuestions = 5, Difficulty = QuestionDifficulty.Hard, EstimatedMinutes = 25 },
            new PlanSection { InterviewPlanId = plan.Id, Name = "Problem Solving", OrderIndex = 3, NumberOfQuestions = 3, Difficulty = QuestionDifficulty.Medium, EstimatedMinutes = 15 },
            new PlanSection { InterviewPlanId = plan.Id, Name = "Behavioral", OrderIndex = 4, NumberOfQuestions = 3, Difficulty = QuestionDifficulty.Medium, EstimatedMinutes = 15 },
            new PlanSection { InterviewPlanId = plan.Id, Name = "Follow-up", OrderIndex = 5, NumberOfQuestions = 1, Difficulty = QuestionDifficulty.Medium, EstimatedMinutes = 5 }
        };
        context.PlanSections.AddRange(sections);

        context.PlanFocusAreas.AddRange(
            new PlanFocusArea { InterviewPlanId = plan.Id, Name = "Technical", Weight = 0.35m, OrderIndex = 1 },
            new PlanFocusArea { InterviewPlanId = plan.Id, Name = "System Design", Weight = 0.30m, OrderIndex = 2 },
            new PlanFocusArea { InterviewPlanId = plan.Id, Name = "Problem Solving", Weight = 0.20m, OrderIndex = 3 },
            new PlanFocusArea { InterviewPlanId = plan.Id, Name = "Behavioral", Weight = 0.15m, OrderIndex = 4 }
        );

        var settings = new StudioSettings
        {
            ProjectId = project.Id,
            AppliedPlanId = plan.Id,
            InterviewLengthMinutes = plan.InterviewLengthMinutes,
            NumberOfQuestions = plan.TotalQuestions,
            SeniorityLevel = plan.SeniorityLevel,
            Language = plan.Language,
            Difficulty = plan.Difficulty,
            QuestionTone = "Professional",
            IncludeSampleAnswers = false,
            IncludeScoringRubric = false,
            OutputFormat = "Markdown"
        };
        context.StudioSettings.Add(settings);
        await context.SaveChangesAsync();

        logger.LogInformation("Đã seed dữ liệu Studio demo.");
    }
}
