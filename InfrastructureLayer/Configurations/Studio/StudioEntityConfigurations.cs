using DomainLayer.Studio;
using DomainLayer.Studio.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfrastructureLayer.Configurations.Studio;

public sealed class InterviewProjectConfiguration : IEntityTypeConfiguration<InterviewProject>
{
    public void Configure(EntityTypeBuilder<InterviewProject> builder)
    {
        builder.ToTable("studio_interview_projects");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(250);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
    }
}

public sealed class JobDescriptionConfiguration : IEntityTypeConfiguration<JobDescription>
{
    public void Configure(EntityTypeBuilder<JobDescription> builder)
    {
        builder.ToTable("studio_job_descriptions");
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.DetectedSkillsJson).HasColumnType("jsonb");
        builder.HasIndex(x => x.ProjectId).IsUnique();
    }
}

public sealed class StudioKnowledgeDocumentConfiguration : IEntityTypeConfiguration<StudioKnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<StudioKnowledgeDocument> builder)
    {
        builder.ToTable("studio_knowledge_documents");
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FileType).IsRequired().HasMaxLength(120);
        builder.Property(x => x.StoragePath).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(x => x.KnowledgeDocumentId);
        builder.HasIndex(x => x.ProjectId);
    }
}

public sealed class AiChatSessionConfiguration : IEntityTypeConfiguration<AiChatSession>
{
    public void Configure(EntityTypeBuilder<AiChatSession> builder)
    {
        builder.ToTable("studio_ai_chat_sessions");
        builder.Property(x => x.SelectionMode).HasConversion<string>().HasMaxLength(20);
    }
}

public sealed class AiChatMessageConfiguration : IEntityTypeConfiguration<AiChatMessage>
{
    public void Configure(EntityTypeBuilder<AiChatMessage> builder)
    {
        builder.ToTable("studio_ai_chat_messages");
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Content).IsRequired();
    }
}

public sealed class InterviewPlanConfiguration : IEntityTypeConfiguration<InterviewPlan>
{
    public void Configure(EntityTypeBuilder<InterviewPlan> builder)
    {
        builder.ToTable("studio_interview_plans");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SourcePlanJson).HasColumnType("text");
        builder.HasIndex(x => new { x.ProjectId, x.Revision }).IsUnique();
        builder.Property(x => x.ConcurrencyVersion).IsConcurrencyToken();
    }
}

public sealed class PlanSectionConfiguration : IEntityTypeConfiguration<PlanSection>
{
    public void Configure(EntityTypeBuilder<PlanSection> builder)
    {
        builder.ToTable("studio_plan_sections");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(20);
    }
}

public sealed class PlanFocusAreaConfiguration : IEntityTypeConfiguration<PlanFocusArea>
{
    public void Configure(EntityTypeBuilder<PlanFocusArea> builder)
    {
        builder.ToTable("studio_plan_focus_areas");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Weight).HasPrecision(5, 2);
    }
}

public sealed class PlanApprovalHistoryConfiguration : IEntityTypeConfiguration<PlanApprovalHistory>
{
    public void Configure(EntityTypeBuilder<PlanApprovalHistory> builder)
    {
        builder.ToTable("studio_plan_approval_histories");
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
    }
}

public sealed class StudioSettingsConfiguration : IEntityTypeConfiguration<StudioSettings>
{
    public void Configure(EntityTypeBuilder<StudioSettings> builder)
    {
        builder.ToTable("studio_settings");
        builder.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.QuestionTypesJson).HasMaxLength(500);
        builder.HasIndex(x => x.ProjectId).IsUnique();
    }
}

public sealed class StudioFocusAreaConfiguration : IEntityTypeConfiguration<StudioFocusArea>
{
    public void Configure(EntityTypeBuilder<StudioFocusArea> builder)
    {
        builder.ToTable("studio_focus_areas");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Weight).HasPrecision(5, 2);
    }
}

public sealed class InterviewQuestionConfiguration : IEntityTypeConfiguration<InterviewQuestion>
{
    public void Configure(EntityTypeBuilder<InterviewQuestion> builder)
    {
        builder.ToTable("studio_interview_questions");
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TagsJson).HasColumnType("jsonb");
    }
}

public sealed class QuestionGenerationRunConfiguration : IEntityTypeConfiguration<QuestionGenerationRun>
{
    public void Configure(EntityTypeBuilder<QuestionGenerationRun> builder)
    {
        builder.ToTable("studio_question_generation_runs");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.GeneratorType).HasConversion<string>().HasMaxLength(20);
        // SCRUM-372: tìm nhanh run đã mirror sang History
        builder.HasIndex(x => x.MirroredJobId);
    }
}

public sealed class ProjectShareConfiguration : IEntityTypeConfiguration<ProjectShare>
{
    public void Configure(EntityTypeBuilder<ProjectShare> builder)
    {
        builder.ToTable("studio_project_shares");
        builder.Property(x => x.Token).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Permission).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.Token).IsUnique();
    }
}
