using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace InfrastructureLayer.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<HRProfile> HRProfiles { get; set; }
    public DbSet<CandidateProfile> CandidateProfiles { get; set; }
    public DbSet<KnowledgeDocument> KnowledgeDocuments { get; set; }
    public DbSet<KnowledgeChunk> KnowledgeChunks { get; set; }
    public DbSet<QuestionGenerationJob> QuestionGenerationJobs { get; set; }
    public DbSet<QuestionGenerationPlan> QuestionGenerationPlans { get; set; }
    public DbSet<GeneratedQuestion> GeneratedQuestions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        // ── Role (lookup table) ─────────────────────────────────────
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).ValueGeneratedNever();   // seed cố định
            entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(r => r.Name).IsUnique();

            // Seed 3 roles
            entity.HasData(
                new Role { Id = UserRole.AdminId, Name = UserRole.Admin },
                new Role { Id = UserRole.HRId, Name = UserRole.HR },
                new Role { Id = UserRole.CandidateId, Name = UserRole.Candidate }
            );
        });

        // ── User ────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(255);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(255);
            entity.Property(u => u.PasswordHash);                            // nullable — OAuth users
            entity.Property(u => u.PhoneNumber).HasMaxLength(20);
            entity.Property(u => u.AvatarUrl).HasMaxLength(500);

            // Role FK
            entity.Property(u => u.RoleId).IsRequired();
            entity.HasOne(u => u.Role)
                  .WithMany(r => r.Users)
                  .HasForeignKey(u => u.RoleId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Auth
            entity.Property(u => u.IsEmailVerified).IsRequired().HasDefaultValue(false);
            entity.Property(u => u.FailedLoginAttempts).IsRequired().HasDefaultValue(0);
            entity.Property(u => u.Provider).IsRequired().HasMaxLength(20).HasDefaultValue("local");
            entity.Property(u => u.GoogleId).HasMaxLength(255);
            // Unique trên GoogleId nhưng cho phép nhiều row NULL (Local users).
            entity.HasIndex(u => u.GoogleId)
                  .IsUnique()
                  .HasFilter("\"GoogleId\" IS NOT NULL");
            entity.Property(u => u.RefreshToken).HasMaxLength(512);
            entity.HasIndex(u => u.RefreshToken);

            // Password reset & email verification — lưu SHA-256 hash
            entity.Property(u => u.PasswordResetToken).HasMaxLength(512);
            entity.Property(u => u.EmailVerificationToken).HasMaxLength(512);

            // Profile
            entity.Property(u => u.IsProfileComplete).IsRequired().HasDefaultValue(false);
        });

        // ── Company ─────────────────────────────────────────────────
        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(255);
            entity.Property(c => c.LogoUrl).HasMaxLength(500);
            entity.Property(c => c.WebsiteUrl).HasMaxLength(500);
            entity.HasIndex(c => c.Name);
        });

        // ── HRProfile ───────────────────────────────────────────────
        modelBuilder.Entity<HRProfile>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.JobTitle).HasMaxLength(150);
            entity.Property(p => p.PhoneNumber).HasMaxLength(20);
            entity.Property(p => p.LinkedInUrl).HasMaxLength(500);
            entity.Property(p => p.IsCompanyVerified).IsRequired().HasDefaultValue(false);

            entity.HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<HRProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(p => p.UserId).IsUnique();

            entity.HasOne(p => p.Company)
                .WithMany(c => c.HRProfiles)
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CandidateProfile ────────────────────────────────────────
        modelBuilder.Entity<CandidateProfile>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.TargetRole).HasMaxLength(150);
            entity.Property(p => p.SeniorityLevel).HasMaxLength(50);
            entity.Property(p => p.TechStack).HasColumnType("text[]");
            entity.Property(p => p.PhoneNumber).HasMaxLength(20);
            entity.Property(p => p.LinkedInUrl).HasMaxLength(500);
            entity.Property(p => p.GithubUrl).HasMaxLength(500);

            entity.HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<CandidateProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(p => p.UserId).IsUnique();
        });

        // ── KnowledgeDocument ───────────────────────────────────────
        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.ToTable("knowledge_documents");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Scope).IsRequired().HasMaxLength(20);
            entity.Property(d => d.FileName).IsRequired().HasMaxLength(500);
            entity.Property(d => d.BlobPath).IsRequired().HasMaxLength(1000);
            entity.Property(d => d.ContentHash).HasMaxLength(128);
            entity.Property(d => d.SourceTitle).HasMaxLength(500);
            entity.Property(d => d.SourceUrl).HasMaxLength(1000);
            entity.Property(d => d.Section).HasMaxLength(200);
            entity.Property(d => d.Status).IsRequired().HasMaxLength(30);
            entity.Property(d => d.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(d => d.Scope);
            entity.HasIndex(d => d.OwnerId);
            entity.HasIndex(d => d.Status);
        });

        // ── KnowledgeChunk (pgvector — RAG ghi trực tiếp, snake_case) ─
        modelBuilder.Entity<KnowledgeChunk>(entity =>
        {
            entity.ToTable("knowledge_chunks");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.DocumentId).HasColumnName("document_id");
            entity.Property(c => c.OwnerId).HasColumnName("owner_id");
            entity.Property(c => c.Scope).HasColumnName("scope").IsRequired().HasMaxLength(20);
            entity.Property(c => c.ChunkIndex).HasColumnName("chunk_index");
            entity.Property(c => c.Content).HasColumnName("content").IsRequired();
            entity.Property(c => c.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(c => c.Embedding).HasColumnName("embedding").HasColumnType("vector(768)");
            entity.Property(c => c.CreatedAt).HasColumnName("created_at");

            entity.HasOne(c => c.Document)
                  .WithMany(d => d.Chunks)
                  .HasForeignKey(c => c.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => c.DocumentId).HasDatabaseName("ix_knowledge_chunks_document_id");
            entity.HasIndex(c => new { c.Scope, c.OwnerId }).HasDatabaseName("ix_knowledge_chunks_scope_owner");
        });

        // ── QuestionGenerationJob ───────────────────────────────────
        modelBuilder.Entity<QuestionGenerationJob>(entity =>
        {
            entity.ToTable("question_generation_jobs");
            entity.HasKey(j => j.Id);
            entity.Property(j => j.JobDescription).IsRequired();
            entity.Property(j => j.HrNote).HasMaxLength(2000);
            entity.Property(j => j.JdInputType).IsRequired().HasMaxLength(10).HasDefaultValue(JdInputType.Text);
            entity.Property(j => j.JdFileName).HasMaxLength(500);
            entity.Property(j => j.Difficulty).IsRequired().HasMaxLength(20);
            entity.Property(j => j.QuestionTypesJson).IsRequired().HasColumnType("jsonb");
            entity.Property(j => j.SkillsJson).IsRequired().HasColumnType("jsonb");
            entity.Property(j => j.Status).IsRequired().HasMaxLength(30);
            entity.Property(j => j.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(j => j.OwnerId);
            entity.HasIndex(j => j.Status);
        });

        // ── QuestionGenerationPlan ──────────────────────────────────
        modelBuilder.Entity<QuestionGenerationPlan>(entity =>
        {
            entity.ToTable("question_generation_plans");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.PlanJson).IsRequired().HasColumnType("jsonb");

            entity.HasOne(p => p.Job)
                  .WithOne(j => j.Plan)
                  .HasForeignKey<QuestionGenerationPlan>(p => p.JobId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => p.JobId).IsUnique();
        });

        // ── GeneratedQuestion ───────────────────────────────────────
        modelBuilder.Entity<GeneratedQuestion>(entity =>
        {
            entity.ToTable("generated_questions");
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Question).IsRequired();
            entity.Property(q => q.QuestionType).IsRequired().HasMaxLength(50);
            entity.Property(q => q.Difficulty).IsRequired().HasMaxLength(20);
            entity.Property(q => q.Skill).HasMaxLength(200);
            entity.Property(q => q.FocusArea).HasMaxLength(200);
            entity.Property(q => q.EvaluationCriteriaJson).IsRequired().HasColumnType("jsonb");
            entity.Property(q => q.CitationsJson).IsRequired().HasColumnType("jsonb");

            entity.HasOne(q => q.Job)
                  .WithMany(j => j.Questions)
                  .HasForeignKey(q => q.JobId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(q => q.JobId);
        });
    }
}
