using DomainLayer.Constants;
using DomainLayer.Entities;
using DomainLayer.Studio;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace InfrastructureLayer.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<Feedback> Feedbacks { get; set; }
    public DbSet<HRProfile> HRProfiles { get; set; }
    public DbSet<CandidateProfile> CandidateProfiles { get; set; }
    public DbSet<KnowledgeDocument> KnowledgeDocuments { get; set; }
    public DbSet<KnowledgeChunk> KnowledgeChunks { get; set; }
    // V1 generate pipeline dropped (Phase 4) — no DbSet for jobs/plans/generated/ai-chat
    public DbSet<QuestionSet> QuestionSets { get; set; }
    public DbSet<QuestionSetQuestion> QuestionSetQuestions { get; set; }
    public DbSet<CandidatePersonalSetJob> CandidatePersonalSetJobs { get; set; }
    public DbSet<CandidateSkillPlan> CandidateSkillPlans { get; set; }
    public DbSet<CandidateSkillPlanItem> CandidateSkillPlanItems { get; set; }
    public DbSet<CompetencyFramework> CompetencyFrameworks { get; set; }
    public DbSet<CompetencyFrameworkSkill> CompetencyFrameworkSkills { get; set; }
    public DbSet<CompetencyRoleAlias> CompetencyRoleAliases { get; set; }
    public DbSet<CompetencyRoleFamily> CompetencyRoleFamilies { get; set; }
    public DbSet<CompetencyRoleFamilyAlias> CompetencyRoleFamilyAliases { get; set; }
    public DbSet<CompetencyScoringPolicy> CompetencyScoringPolicies { get; set; }
    public DbSet<CompetencyLevelRule> CompetencyLevelRules { get; set; }
    public DbSet<CandidateAssessment> CandidateAssessments { get; set; }
    public DbSet<CandidateAssessmentSkillResult> CandidateAssessmentSkillResults { get; set; }
    public DbSet<CandidateRoadmap> CandidateRoadmaps { get; set; }
    public DbSet<CandidateRoadmapItem> CandidateRoadmapItems { get; set; }
    public DbSet<RoadmapNode> RoadmapNodes { get; set; }
    public DbSet<QuestionSetBookmark> QuestionSetBookmarks { get; set; }
    public DbSet<HrQuestionSetBookmark> HrQuestionSetBookmarks { get; set; }
    public DbSet<QuestionSetFeedback> QuestionSetFeedbacks { get; set; }
    public DbSet<QuestionSetJdFitReview> QuestionSetJdFitReviews { get; set; }
    public DbSet<PracticeSession> PracticeSessions { get; set; }
    public DbSet<CandidateAnswer> CandidateAnswers { get; set; }
    public DbSet<AiFeedback> AiFeedbacks { get; set; }
    public DbSet<CandidateRecommendation> CandidateRecommendations { get; set; }
    public DbSet<CandidateInvitation> CandidateInvitations { get; set; }
    public DbSet<CandidateOffer> CandidateOffers { get; set; }
    public DbSet<DomainLayer.Entities.PlatformSettings> PlatformSettings { get; set; }
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<UsageCounter> UsageCounters { get; set; }
    public DbSet<SubscriptionTransaction> SubscriptionTransactions { get; set; }
    public DbSet<UserProgress> UserProgresses { get; set; }
    public DbSet<XpTransaction> XpTransactions { get; set; }
    public DbSet<DailyProgress> DailyProgresses { get; set; }
    public DbSet<UserAchievement> UserAchievements { get; set; }
    public DbSet<InterviewProject> InterviewProjects { get; set; }
    public DbSet<JobDescription> StudioJobDescriptions { get; set; }
    public DbSet<StudioKnowledgeDocument> StudioKnowledgeDocuments { get; set; }
    public DbSet<AiChatSession> AiChatSessions { get; set; }
    public DbSet<AiChatMessage> AiChatMessages { get; set; }
    public DbSet<InterviewPlan> InterviewPlans { get; set; }
    public DbSet<PlanSection> PlanSections { get; set; }
    public DbSet<PlanFocusArea> PlanFocusAreas { get; set; }
    public DbSet<PlanApprovalHistory> PlanApprovalHistories { get; set; }
    public DbSet<StudioSettings> StudioSettings { get; set; }
    public DbSet<StudioFocusArea> StudioFocusAreas { get; set; }
    public DbSet<InterviewQuestion> InterviewQuestions { get; set; }
    public DbSet<QuestionGenerationRun> QuestionGenerationRuns { get; set; }
    public DbSet<ProjectShare> ProjectShares { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        // Domain classes kept for DTOs/services compile — tables dropped Phase 4
        modelBuilder.Ignore<QuestionGenerationJob>();
        modelBuilder.Ignore<QuestionGenerationPlan>();
        modelBuilder.Ignore<GeneratedQuestion>();
        modelBuilder.Ignore<QuestionAiChatMessage>();

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly,
            type => type.Namespace is not null && type.Namespace.Contains(".Configurations.Studio"));

        // ── Role (lookup table) ─────────────────────────────────────
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("tbl_roles");
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
            entity.ToTable("tbl_users");
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
            entity.Property(u => u.GithubId).HasMaxLength(255);
            // Unique trên GithubId nhưng cho phép nhiều row NULL (chưa liên kết GitHub).
            entity.HasIndex(u => u.GithubId)
                  .IsUnique()
                  .HasFilter("\"GithubId\" IS NOT NULL");
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
            entity.ToTable("tbl_companies");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(255);
            entity.Property(c => c.LogoUrl).HasMaxLength(500);
            entity.Property(c => c.WebsiteUrl).HasMaxLength(500);
            entity.HasIndex(c => c.Name);
        });

        // ── Feedback ────────────────────────────────────────────────
        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.ToTable("tbl_feedbacks");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.Content).IsRequired().HasMaxLength(1000);
            entity.Property(f => f.Status).IsRequired().HasMaxLength(20);

            entity.HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(f => f.Status);
        });

        // ── HRProfile ───────────────────────────────────────────────
        modelBuilder.Entity<HRProfile>(entity =>
        {
            entity.ToTable("tbl_hr_profiles");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.JobTitle).HasMaxLength(150);
            entity.Property(p => p.InviteMessageTemplate).HasMaxLength(4000);
            entity.Property(p => p.PhoneNumber).HasMaxLength(20);
            entity.Property(p => p.LinkedInUrl).HasMaxLength(500);
            entity.Property(p => p.GithubUrl).HasMaxLength(500);
            entity.Property(p => p.IsCompanyVerified).IsRequired().HasDefaultValue(false);
            // SCRUM-424: prefs hiển thị list recommendation
            entity.Property(p => p.RecDefaultSortBy).IsRequired().HasMaxLength(16).HasDefaultValue("score");
            entity.Property(p => p.RecDefaultSortDir).IsRequired().HasMaxLength(8).HasDefaultValue("desc");
            entity.Property(p => p.RecHideDismissed).IsRequired().HasDefaultValue(false);

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
            entity.ToTable("tbl_candidate_profiles");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.TargetRole).HasMaxLength(150);
            entity.Property(p => p.SeniorityLevel).HasMaxLength(50);
            entity.Property(p => p.TechStack).HasColumnType("text[]");
            entity.Property(p => p.PhoneNumber).HasMaxLength(20);
            entity.Property(p => p.LinkedInUrl).HasMaxLength(500);
            entity.Property(p => p.GithubUrl).HasMaxLength(500);
            entity.Property(p => p.Address).HasMaxLength(500);
            entity.Property(p => p.TimeZoneId).HasMaxLength(100);
            entity.Property(p => p.SuggestedRole).HasMaxLength(200);
            entity.Property(p => p.SelfAssessedLevel).HasMaxLength(30);
            entity.Property(p => p.TargetLevel).HasMaxLength(30);
            entity.Property(p => p.InterviewGoal).HasMaxLength(500);
            entity.Property(p => p.CoachContextConfirmed).IsRequired().HasDefaultValue(false);

            entity.Property(p => p.AllowRecruiterRecommendation).IsRequired().HasDefaultValue(true);
            entity.Property(p => p.AutoSyncProfileFromCv).IsRequired().HasDefaultValue(true);
            entity.Property(p => p.CvSyncLockedFields).HasColumnType("text[]");

            entity.HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<CandidateProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(p => p.UserId).IsUnique();
        });

        // ── KnowledgeDocument (snake_case — shared DB với RAG/migration thủ công) ─
        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.ToTable("tbl_knowledge_documents");
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
            entity.Property(d => d.AdminNote).HasColumnName("admin_note").HasMaxLength(2000);
            entity.Property(d => d.Folder).HasColumnName("folder").HasMaxLength(64);
            entity.Property(d => d.CreatedAt).HasColumnName("created_at");
            entity.Property(d => d.UpdatedAt).HasColumnName("updated_at");
            entity.Property(d => d.IsActive).HasColumnName("is_active");
            entity.HasIndex(d => d.Scope).HasDatabaseName("ix_knowledge_documents_scope");
            entity.HasIndex(d => d.OwnerId).HasDatabaseName("ix_knowledge_documents_owner_id");
            entity.HasIndex(d => d.Status).HasDatabaseName("ix_knowledge_documents_status");
            entity.HasIndex(d => new { d.Scope, d.Folder }).HasDatabaseName("ix_knowledge_documents_scope_folder");
        });

        // ── KnowledgeChunk (pgvector — RAG ghi trực tiếp, snake_case) ─
        modelBuilder.Entity<KnowledgeChunk>(entity =>
        {
            entity.ToTable("tbl_knowledge_chunks");
            entity.HasKey(c => c.Id);
            // Bắt buộc map "id" — thiếu thì EF query "Id" → 500, FE hiện "Chưa có chunk".
            entity.Property(c => c.Id).HasColumnName("id");
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

        // V1 QuestionGenerationJob / Plan / GeneratedQuestion / QuestionAiChatMessage — tables dropped (Ignore above)

        // ── QuestionSet (snapshot Save private / Publish marketplace) ─
        modelBuilder.Entity<QuestionSet>(entity =>
        {
            entity.ToTable("tbl_question_sets");
            entity.HasKey(qs => qs.Id);
            entity.Property(qs => qs.Status).IsRequired().HasMaxLength(20);
            entity.Property(qs => qs.Kind).IsRequired().HasMaxLength(20).HasDefaultValue(QuestionSetKind.Marketplace);
            entity.Property(qs => qs.Title).HasMaxLength(500);
            entity.Property(qs => qs.JobDescription).IsRequired();
            entity.Property(qs => qs.JdSourceType).IsRequired().HasMaxLength(20).HasDefaultValue("PastedText");
            entity.Property(qs => qs.JdOriginalFileName).HasMaxLength(260);
            entity.Property(qs => qs.JdBlobPath).HasMaxLength(1000);
            entity.Property(qs => qs.PublicJobDescription);
            // SCRUM-468: metadata tin tuyển (chỉ meaningful khi IsHiringAssessment)
            entity.Property(qs => qs.JobLocation).HasMaxLength(200);
            entity.Property(qs => qs.WorkplaceType).HasMaxLength(20);
            entity.Property(qs => qs.SalaryMin);
            entity.Property(qs => qs.SalaryMax);
            entity.Property(qs => qs.SalaryNegotiable).IsRequired().HasDefaultValue(true);
            entity.Property(qs => qs.JobExpertise).HasMaxLength(120);
            entity.Property(qs => qs.JobDomain).HasMaxLength(120);
            entity.Property(qs => qs.HrNote).HasMaxLength(2000);
            entity.Property(qs => qs.PlanJson).IsRequired().HasColumnType("jsonb");

            // SourceJobId optional legacy — no FK after V1 tables dropped
            entity.Ignore(qs => qs.SourceJob);

            // Cầu product ← Studio (thay SourceJobId → job V1): Restrict để hard-delete project không nuốt set published
            entity.HasOne(qs => qs.SourceProject)
                  .WithMany()
                  .HasForeignKey(qs => qs.SourceProjectId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(qs => qs.SourcePlan)
                  .WithMany()
                  .HasForeignKey(qs => qs.SourcePlanId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(qs => qs.SourceRun)
                  .WithMany()
                  .HasForeignKey(qs => qs.SourceRunId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(qs => qs.OwnerId);
            entity.HasIndex(qs => qs.SourceJobId)
                  .IsUnique()
                  .HasFilter("\"SourceJobId\" IS NOT NULL");
            entity.HasIndex(qs => qs.SourceProjectId)
                  .IsUnique()
                  .HasFilter("\"SourceProjectId\" IS NOT NULL");

            // SCRUM-424: intake recommendation theo từng bộ
            entity.Property(qs => qs.AutoRecommendEnabled).IsRequired().HasDefaultValue(true);
            entity.Property(qs => qs.RecommendationMinScore).IsRequired().HasDefaultValue(70.0);

            // SCRUM-464: Practice vs Tuyển
            entity.Property(qs => qs.IsHiringAssessment).IsRequired().HasDefaultValue(false);
            entity.Property(qs => qs.HrAntiCheatEnabled).IsRequired().HasDefaultValue(false);

            // SCRUM-404: pin Marketplace — mặc định false/null; index hỗ trợ sort featured
            entity.Property(qs => qs.IsPinned).IsRequired().HasDefaultValue(false);
            entity.HasIndex(qs => new { qs.IsPinned, qs.PinnedAt });
            entity.HasIndex(qs => qs.SourcePlanId);
            entity.HasIndex(qs => qs.SourceRunId);
            entity.HasIndex(qs => new { qs.Kind, qs.Status, qs.IsActive });
        });

        modelBuilder.Entity<CandidatePersonalSetJob>(entity =>
        {
            entity.ToTable("tbl_candidate_personal_set_jobs");
            entity.HasKey(j => j.Id);
            entity.Property(j => j.Status).IsRequired().HasMaxLength(20);
            entity.Property(j => j.Purpose).IsRequired().HasMaxLength(20).HasDefaultValue(CandidatePersonalSetPurpose.JdGap);
            entity.Property(j => j.JobDescription).IsRequired();
            entity.Property(j => j.CvSkillsJson).IsRequired().HasColumnType("jsonb");
            entity.Property(j => j.GapSkillsJson).IsRequired().HasColumnType("jsonb");
            entity.Property(j => j.FocusSkillsJson).IsRequired().HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(j => j.PlanJson).HasColumnType("jsonb");
            entity.Property(j => j.ErrorMessage).HasMaxLength(4000);

            entity.HasOne(j => j.QuestionSet)
                  .WithMany()
                  .HasForeignKey(j => j.QuestionSetId)
                  .OnDelete(DeleteBehavior.SetNull);

            // AssessmentId lưu tham chiếu lỏng — tránh FK vòng với CandidateAssessment.PersonalSetJobId
            entity.Ignore(j => j.Assessment);

            entity.HasIndex(j => j.CandidateUserId);
            entity.HasIndex(j => j.Status);
            entity.HasIndex(j => j.QuestionSetId);
            entity.HasIndex(j => j.Purpose);
            entity.HasIndex(j => j.AssessmentId);
        });

        modelBuilder.Entity<CompetencyFramework>(entity =>
        {
            entity.ToTable("tbl_competency_frameworks");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.RoleKey).IsRequired().HasMaxLength(100);
            entity.Property(f => f.DisplayRole).IsRequired().HasMaxLength(200);
            entity.Property(f => f.TargetLevel).IsRequired().HasMaxLength(30);
            entity.Property(f => f.Status).IsRequired().HasMaxLength(20);
            entity.Property(f => f.Description).HasMaxLength(1000);
            entity.Property(f => f.Technology).HasMaxLength(200);
            entity.Property(f => f.StackJson).IsRequired().HasColumnType("jsonb");
            entity.Property(f => f.Provenance).IsRequired().HasMaxLength(20);
            entity.Property(f => f.SourceRef).HasMaxLength(300);
            entity.Property(f => f.SourceVersion).HasMaxLength(50);
            entity.Property(f => f.RoleFamilyKey).HasMaxLength(80);
            entity.HasIndex(f => new { f.RoleKey, f.TargetLevel }).IsUnique();
        });

        modelBuilder.Entity<CompetencyRoleFamily>(entity =>
        {
            entity.ToTable("tbl_competency_role_families");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.FamilyKey).IsRequired().HasMaxLength(80);
            entity.Property(f => f.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(f => f.Status).IsRequired().HasMaxLength(20);
            entity.Property(f => f.Description).HasMaxLength(1000);
            entity.HasIndex(f => f.FamilyKey).IsUnique();
            entity.HasMany(f => f.Aliases)
                  .WithOne()
                  .HasForeignKey(a => a.FamilyKey)
                  .HasPrincipalKey(f => f.FamilyKey)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompetencyRoleFamilyAlias>(entity =>
        {
            entity.ToTable("tbl_competency_role_family_aliases");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FamilyKey).IsRequired().HasMaxLength(80);
            entity.Property(a => a.Alias).IsRequired().HasMaxLength(200);
            entity.Property(a => a.MatchKind).IsRequired().HasMaxLength(20);
            entity.HasIndex(a => a.Alias).IsUnique();
            entity.HasIndex(a => a.FamilyKey);
        });

        modelBuilder.Entity<CompetencyRoleAlias>(entity =>
        {
            entity.ToTable("tbl_competency_role_aliases");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.RoleKey).IsRequired().HasMaxLength(100);
            entity.Property(a => a.Alias).IsRequired().HasMaxLength(200);
            entity.Property(a => a.MatchKind).IsRequired().HasMaxLength(20);
            // Alias phải unique toàn hệ thống: cùng một chuỗi không được trỏ 2 role khác nhau.
            entity.HasIndex(a => a.Alias).IsUnique();
            entity.HasIndex(a => a.RoleKey);
        });

        modelBuilder.Entity<CompetencyFrameworkSkill>(entity =>
        {
            entity.ToTable("tbl_competency_framework_skills");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Skill).IsRequired().HasMaxLength(200);
            entity.Property(s => s.RequiredDifficulty).IsRequired().HasMaxLength(20);
            entity.Property(s => s.TopicsJson).IsRequired().HasColumnType("jsonb");
            entity.HasOne(s => s.Framework)
                  .WithMany(f => f.Skills)
                  .HasForeignKey(s => s.FrameworkId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(s => new { s.FrameworkId, s.Skill }).IsUnique();
        });

        modelBuilder.Entity<CompetencyScoringPolicy>(entity =>
        {
            entity.ToTable("tbl_competency_scoring_policies");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.TargetScoreByLevelJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<CompetencyLevelRule>(entity =>
        {
            entity.ToTable("tbl_competency_level_rules");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Level).IsRequired().HasMaxLength(30);
            // Luật level là global: unique theo Level, không có cột role/framework.
            entity.HasIndex(r => r.Level).IsUnique();
        });

        modelBuilder.Entity<CandidateAssessment>(entity =>
        {
            entity.ToTable("tbl_candidate_assessments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Kind).IsRequired().HasMaxLength(30);
            entity.Property(a => a.Status).IsRequired().HasMaxLength(30);
            entity.Property(a => a.ReadinessStatus).HasMaxLength(30);
            entity.Property(a => a.ContextSnapshotJson).IsRequired().HasColumnType("jsonb");
            entity.Property(a => a.ScopeSkillsJson).HasColumnType("jsonb");
            entity.Property(a => a.ExplanationJson).HasColumnType("jsonb");
            entity.Property(a => a.ResolutionMode).IsRequired().HasMaxLength(20);
            entity.Property(a => a.RoleFamilyKey).HasMaxLength(80);
            entity.Property(a => a.BlueprintJson).HasColumnType("jsonb");
            entity.HasOne(a => a.Framework)
                  .WithMany()
                  .HasForeignKey(a => a.FrameworkId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.PreviousAssessment)
                  .WithMany()
                  .HasForeignKey(a => a.PreviousAssessmentId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(a => a.QuestionSet)
                  .WithMany()
                  .HasForeignKey(a => a.QuestionSetId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(a => a.PracticeSession)
                  .WithMany()
                  .HasForeignKey(a => a.PracticeSessionId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(a => a.PersonalSetJob)
                  .WithMany()
                  .HasForeignKey(a => a.PersonalSetJobId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(a => a.CandidateUserId);
            entity.HasIndex(a => a.FrameworkId);
        });

        modelBuilder.Entity<CandidateAssessmentSkillResult>(entity =>
        {
            entity.ToTable("tbl_candidate_assessment_skill_results");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Skill).IsRequired().HasMaxLength(200);
            entity.Property(r => r.DemonstratedDifficulty).HasMaxLength(20);
            entity.Property(r => r.EvidenceJson).IsRequired().HasColumnType("jsonb");
            entity.HasOne(r => r.Assessment)
                  .WithMany(a => a.SkillResults)
                  .HasForeignKey(r => r.AssessmentId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(r => new { r.AssessmentId, r.Skill }).IsUnique();
        });

        modelBuilder.Entity<CandidateRoadmap>(entity =>
        {
            entity.ToTable("tbl_candidate_roadmaps");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Skill).IsRequired().HasMaxLength(200);
            entity.Property(r => r.Priority).IsRequired().HasMaxLength(20);
            entity.Property(r => r.Kind).IsRequired().HasMaxLength(20);
            entity.Property(r => r.Status).IsRequired().HasMaxLength(30);
            entity.Property(r => r.ExplanationJson).HasColumnType("jsonb");
            entity.HasOne(r => r.SourceAssessment)
                  .WithMany()
                  .HasForeignKey(r => r.SourceAssessmentId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.Property(r => r.SourceMode).IsRequired().HasMaxLength(20);
            entity.Property(r => r.AcceptedAt);
            entity.HasOne(r => r.Framework)
                  .WithMany()
                  .HasForeignKey(r => r.FrameworkId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(r => r.CandidateUserId);
        });

        modelBuilder.Entity<CandidateRoadmapItem>(entity =>
        {
            entity.ToTable("tbl_candidate_roadmap_items");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Topic).IsRequired().HasMaxLength(300);
            entity.Property(i => i.Subtopic).HasMaxLength(300);
            entity.Property(i => i.Status).IsRequired().HasMaxLength(30);
            entity.Property(i => i.IsIncluded).IsRequired().HasDefaultValue(true);
            entity.Property(i => i.SourceUrl).HasMaxLength(1000);
            entity.Property(i => i.SourceTitle).HasMaxLength(500);
            entity.HasOne(i => i.Roadmap)
                  .WithMany(r => r.Items)
                  .HasForeignKey(i => i.RoadmapId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(i => i.RoadmapNode)
                  .WithMany()
                  .HasForeignKey(i => i.RoadmapNodeId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(i => i.DrillSession)
                  .WithMany()
                  .HasForeignKey(i => i.DrillSessionId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(i => i.DrillQuestionSet)
                  .WithMany()
                  .HasForeignKey(i => i.DrillQuestionSetId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(i => i.RoadmapId);
        });

        modelBuilder.Entity<RoadmapNode>(entity =>
        {
            entity.ToTable("tbl_roadmap_nodes");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.RoleKey).IsRequired().HasMaxLength(100);
            entity.Property(n => n.Technology).HasMaxLength(200);
            entity.Property(n => n.Level).IsRequired().HasMaxLength(30);
            entity.Property(n => n.Skill).IsRequired().HasMaxLength(200);
            entity.Property(n => n.Topic).IsRequired().HasMaxLength(300);
            entity.Property(n => n.Subtopic).HasMaxLength(300);
            entity.Property(n => n.PrerequisitesJson).IsRequired().HasColumnType("jsonb");
            entity.Property(n => n.NextTopicsJson).IsRequired().HasColumnType("jsonb");
            entity.Property(n => n.SourceTitle).HasMaxLength(500);
            entity.Property(n => n.SourceUrl).HasMaxLength(1000);
            entity.Property(n => n.SourceVersion).HasMaxLength(50);
            entity.HasIndex(n => new { n.RoleKey, n.Level, n.Skill, n.Topic }).IsUnique();
        });

        modelBuilder.Entity<CandidateSkillPlan>(entity =>
        {
            entity.ToTable("tbl_candidate_skill_plans");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Status).IsRequired().HasMaxLength(20);
            entity.Property(p => p.ReadinessStatus).HasMaxLength(30);
            entity.Property(p => p.AchievedLevel).HasMaxLength(30);
            entity.Property(p => p.ResolutionMode).HasMaxLength(20);
            entity.Property(p => p.RoleFamilyKey).HasMaxLength(80);
            entity.Property(p => p.ActiveBlueprintJson).HasColumnType("jsonb");
            entity.Property(p => p.TargetLevel).HasMaxLength(30);
            entity.HasIndex(p => p.CandidateUserId).IsUnique();
            entity.HasOne(p => p.SourceDiagnosticSet)
                  .WithMany()
                  .HasForeignKey(p => p.SourceDiagnosticSetId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CandidateSkillPlanItem>(entity =>
        {
            entity.ToTable("tbl_candidate_skill_plan_items");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Skill).IsRequired().HasMaxLength(200);
            entity.Property(i => i.Status).IsRequired().HasMaxLength(20);
            entity.Property(i => i.DemonstratedDifficulty).HasMaxLength(20);
            entity.Property(i => i.UpdatedFromKind).HasMaxLength(30);
            entity.Property(i => i.SourceMode).HasMaxLength(20);
            entity.HasOne(i => i.Plan)
                  .WithMany(p => p.Items)
                  .HasForeignKey(i => i.PlanId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(i => i.LastSession)
                  .WithMany()
                  .HasForeignKey(i => i.LastSessionId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(i => new { i.PlanId, i.Skill }).IsUnique();
        });

        // ── QuestionSetQuestion ─────────────────────────────────────
        modelBuilder.Entity<QuestionSetQuestion>(entity =>
        {
            entity.ToTable("tbl_question_set_questions");
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Question).IsRequired();
            entity.Property(q => q.QuestionType).IsRequired().HasMaxLength(50);
            entity.Property(q => q.Difficulty).IsRequired().HasMaxLength(20);
            entity.Property(q => q.Skill).HasMaxLength(200);
            entity.Property(q => q.FocusArea).HasMaxLength(200);
            entity.Property(q => q.AttachedImageBlobPath).HasMaxLength(1000);
            entity.Property(q => q.AnswerMethod).IsRequired().HasMaxLength(20).HasDefaultValue("Text");
            entity.Property(q => q.EvaluationCriteriaJson).IsRequired().HasColumnType("jsonb");
            entity.Property(q => q.CitationsJson).IsRequired().HasColumnType("jsonb");

            entity.HasOne(q => q.QuestionSet)
                  .WithMany(qs => qs.Questions)
                  .HasForeignKey(q => q.QuestionSetId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(q => new { q.QuestionSetId, q.Order });
        });

        // ── QuestionSetBookmark (Candidate — SCRUM-275) ─────────────
        modelBuilder.Entity<QuestionSetBookmark>(entity =>
        {
            entity.ToTable("tbl_question_set_bookmarks");
            entity.HasKey(b => b.Id);

            entity.HasOne(b => b.QuestionSet)
                  .WithMany()
                  .HasForeignKey(b => b.QuestionSetId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(b => new { b.CandidateUserId, b.QuestionSetId }).IsUnique();
        });

        // ── HrQuestionSetBookmark (HR — SCRUM-324) ──────────────────
        modelBuilder.Entity<HrQuestionSetBookmark>(entity =>
        {
            entity.ToTable("tbl_hr_question_set_bookmarks");
            entity.HasKey(b => b.Id);

            entity.HasOne(b => b.QuestionSet)
                  .WithMany()
                  .HasForeignKey(b => b.QuestionSetId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(b => new { b.HrUserId, b.QuestionSetId }).IsUnique();
        });

        // ── QuestionSetFeedback (Candidate đánh giá bộ câu hỏi HR) ───
        modelBuilder.Entity<QuestionSetFeedback>(entity =>
        {
            entity.ToTable("tbl_question_set_feedbacks");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.Comment).HasMaxLength(2000);

            entity.HasOne(f => f.QuestionSet)
                  .WithMany()
                  .HasForeignKey(f => f.QuestionSetId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(f => f.CandidateUser)
                  .WithMany()
                  .HasForeignKey(f => f.CandidateUserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // 1 candidate chỉ có 1 feedback / question set — submit lại sẽ update đè.
            entity.HasIndex(f => new { f.QuestionSetId, f.CandidateUserId }).IsUnique();
        });

        // ── QuestionSetJdFitReview (1–1, bản đánh giá JD mới nhất) ──
        modelBuilder.Entity<QuestionSetJdFitReview>(entity =>
        {
            entity.ToTable("tbl_question_set_jd_fit_reviews");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.ReviewJson).IsRequired().HasColumnType("jsonb");
            entity.Property(r => r.ContentHash).IsRequired().HasMaxLength(64);
            entity.Property(r => r.ReviewedAt).IsRequired();

            entity.HasOne(r => r.QuestionSet)
                  .WithOne(qs => qs.JdFitReview)
                  .HasForeignKey<QuestionSetJdFitReview>(r => r.QuestionSetId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => r.QuestionSetId).IsUnique();
        });

        // ── PracticeSession (Candidate — SCRUM-277) ─────────────────
        modelBuilder.Entity<PracticeSession>(entity =>
        {
            entity.ToTable("tbl_practice_sessions");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Status).IsRequired().HasMaxLength(20);
            // SCRUM-305: AI Insight song ngữ + skillsToImprove
            entity.Property(s => s.AiInsightVi).HasMaxLength(2000);
            entity.Property(s => s.AiInsightEn).HasMaxLength(2000);
            entity.Property(s => s.SkillsToImproveJson);
            // SCRUM-446: snapshot anti-cheat + đếm rời tab
            entity.Property(s => s.AntiCheatEnabled).IsRequired().HasDefaultValue(false);
            entity.Property(s => s.AntiCheatMaxTabLeaves).IsRequired().HasDefaultValue(3);
            entity.Property(s => s.TabLeaveCount).IsRequired().HasDefaultValue(0);
            // SCRUM-464: lần complete đầu trên bộ Tuyển
            entity.Property(s => s.IsOfficialTest).IsRequired().HasDefaultValue(false);

            entity.HasOne(s => s.QuestionSet)
                  .WithMany()
                  .HasForeignKey(s => s.QuestionSetId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(s => s.CandidateUserId);
        });

        // ── CandidateAnswer (Candidate — SCRUM-278) ─────────────────
        modelBuilder.Entity<CandidateAnswer>(entity =>
        {
            entity.ToTable("tbl_candidate_answers");
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
            entity.ToTable("tbl_ai_feedbacks");
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
            entity.ToTable("tbl_candidate_recommendations");
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
            entity.ToTable("tbl_candidate_invitations");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Status).IsRequired().HasMaxLength(20);
            entity.Property(i => i.Message).HasMaxLength(2000);
            entity.Property(i => i.ResponseMessage).HasMaxLength(2000);
            entity.Property(i => i.SharedPhoneNumber).HasMaxLength(20);
            entity.Property(i => i.TimeZoneId).HasMaxLength(100);
            entity.Property(i => i.MeetingMode).HasMaxLength(20);
            entity.Property(i => i.MeetingLink).HasMaxLength(2000);
            entity.Property(i => i.Location).HasMaxLength(500);

            entity.HasOne(i => i.Recommendation)
                  .WithMany()
                  .HasForeignKey(i => i.RecommendationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(i => i.RecommendationId).IsUnique();
            entity.HasIndex(i => new { i.CandidateUserId, i.Status });
        });

        // ── CandidateOffer (offer phỏng vấn qua email — độc lập với CandidateInvitation) ─
        modelBuilder.Entity<CandidateOffer>(entity =>
        {
            entity.ToTable("tbl_candidate_offers");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Status).IsRequired().HasMaxLength(20);
            entity.Property(o => o.Message).IsRequired().HasMaxLength(5000);
            entity.Property(o => o.TokenHash).IsRequired().HasMaxLength(64);

            entity.HasOne(o => o.Recommendation)
                  .WithMany()
                  .HasForeignKey(o => o.RecommendationId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Lookup công khai theo token — không unique theo RecommendationId vì cho phép gửi lại.
            entity.HasIndex(o => o.TokenHash).IsUnique();
            entity.HasIndex(o => new { o.RecommendationId, o.CreatedAt });
        });

        // ── PlatformSettings (singleton — Admin runtime config) ─────
        modelBuilder.Entity<DomainLayer.Entities.PlatformSettings>(entity =>
        {
            entity.ToTable("tbl_platform_settings");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.MinQuestionsToPublish).IsRequired().HasDefaultValue(10);
            // SCRUM-404: quy tắc hiển thị Marketplace
            entity.Property(p => p.MaxPinnedSets).IsRequired().HasDefaultValue(5);
            entity.Property(p => p.MinAttemptsForTrending).IsRequired().HasDefaultValue(10);
            // SCRUM-446: anti-cheat toàn hệ thống
            entity.Property(p => p.AntiCheatEnabled).IsRequired().HasDefaultValue(false);
            entity.Property(p => p.AntiCheatMaxTabLeaves).IsRequired().HasDefaultValue(3);

            // Seed đúng 1 dòng cố định — repository luôn đọc/ghi dòng này, không tự tạo mới.
            entity.HasData(new DomainLayer.Entities.PlatformSettings
            {
                Id = DomainLayer.Entities.PlatformSettings.SingletonId,
                MinQuestionsToPublish = 10,
                MaxPinnedSets = 5,
                MinAttemptsForTrending = 10,
                AntiCheatEnabled = false,
                AntiCheatMaxTabLeaves = 3,
                CreatedAt = new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            });
        });

        // ── SubscriptionPlan (Free/Premium templates) ───────────────
        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.ToTable("tbl_subscription_plans");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Code).IsRequired().HasMaxLength(50);
            entity.HasIndex(p => p.Code).IsUnique();
            entity.Property(p => p.Audience).IsRequired().HasMaxLength(20);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Currency).IsRequired().HasMaxLength(10);
            entity.Property(p => p.PriceMonthly).HasPrecision(18, 2);
            entity.Property(p => p.LimitsJson).IsRequired().HasColumnType("jsonb");

            var seedAt = new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc);
            // JSON cố định (camelCase) khớp SubscriptionPlanLimits — không gọi helper lúc design-time
            // Teaser Freemium: Free làm full bài (freeVisiblePercent=100), AI chi tiết chỉ Premium (canDetailedAiFeedback).
            const string hrFreeLimits =
                "{\"generateCooldownHours\":24,\"generatePerWindow\":1,\"generateUnlimited\":false,\"planRegeneratePerDraft\":5,\"questionRegenPerPlan\":2,\"canExport\":false,\"askAiPerMonth\":0,\"canPublish\":true,\"freeVisiblePercent\":100,\"canPersistHrRecommendation\":false,\"feedbackOnlyOnVisible\":false,\"canDetailedAiFeedback\":true,\"freeTeaserFeedbackCount\":0}";
            const string hrPremiumLimits =
                "{\"generateCooldownHours\":0,\"generateUnlimited\":true,\"planRegeneratePerDraft\":5,\"questionRegenPerPlan\":0,\"canExport\":true,\"askAiPerMonth\":1000,\"canPublish\":true,\"freeVisiblePercent\":100,\"canPersistHrRecommendation\":false,\"feedbackOnlyOnVisible\":false,\"canDetailedAiFeedback\":true,\"freeTeaserFeedbackCount\":0}";
            const string candidateFreeLimits =
                "{\"generateCooldownHours\":0,\"generateUnlimited\":false,\"planRegeneratePerDraft\":0,\"canExport\":false,\"askAiPerMonth\":0,\"canPublish\":false,\"freeVisiblePercent\":100,\"canPersistHrRecommendation\":false,\"feedbackOnlyOnVisible\":false,\"canDetailedAiFeedback\":false,\"freeTeaserFeedbackCount\":1,\"canGeneratePersonalSet\":false,\"personalSetPerMonth\":0}";
            const string candidatePremiumLimits =
                "{\"generateCooldownHours\":0,\"generateUnlimited\":false,\"planRegeneratePerDraft\":0,\"canExport\":false,\"askAiPerMonth\":0,\"canPublish\":false,\"freeVisiblePercent\":100,\"canPersistHrRecommendation\":true,\"feedbackOnlyOnVisible\":false,\"canDetailedAiFeedback\":true,\"freeTeaserFeedbackCount\":0,\"canGeneratePersonalSet\":true,\"personalSetPerMonth\":10}";

            entity.HasData(
                new SubscriptionPlan
                {
                    Id = SubscriptionPlanCodes.HrFreeId,
                    Code = SubscriptionPlanCodes.HrFree,
                    Audience = SubscriptionAudience.HR,
                    Name = "HR Free",
                    PriceMonthly = 0,
                    Currency = "VND",
                    LimitsJson = hrFreeLimits,
                    CreatedAt = seedAt,
                    IsActive = true
                },
                new SubscriptionPlan
                {
                    Id = SubscriptionPlanCodes.HrPremiumId,
                    Code = SubscriptionPlanCodes.HrPremium,
                    Audience = SubscriptionAudience.HR,
                    Name = "HR Premium",
                    PriceMonthly = 699000,
                    Currency = "VND",
                    LimitsJson = hrPremiumLimits,
                    CreatedAt = seedAt,
                    IsActive = true
                },
                new SubscriptionPlan
                {
                    Id = SubscriptionPlanCodes.CandidateFreeId,
                    Code = SubscriptionPlanCodes.CandidateFree,
                    Audience = SubscriptionAudience.Candidate,
                    Name = "Candidate Free",
                    PriceMonthly = 0,
                    Currency = "VND",
                    LimitsJson = candidateFreeLimits,
                    CreatedAt = seedAt,
                    IsActive = true
                },
                new SubscriptionPlan
                {
                    Id = SubscriptionPlanCodes.CandidatePremiumId,
                    Code = SubscriptionPlanCodes.CandidatePremium,
                    Audience = SubscriptionAudience.Candidate,
                    Name = "Candidate Premium",
                    PriceMonthly = 149000,
                    Currency = "VND",
                    LimitsJson = candidatePremiumLimits,
                    CreatedAt = seedAt,
                    IsActive = true
                });
        });

        // ── Subscription (per user, anniversary period + snapshot) ──
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("tbl_subscriptions");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Status).IsRequired().HasMaxLength(20);
            entity.Property(s => s.LimitsSnapshotJson).IsRequired().HasColumnType("jsonb");
            entity.Property(s => s.CancelAtPeriodEnd).IsRequired().HasDefaultValue(false);
            entity.HasIndex(s => s.UserId).IsUnique();

            entity.HasOne(s => s.User)
                  .WithMany()
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.Plan)
                  .WithMany(p => p.Subscriptions)
                  .HasForeignKey(s => s.PlanId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── UsageCounter ────────────────────────────────────────────
        modelBuilder.Entity<UsageCounter>(entity =>
        {
            entity.ToTable("tbl_usage_counters");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.UsageType).IsRequired().HasMaxLength(50);
            entity.Property(c => c.ScopeKey).IsRequired().HasMaxLength(100).HasDefaultValue(string.Empty);

            entity.HasOne(c => c.Subscription)
                  .WithMany(s => s.UsageCounters)
                  .HasForeignKey(c => c.SubscriptionId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Unique theo kỳ + type + scope (null scope = tổng)
            entity.HasIndex(c => new { c.SubscriptionId, c.PeriodStart, c.UsageType, c.ScopeKey })
                  .IsUnique();
        });

        // ── SubscriptionTransaction (sandbox) ───────────────────────
        modelBuilder.Entity<SubscriptionTransaction>(entity =>
        {
            entity.ToTable("tbl_subscription_transactions");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Type).IsRequired().HasMaxLength(30);
            entity.Property(t => t.Status).IsRequired().HasMaxLength(20);
            entity.Property(t => t.Provider).IsRequired().HasMaxLength(30);
            entity.Property(t => t.Currency).IsRequired().HasMaxLength(10);
            entity.Property(t => t.Amount).HasPrecision(18, 2);
            entity.Property(t => t.OrderCode).HasMaxLength(80);
            entity.Property(t => t.ExternalTransactionId).HasMaxLength(120);
            entity.Property(t => t.RawPayloadJson).HasColumnType("jsonb");
            entity.Property(t => t.Note).HasMaxLength(500);
            entity.HasIndex(t => t.OrderCode).IsUnique();
            entity.HasIndex(t => t.ExternalTransactionId).IsUnique();

            entity.HasOne(t => t.Subscription)
                  .WithMany(s => s.Transactions)
                  .HasForeignKey(t => t.SubscriptionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── UserProgress (Gamification — snapshot 1-1 với User) ─────
        modelBuilder.Entity<UserProgress>(entity =>
        {
            entity.ToTable("tbl_user_progress");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.TotalXp).IsRequired().HasDefaultValue(0L);
            entity.Property(p => p.Level).IsRequired().HasDefaultValue(1);
            entity.Property(p => p.CurrentStreak).IsRequired().HasDefaultValue(0);
            entity.Property(p => p.LongestStreak).IsRequired().HasDefaultValue(0);
            entity.Property(p => p.DailyGoalXp).IsRequired().HasDefaultValue(50);
            entity.Property(p => p.TotalQuestionsCompleted).IsRequired().HasDefaultValue(0);
            entity.Property(p => p.TotalSessionsCompleted).IsRequired().HasDefaultValue(0);
            entity.Property(p => p.TotalTechnicalQuestionsCompleted).IsRequired().HasDefaultValue(0);
            entity.Property(p => p.TotalSystemDesignQuestionsCompleted).IsRequired().HasDefaultValue(0);
            entity.Property(p => p.DailyGoalCompletedDaysCount).IsRequired().HasDefaultValue(0);

            entity.HasOne(p => p.User)
                  .WithOne()
                  .HasForeignKey<UserProgress>(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(p => p.UserId).IsUnique();
        });

        // ── XpTransaction (Gamification — audit/idempotency) ────────
        modelBuilder.Entity<XpTransaction>(entity =>
        {
            entity.ToTable("tbl_xp_transactions");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Type).IsRequired().HasMaxLength(30);
            entity.Property(t => t.Description).IsRequired().HasMaxLength(500);
            entity.Property(t => t.IdempotencyKey).IsRequired().HasMaxLength(200);

            entity.HasOne(t => t.User)
                  .WithMany()
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Idempotency — cùng 1 sự kiện (vd cùng answerId) không bao giờ được cộng XP 2 lần.
            entity.HasIndex(t => t.IdempotencyKey).IsUnique();
            entity.HasIndex(t => new { t.UserId, t.CreatedAtUtc });
        });

        // ── DailyProgress (Gamification — activity heatmap + daily goal) ─
        modelBuilder.Entity<DailyProgress>(entity =>
        {
            entity.ToTable("tbl_daily_progress");
            entity.HasKey(d => d.Id);

            entity.HasOne(d => d.User)
                  .WithMany()
                  .HasForeignKey(d => d.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(d => new { d.UserId, d.LocalDate }).IsUnique();
        });

        // ── UserAchievement (Gamification) ──────────────────────────
        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.ToTable("tbl_user_achievements");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.AchievementCode).IsRequired().HasMaxLength(50);

            entity.HasOne(a => a.User)
                  .WithMany()
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(a => new { a.UserId, a.AchievementCode }).IsUnique();
        });
    }
}
