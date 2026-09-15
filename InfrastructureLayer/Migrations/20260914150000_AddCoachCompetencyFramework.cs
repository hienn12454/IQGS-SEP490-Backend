using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>SCRUM-447: schema Coach competency + AdminNote + profile context + seed .NET Junior.</summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260914150000_AddCoachCompetencyFramework")]
public partial class AddCoachCompetencyFramework : Migration
{
    private static readonly Guid FrameworkId = Guid.Parse("b2000000-0000-0000-0000-000000000001");
    private static readonly Guid PolicyId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid SkillCsharp = Guid.Parse("b2000000-0000-0000-0000-000000000011");
    private static readonly Guid SkillAspNet = Guid.Parse("b2000000-0000-0000-0000-000000000012");
    private static readonly Guid SkillSql = Guid.Parse("b2000000-0000-0000-0000-000000000013");
    private static readonly Guid SkillRest = Guid.Parse("b2000000-0000-0000-0000-000000000014");
    private static readonly Guid SkillEf = Guid.Parse("b2000000-0000-0000-0000-000000000015");

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SuggestedRole",
            table: "tbl_candidate_profiles",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SelfAssessedLevel",
            table: "tbl_candidate_profiles",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TargetLevel",
            table: "tbl_candidate_profiles",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "YearsOfExperience",
            table: "tbl_candidate_profiles",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InterviewGoal",
            table: "tbl_candidate_profiles",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "CoachContextConfirmed",
            table: "tbl_candidate_profiles",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "CoachContextConfirmedAt",
            table: "tbl_candidate_profiles",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "AssessmentId",
            table: "tbl_candidate_personal_set_jobs",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RoadmapItemId",
            table: "tbl_candidate_personal_set_jobs",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "admin_note",
            table: "tbl_knowledge_documents",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "tbl_competency_scoring_policies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CorrectnessWeight = table.Column<double>(type: "double precision", nullable: false),
                RelevanceWeight = table.Column<double>(type: "double precision", nullable: false),
                ClarityWeight = table.Column<double>(type: "double precision", nullable: false),
                EasyDifficultyWeight = table.Column<double>(type: "double precision", nullable: false),
                MediumDifficultyWeight = table.Column<double>(type: "double precision", nullable: false),
                HardDifficultyWeight = table.Column<double>(type: "double precision", nullable: false),
                DevelopingMaxExclusive = table.Column<double>(type: "double precision", nullable: false),
                NearTargetMaxExclusive = table.Column<double>(type: "double precision", nullable: false),
                ReadyMaxExclusive = table.Column<double>(type: "double precision", nullable: false),
                JuniorReadyCoreSkillRatio = table.Column<double>(type: "double precision", nullable: false),
                OverallReadyThreshold = table.Column<double>(type: "double precision", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_competency_scoring_policies", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "tbl_competency_frameworks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RoleKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                DisplayRole = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                TargetLevel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_competency_frameworks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "tbl_competency_framework_skills",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FrameworkId = table.Column<Guid>(type: "uuid", nullable: false),
                Skill = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ImportanceWeight = table.Column<double>(type: "double precision", nullable: false),
                TargetScore = table.Column<double>(type: "double precision", nullable: false),
                RequiredDifficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                TopicsJson = table.Column<string>(type: "jsonb", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_competency_framework_skills", x => x.Id);
                table.ForeignKey(
                    name: "FK_tbl_competency_framework_skills_frameworks",
                    column: x => x.FrameworkId,
                    principalTable: "tbl_competency_frameworks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "tbl_candidate_assessments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CandidateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                FrameworkId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                PersonalSetJobId = table.Column<Guid>(type: "uuid", nullable: true),
                QuestionSetId = table.Column<Guid>(type: "uuid", nullable: true),
                PracticeSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                PreviousAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                OverallReadiness = table.Column<double>(type: "double precision", nullable: true),
                ReadinessStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                ContextSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                ExplanationJson = table.Column<string>(type: "jsonb", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_candidate_assessments", x => x.Id);
                table.ForeignKey(
                    name: "FK_tbl_candidate_assessments_frameworks",
                    column: x => x.FrameworkId,
                    principalTable: "tbl_competency_frameworks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_tbl_candidate_assessments_previous",
                    column: x => x.PreviousAssessmentId,
                    principalTable: "tbl_candidate_assessments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_tbl_candidate_assessments_question_sets",
                    column: x => x.QuestionSetId,
                    principalTable: "tbl_question_sets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_tbl_candidate_assessments_sessions",
                    column: x => x.PracticeSessionId,
                    principalTable: "tbl_practice_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_tbl_candidate_assessments_jobs",
                    column: x => x.PersonalSetJobId,
                    principalTable: "tbl_candidate_personal_set_jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "tbl_candidate_assessment_skill_results",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                Skill = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                SkillScore = table.Column<double>(type: "double precision", nullable: false),
                TargetScore = table.Column<double>(type: "double precision", nullable: false),
                Gap = table.Column<double>(type: "double precision", nullable: false),
                ImportanceWeight = table.Column<double>(type: "double precision", nullable: false),
                DemonstratedDifficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                EvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_candidate_assessment_skill_results", x => x.Id);
                table.ForeignKey(
                    name: "FK_tbl_candidate_assessment_skill_results_assessments",
                    column: x => x.AssessmentId,
                    principalTable: "tbl_candidate_assessments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "tbl_candidate_roadmaps",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CandidateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                SourceAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                FrameworkId = table.Column<Guid>(type: "uuid", nullable: false),
                Skill = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CurrentScore = table.Column<double>(type: "double precision", nullable: true),
                TargetScore = table.Column<double>(type: "double precision", nullable: false),
                Gap = table.Column<double>(type: "double precision", nullable: false),
                Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                ExplanationJson = table.Column<string>(type: "jsonb", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_candidate_roadmaps", x => x.Id);
                table.ForeignKey(
                    name: "FK_tbl_candidate_roadmaps_assessments",
                    column: x => x.SourceAssessmentId,
                    principalTable: "tbl_candidate_assessments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_tbl_candidate_roadmaps_frameworks",
                    column: x => x.FrameworkId,
                    principalTable: "tbl_competency_frameworks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "tbl_candidate_roadmap_items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RoadmapId = table.Column<Guid>(type: "uuid", nullable: false),
                Topic = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Subtopic = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                IsReassessmentGate = table.Column<bool>(type: "boolean", nullable: false),
                DrillSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                DrillQuestionSetId = table.Column<Guid>(type: "uuid", nullable: true),
                DrillScore = table.Column<double>(type: "double precision", nullable: true),
                SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                SourceTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_candidate_roadmap_items", x => x.Id);
                table.ForeignKey(
                    name: "FK_tbl_candidate_roadmap_items_roadmaps",
                    column: x => x.RoadmapId,
                    principalTable: "tbl_candidate_roadmaps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_tbl_candidate_roadmap_items_sessions",
                    column: x => x.DrillSessionId,
                    principalTable: "tbl_practice_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_tbl_candidate_roadmap_items_sets",
                    column: x => x.DrillQuestionSetId,
                    principalTable: "tbl_question_sets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tbl_competency_frameworks_RoleKey_TargetLevel",
            table: "tbl_competency_frameworks",
            columns: new[] { "RoleKey", "TargetLevel" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_tbl_competency_framework_skills_FrameworkId_Skill",
            table: "tbl_competency_framework_skills",
            columns: new[] { "FrameworkId", "Skill" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_tbl_candidate_assessments_CandidateUserId",
            table: "tbl_candidate_assessments",
            column: "CandidateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_tbl_candidate_assessments_FrameworkId",
            table: "tbl_candidate_assessments",
            column: "FrameworkId");

        migrationBuilder.CreateIndex(
            name: "IX_tbl_candidate_assessment_skill_results_AssessmentId_Skill",
            table: "tbl_candidate_assessment_skill_results",
            columns: new[] { "AssessmentId", "Skill" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_tbl_candidate_roadmaps_CandidateUserId",
            table: "tbl_candidate_roadmaps",
            column: "CandidateUserId");

        migrationBuilder.CreateIndex(
            name: "IX_tbl_candidate_roadmap_items_RoadmapId",
            table: "tbl_candidate_roadmap_items",
            column: "RoadmapId");

        migrationBuilder.CreateIndex(
            name: "IX_tbl_candidate_personal_set_jobs_AssessmentId",
            table: "tbl_candidate_personal_set_jobs",
            column: "AssessmentId");

        // Seed qua SQL thuần — InsertData cần entity trong ModelSnapshot (migration viết tay chưa có Designer).
        migrationBuilder.Sql($@"
INSERT INTO tbl_competency_scoring_policies (
    ""Id"", ""CorrectnessWeight"", ""RelevanceWeight"", ""ClarityWeight"",
    ""EasyDifficultyWeight"", ""MediumDifficultyWeight"", ""HardDifficultyWeight"",
    ""DevelopingMaxExclusive"", ""NearTargetMaxExclusive"", ""ReadyMaxExclusive"",
    ""JuniorReadyCoreSkillRatio"", ""OverallReadyThreshold"",
    ""CreatedAt"", ""UpdatedAt"", ""IsActive"")
VALUES (
    '{PolicyId}', 0.5, 0.3, 0.2, 1.0, 1.5, 2.0, 50.0, 70.0, 85.0, 0.7, 70.0,
    TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE)
ON CONFLICT (""Id"") DO NOTHING;

INSERT INTO tbl_competency_frameworks (
    ""Id"", ""RoleKey"", ""DisplayRole"", ""TargetLevel"", ""Status"", ""Description"",
    ""CreatedAt"", ""UpdatedAt"", ""IsActive"")
VALUES (
    '{FrameworkId}', 'dotnet-backend', '.NET Backend Developer', 'Junior', 'Active',
    'MVP framework: core skills for Junior .NET Backend interview readiness.',
    TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE)
ON CONFLICT (""Id"") DO NOTHING;

INSERT INTO tbl_competency_framework_skills (
    ""Id"", ""FrameworkId"", ""Skill"", ""ImportanceWeight"", ""TargetScore"", ""RequiredDifficulty"",
    ""TopicsJson"", ""SortOrder"", ""CreatedAt"", ""UpdatedAt"", ""IsActive"")
VALUES
    ('{SkillCsharp}', '{FrameworkId}', 'C#', 0.30, 70, 'medium',
     '[""OOP"",""Collections"",""Async"",""LINQ"",""Exception handling""]'::jsonb, 1,
     TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('{SkillAspNet}', '{FrameworkId}', 'ASP.NET Core', 0.25, 70, 'medium',
     '[""Dependency Injection"",""Middleware"",""Routing"",""Configuration""]'::jsonb, 2,
     TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('{SkillSql}', '{FrameworkId}', 'SQL', 0.20, 65, 'medium',
     '[""Joins"",""Indexing"",""Transactions"",""Query optimization""]'::jsonb, 3,
     TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('{SkillRest}', '{FrameworkId}', 'REST API', 0.15, 70, 'medium',
     '[""HTTP methods"",""Status codes"",""Versioning"",""Idempotency""]'::jsonb, 4,
     TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('{SkillEf}', '{FrameworkId}', 'EF Core', 0.10, 65, 'medium',
     '[""DbContext"",""Relationships"",""Tracking"",""Query performance""]'::jsonb, 5,
     TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE)
ON CONFLICT (""Id"") DO NOTHING;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tbl_candidate_roadmap_items");
        migrationBuilder.DropTable(name: "tbl_candidate_assessment_skill_results");
        migrationBuilder.DropTable(name: "tbl_candidate_roadmaps");
        migrationBuilder.DropTable(name: "tbl_competency_framework_skills");
        migrationBuilder.DropTable(name: "tbl_candidate_assessments");
        migrationBuilder.DropTable(name: "tbl_competency_frameworks");
        migrationBuilder.DropTable(name: "tbl_competency_scoring_policies");

        migrationBuilder.DropIndex(
            name: "IX_tbl_candidate_personal_set_jobs_AssessmentId",
            table: "tbl_candidate_personal_set_jobs");

        migrationBuilder.DropColumn(name: "AssessmentId", table: "tbl_candidate_personal_set_jobs");
        migrationBuilder.DropColumn(name: "RoadmapItemId", table: "tbl_candidate_personal_set_jobs");
        migrationBuilder.DropColumn(name: "admin_note", table: "tbl_knowledge_documents");
        migrationBuilder.DropColumn(name: "SuggestedRole", table: "tbl_candidate_profiles");
        migrationBuilder.DropColumn(name: "SelfAssessedLevel", table: "tbl_candidate_profiles");
        migrationBuilder.DropColumn(name: "TargetLevel", table: "tbl_candidate_profiles");
        migrationBuilder.DropColumn(name: "YearsOfExperience", table: "tbl_candidate_profiles");
        migrationBuilder.DropColumn(name: "InterviewGoal", table: "tbl_candidate_profiles");
        migrationBuilder.DropColumn(name: "CoachContextConfirmed", table: "tbl_candidate_profiles");
        migrationBuilder.DropColumn(name: "CoachContextConfirmedAt", table: "tbl_candidate_profiles");
    }
}
