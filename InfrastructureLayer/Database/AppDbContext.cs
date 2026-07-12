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
    public DbSet<QuestionSet> QuestionSets { get; set; }
    public DbSet<QuestionSetQuestion> QuestionSetQuestions { get; set; }
    public DbSet<QuestionAiChatMessage> QuestionAiChatMessages { get; set; }
    public DbSet<QuestionSetBookmark> QuestionSetBookmarks { get; set; }
    public DbSet<PracticeSession> PracticeSessions { get; set; }
    public DbSet<CandidateAnswer> CandidateAnswers { get; set; }
    public DbSet<AiFeedback> AiFeedbacks { get; set; }
    public DbSet<CandidateRecommendation> CandidateRecommendations { get; set; }
    public DbSet<CandidateInvitation> CandidateInvitations { get; set; }
    public DbSet<DomainLayer.Entities.PlatformSettings> PlatformSettings { get; set; }

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
            entity.Property(p => p.Address).HasMaxLength(500);

            entity.Property(p => p.AllowRecruiterRecommendation).IsRequired().HasDefaultValue(true);
            entity.Property(p => p.AutoSyncProfileFromCv).IsRequired().HasDefaultValue(true);

            entity.HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<CandidateProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(p => p.UserId).IsUnique();
        });

        // ── KnowledgeDocument (snake_case — shared DB với RAG/migration thủ công) ─
        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.ToTable("knowledge_documents");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id).HasColumnName("id");
            entity.Property(d => d.Scope).HasColumnName("scope").IsRequired().HasMaxLength(20);
            entity.Property(d => d.OwnerId).HasColumnName("owner_id");
            entity.Property(d => d.FileName).HasColumnName("file_name").IsRequired().HasMaxLength(500);
            entity.Property(d => d.BlobPath).HasColumnName("blob_path").IsRequired().HasMaxLength(1000);
            entity.Property(d => d.ContentHash).HasColumnName("content_hash").HasMaxLength(128);
            entity.Property(d => d.SourceTitle).HasColumnName("source_title").HasMaxLength(500);
            entity.Property(d => d.SourceUrl).HasColumnName("source_url").HasMaxLength(1000);
            entity.Property(d => d.Section).HasColumnName("section").HasMaxLength(200);
            entity.Property(d => d.Year).HasColumnName("year");
            entity.Property(d => d.Status).HasColumnName("status").IsRequired().HasMaxLength(30);
            entity.Property(d => d.ChunkCount).HasColumnName("chunk_count");
            entity.Property(d => d.UploadedBy).HasColumnName("uploaded_by");
            entity.Property(d => d.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
            entity.Property(d => d.CreatedAt).HasColumnName("created_at");
            entity.Property(d => d.UpdatedAt).HasColumnName("updated_at");
            entity.Property(d => d.IsActive).HasColumnName("is_active");
            entity.HasIndex(d => d.Scope).HasDatabaseName("ix_knowledge_documents_scope");
            entity.HasIndex(d => d.OwnerId).HasDatabaseName("ix_knowledge_documents_owner_id");
            entity.HasIndex(d => d.Status).HasDatabaseName("ix_knowledge_documents_status");
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

        // ── QuestionSet (draft snapshot sau khi HR review) ─────────
        modelBuilder.Entity<QuestionSet>(entity =>
        {
            entity.ToTable("question_sets");
            entity.HasKey(qs => qs.Id);
            entity.Property(qs => qs.Status).IsRequired().HasMaxLength(20);
            entity.Property(qs => qs.Title).HasMaxLength(500);
            entity.Property(qs => qs.JobDescription).IsRequired();
            entity.Property(qs => qs.HrNote).HasMaxLength(2000);
            entity.Property(qs => qs.PlanJson).IsRequired().HasColumnType("jsonb");

            entity.HasOne(qs => qs.SourceJob)
                  .WithMany()
                  .HasForeignKey(qs => qs.SourceJobId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(qs => qs.OwnerId);
            entity.HasIndex(qs => qs.SourceJobId).IsUnique();
        });

        // ── QuestionSetQuestion ─────────────────────────────────────
        modelBuilder.Entity<QuestionSetQuestion>(entity =>
        {
            entity.ToTable("question_set_questions");
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Question).IsRequired();
            entity.Property(q => q.QuestionType).IsRequired().HasMaxLength(50);
            entity.Property(q => q.Difficulty).IsRequired().HasMaxLength(20);
            entity.Property(q => q.Skill).HasMaxLength(200);
            entity.Property(q => q.FocusArea).HasMaxLength(200);
            entity.Property(q => q.EvaluationCriteriaJson).IsRequired().HasColumnType("jsonb");
            entity.Property(q => q.CitationsJson).IsRequired().HasColumnType("jsonb");

            entity.HasOne(q => q.QuestionSet)
                  .WithMany(qs => qs.Questions)
                  .HasForeignKey(q => q.QuestionSetId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(q => new { q.QuestionSetId, q.Order });
        });

        // ── QuestionAiChatMessage (Ask AI per question — SCRUM-215) ─
        modelBuilder.Entity<QuestionAiChatMessage>(entity =>
        {
            entity.ToTable("question_ai_chat_messages");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Role).IsRequired().HasMaxLength(10);
            entity.Property(m => m.Content).IsRequired();
            entity.Property(m => m.SuggestionJson).HasColumnType("jsonb");

            entity.HasOne(m => m.Job)
                  .WithMany()
                  .HasForeignKey(m => m.JobId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.Question)
                  .WithMany()
                  .HasForeignKey(m => m.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(m => new { m.JobId, m.QuestionId, m.CreatedAt });
        });

        // ── QuestionSetBookmark (Candidate — SCRUM-275) ─────────────
        modelBuilder.Entity<QuestionSetBookmark>(entity =>
        {
            entity.ToTable("question_set_bookmarks");
            entity.HasKey(b => b.Id);

            entity.HasOne(b => b.QuestionSet)
                  .WithMany()
                  .HasForeignKey(b => b.QuestionSetId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(b => new { b.CandidateUserId, b.QuestionSetId }).IsUnique();
        });

        // ── PracticeSession (Candidate — SCRUM-277) ─────────────────
        modelBuilder.Entity<PracticeSession>(entity =>
        {
            entity.ToTable("practice_sessions");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Status).IsRequired().HasMaxLength(20);

            entity.HasOne(s => s.QuestionSet)
                  .WithMany()
                  .HasForeignKey(s => s.QuestionSetId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(s => s.CandidateUserId);
        });

        // ── CandidateAnswer (Candidate — SCRUM-278) ─────────────────
        modelBuilder.Entity<CandidateAnswer>(entity =>
        {
            entity.ToTable("candidate_answers");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.AnswerText).IsRequired();

            entity.HasOne(a => a.PracticeSession)
                  .WithMany()
                  .HasForeignKey(a => a.PracticeSessionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.QuestionSetQuestion)
                  .WithMany()
                  .HasForeignKey(a => a.QuestionSetQuestionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(a => new { a.PracticeSessionId, a.QuestionSetQuestionId }).IsUnique();
        });

        // ── AiFeedback (Candidate — SCRUM-282) ──────────────────────
        modelBuilder.Entity<AiFeedback>(entity =>
        {
            entity.ToTable("ai_feedbacks");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.StrengthsJson).IsRequired().HasColumnType("jsonb");
            entity.Property(f => f.ImprovementsJson).IsRequired().HasColumnType("jsonb");
            entity.Property(f => f.DimensionScoresJson).HasColumnType("jsonb");
            entity.Property(f => f.EvaluationStatus).IsRequired().HasMaxLength(20);
            entity.Property(f => f.Suggestion).HasColumnType("text");
            entity.Property(f => f.ErrorMessage).HasColumnType("text");

            // 1-1 với candidate_answers — mỗi answer tối đa 1 feedback
            entity.HasOne(f => f.CandidateAnswer)
                  .WithMany()
                  .HasForeignKey(f => f.CandidateAnswerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(f => f.CandidateAnswerId).IsUnique();
        });

        // ── CandidateRecommendation (SCRUM-291) ─────────────────────
        modelBuilder.Entity<CandidateRecommendation>(entity =>
        {
            entity.ToTable("candidate_recommendations");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Status).IsRequired().HasMaxLength(20);

            entity.HasOne(r => r.QuestionSet)
                  .WithMany()
                  .HasForeignKey(r => r.QuestionSetId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.PracticeSession)
                  .WithMany()
                  .HasForeignKey(r => r.PracticeSessionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => new { r.CandidateUserId, r.QuestionSetId }).IsUnique();
            entity.HasIndex(r => new { r.HrOwnerId, r.Status });
        });

        // ── CandidateInvitation (SCRUM-295) ─────────────────────────
        modelBuilder.Entity<CandidateInvitation>(entity =>
        {
            entity.ToTable("candidate_invitations");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Status).IsRequired().HasMaxLength(20);
            entity.Property(i => i.Message).HasMaxLength(2000);

            entity.HasOne(i => i.Recommendation)
                  .WithMany()
                  .HasForeignKey(i => i.RecommendationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(i => i.RecommendationId).IsUnique();
            entity.HasIndex(i => new { i.CandidateUserId, i.Status });
        });

        // ── PlatformSettings (singleton — Admin runtime config) ─────
        modelBuilder.Entity<DomainLayer.Entities.PlatformSettings>(entity =>
        {
            entity.ToTable("platform_settings");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.MinQuestionsToPublish).IsRequired().HasDefaultValue(10);

            // Seed đúng 1 dòng cố định — repository luôn đọc/ghi dòng này, không tự tạo mới.
            entity.HasData(new DomainLayer.Entities.PlatformSettings
            {
                Id = DomainLayer.Entities.PlatformSettings.SingletonId,
                MinQuestionsToPublish = 10,
                CreatedAt = new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            });
        });
    }
}
