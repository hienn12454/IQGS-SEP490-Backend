using DomainLayer.Entities;
using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfrastructureLayer.Configurations.Studio;

/// <summary>
/// Restrict trên root/product links: soft-delete IsActive, tránh hard-delete nuốt set đã publish.
/// Cascade chỉ con thuần (messages, sections, focus) không có consumer ngoài Studio.
/// </summary>
public sealed class InterviewProjectConfiguration : IEntityTypeConfiguration<InterviewProject>
{
    public void Configure(EntityTypeBuilder<InterviewProject> builder)
    {
        builder.ToTable("tbl_studio_interview_projects");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(250);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);

        builder.HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OwnerId);
    }
}

public sealed class JobDescriptionConfiguration : IEntityTypeConfiguration<JobDescription>
{
    public void Configure(EntityTypeBuilder<JobDescription> builder)
    {
        builder.ToTable("tbl_studio_job_descriptions");
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.DetectedSkillsJson).HasColumnType("jsonb");
        builder.HasIndex(x => x.ProjectId).IsUnique();

        builder.HasOne(x => x.Project)
            .WithOne(p => p.JobDescription)
            .HasForeignKey<JobDescription>(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StudioKnowledgeDocumentConfiguration : IEntityTypeConfiguration<StudioKnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<StudioKnowledgeDocument> builder)
    {
        builder.ToTable("tbl_studio_knowledge_documents");
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FileType).IsRequired().HasMaxLength(120);
        builder.Property(x => x.StoragePath).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(x => x.KnowledgeDocumentId);
        builder.HasIndex(x => x.ProjectId);

        builder.HasOne(x => x.Project)
            .WithMany(p => p.KnowledgeDocuments)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.KnowledgeDocument)
            .WithMany()
            .HasForeignKey(x => x.KnowledgeDocumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class AiChatSessionConfiguration : IEntityTypeConfiguration<AiChatSession>
{
    public void Configure(EntityTypeBuilder<AiChatSession> builder)
    {
        builder.ToTable("tbl_studio_ai_chat_sessions");
        builder.Property(x => x.SelectionMode).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Project)
            .WithMany(p => p.ChatSessions)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.UserId);
    }
}

public sealed class AiChatMessageConfiguration : IEntityTypeConfiguration<AiChatMessage>
{
    public void Configure(EntityTypeBuilder<AiChatMessage> builder)
    {
        builder.ToTable("tbl_studio_ai_chat_messages");
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Content).IsRequired();

        builder.HasOne(x => x.Session)
            .WithMany(s => s.Messages)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RelatedPlan)
            .WithMany()
            .HasForeignKey(x => x.RelatedPlanId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.SessionId);
    }
}

public sealed class InterviewPlanConfiguration : IEntityTypeConfiguration<InterviewPlan>
{
    public void Configure(EntityTypeBuilder<InterviewPlan> builder)
    {
        builder.ToTable("tbl_studio_interview_plans");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SourcePlanJson).HasColumnType("text");
        builder.HasIndex(x => new { x.ProjectId, x.Revision }).IsUnique();
        builder.Property(x => x.ConcurrencyVersion).IsConcurrencyToken();

        builder.HasOne(x => x.Project)
            .WithMany(p => p.Plans)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Session)
            .WithMany(s => s.Plans)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.SessionId);
    }
}

public sealed class PlanSectionConfiguration : IEntityTypeConfiguration<PlanSection>
{
    public void Configure(EntityTypeBuilder<PlanSection> builder)
    {
        builder.ToTable("tbl_studio_plan_sections");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.InterviewPlan)
            .WithMany(p => p.Sections)
            .HasForeignKey(x => x.InterviewPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.InterviewPlanId);
    }
}

public sealed class PlanFocusAreaConfiguration : IEntityTypeConfiguration<PlanFocusArea>
{
    public void Configure(EntityTypeBuilder<PlanFocusArea> builder)
    {
        builder.ToTable("tbl_studio_plan_focus_areas");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Weight).HasPrecision(5, 2);

        builder.HasOne(x => x.InterviewPlan)
            .WithMany(p => p.FocusAreas)
            .HasForeignKey(x => x.InterviewPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.InterviewPlanId);
    }
}

public sealed class PlanApprovalHistoryConfiguration : IEntityTypeConfiguration<PlanApprovalHistory>
{
    public void Configure(EntityTypeBuilder<PlanApprovalHistory> builder)
    {
        builder.ToTable("tbl_studio_plan_approval_histories");
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Project)
            .WithMany(p => p.ApprovalHistories)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InterviewPlan)
            .WithMany(p => p.ApprovalHistories)
            .HasForeignKey(x => x.InterviewPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Actor)
            .WithMany()
            .HasForeignKey(x => x.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.InterviewPlanId);
    }
}

public sealed class StudioSettingsConfiguration : IEntityTypeConfiguration<StudioSettings>
{
    public void Configure(EntityTypeBuilder<StudioSettings> builder)
    {
        builder.ToTable("tbl_studio_settings");
        builder.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.QuestionTypesJson).HasMaxLength(500);
        builder.Property(x => x.ContentMode).HasMaxLength(30).HasDefaultValue("Mixed");
        builder.Property(x => x.CodeTemplatesJson).HasMaxLength(1000);
        builder.HasIndex(x => x.ProjectId).IsUnique();

        builder.HasOne(x => x.Project)
            .WithOne(p => p.Settings)
            .HasForeignKey<StudioSettings>(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AppliedPlan)
            .WithMany(p => p.AppliedInSettings)
            .HasForeignKey(x => x.AppliedPlanId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.AppliedPlanId);
    }
}

public sealed class StudioFocusAreaConfiguration : IEntityTypeConfiguration<StudioFocusArea>
{
    public void Configure(EntityTypeBuilder<StudioFocusArea> builder)
    {
        builder.ToTable("tbl_studio_focus_areas");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Weight).HasPrecision(5, 2);

        builder.HasOne(x => x.StudioSettings)
            .WithMany(s => s.FocusAreas)
            .HasForeignKey(x => x.StudioSettingsId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.StudioSettingsId);
    }
}

public sealed class InterviewQuestionConfiguration : IEntityTypeConfiguration<InterviewQuestion>
{
    public void Configure(EntityTypeBuilder<InterviewQuestion> builder)
    {
        builder.ToTable("tbl_studio_interview_questions");
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TagsJson).HasColumnType("jsonb");

        // Restrict: hard-delete project/plan/run bị chặn nếu còn câu — soft-delete IsActive an toàn
        builder.HasOne(x => x.Project)
            .WithMany(p => p.Questions)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InterviewPlan)
            .WithMany(p => p.Questions)
            .HasForeignKey(x => x.InterviewPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PlanSection)
            .WithMany(s => s.Questions)
            .HasForeignKey(x => x.PlanSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.GenerationRun)
            .WithMany(r => r.Questions)
            .HasForeignKey(x => x.GenerationRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => new { x.ProjectId, x.OrderIndex });
        builder.HasIndex(x => x.InterviewPlanId);
        builder.HasIndex(x => x.GenerationRunId);
    }
}

public sealed class QuestionGenerationRunConfiguration : IEntityTypeConfiguration<QuestionGenerationRun>
{
    public void Configure(EntityTypeBuilder<QuestionGenerationRun> builder)
    {
        builder.ToTable("tbl_studio_question_generation_runs");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.GeneratorType).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Project)
            .WithMany(p => p.GenerationRuns)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InterviewPlan)
            .WithMany(p => p.GenerationRuns)
            .HasForeignKey(x => x.InterviewPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.InterviewPlanId);
    }
}

public sealed class ProjectShareConfiguration : IEntityTypeConfiguration<ProjectShare>
{
    public void Configure(EntityTypeBuilder<ProjectShare> builder)
    {
        builder.ToTable("tbl_studio_project_shares");
        builder.Property(x => x.Token).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Permission).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.Token).IsUnique();

        builder.HasOne(x => x.Project)
            .WithMany(p => p.Shares)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProjectId);
    }
}
