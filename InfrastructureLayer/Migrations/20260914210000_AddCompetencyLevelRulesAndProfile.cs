using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// SCRUM-454: level rule TOÀN CỤC (không gắn role) + competency profile tích luỹ trên
/// tbl_candidate_skill_plans + phạm vi skill của mỗi assessment (full vs partial).
/// Thay đổi thuần additive: cột mới nullable / có default, không xoá dữ liệu cũ.
/// </summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260914210000_AddCompetencyLevelRulesAndProfile")]
public partial class AddCompetencyLevelRulesAndProfile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tbl_competency_level_rules",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Level = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                OverallThreshold = table.Column<double>(type: "double precision", nullable: false),
                TargetMetRatio = table.Column<double>(type: "double precision", nullable: false),
                RequiredDifficultyRatio = table.Column<double>(type: "double precision", nullable: false),
                HardEvidenceRatio = table.Column<double>(type: "double precision", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_competency_level_rules", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tbl_competency_level_rules_Level",
            table: "tbl_competency_level_rules",
            column: "Level",
            unique: true);

        // Competency profile: trạng thái tích luỹ, tính lại sau mỗi lần merge assessment.
        migrationBuilder.AddColumn<Guid>(
            name: "FrameworkId",
            table: "tbl_candidate_skill_plans",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "OverallReadiness",
            table: "tbl_candidate_skill_plans",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ReadinessStatus",
            table: "tbl_candidate_skill_plans",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AchievedLevel",
            table: "tbl_candidate_skill_plans",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "LastAssessmentId",
            table: "tbl_candidate_skill_plans",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DemonstratedDifficulty",
            table: "tbl_candidate_skill_plan_items",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "ImportanceWeight",
            table: "tbl_candidate_skill_plan_items",
            type: "double precision",
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.AddColumn<Guid>(
            name: "SourceAssessmentId",
            table: "tbl_candidate_skill_plan_items",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UpdatedFromKind",
            table: "tbl_candidate_skill_plan_items",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        // Phân biệt assessment full framework vs partial (re-assessment 1 skill).
        migrationBuilder.AddColumn<string>(
            name: "ScopeSkillsJson",
            table: "tbl_candidate_assessments",
            type: "jsonb",
            nullable: true);

        // Seed level rule mặc định (config vận hành, có thể tinh chỉnh sau).
        // Cơ sở: Overall threshold khớp thang readiness đang dùng (50/70/85);
        // ratio tăng dần theo level; HardEvidenceRatio > 0 từ Middle để chặn level cao chỉ nhờ câu dễ.
        migrationBuilder.Sql(@"
INSERT INTO tbl_competency_level_rules
    (""Id"", ""Level"", ""OverallThreshold"", ""TargetMetRatio"", ""RequiredDifficultyRatio"",
     ""HardEvidenceRatio"", ""SortOrder"", ""CreatedAt"", ""UpdatedAt"", ""IsActive"")
VALUES
    (gen_random_uuid(), 'Fresher', 40.0, 0.4, 0.3, 0.0, 1, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'Junior',  60.0, 0.6, 0.6, 0.0, 2, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'Middle',  75.0, 0.8, 0.8, 0.5, 3, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'Senior',  85.0, 0.9, 0.9, 0.8, 4, NOW(), NULL, TRUE)
ON CONFLICT (""Level"") DO NOTHING;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tbl_competency_level_rules");
        migrationBuilder.DropColumn(name: "ScopeSkillsJson", table: "tbl_candidate_assessments");
        migrationBuilder.DropColumn(name: "UpdatedFromKind", table: "tbl_candidate_skill_plan_items");
        migrationBuilder.DropColumn(name: "SourceAssessmentId", table: "tbl_candidate_skill_plan_items");
        migrationBuilder.DropColumn(name: "ImportanceWeight", table: "tbl_candidate_skill_plan_items");
        migrationBuilder.DropColumn(name: "DemonstratedDifficulty", table: "tbl_candidate_skill_plan_items");
        migrationBuilder.DropColumn(name: "LastAssessmentId", table: "tbl_candidate_skill_plans");
        migrationBuilder.DropColumn(name: "AchievedLevel", table: "tbl_candidate_skill_plans");
        migrationBuilder.DropColumn(name: "ReadinessStatus", table: "tbl_candidate_skill_plans");
        migrationBuilder.DropColumn(name: "OverallReadiness", table: "tbl_candidate_skill_plans");
        migrationBuilder.DropColumn(name: "FrameworkId", table: "tbl_candidate_skill_plans");
    }
}
