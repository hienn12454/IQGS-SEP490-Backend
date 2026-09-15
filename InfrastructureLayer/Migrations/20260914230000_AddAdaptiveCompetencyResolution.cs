using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// SCRUM-457: Role Family (data) + Adaptive blueprint persistence + TargetScoreByLevelJson.
/// Family/alias là config — thêm domain SE mới không sửa resolver.
/// </summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260914230000_AddAdaptiveCompetencyResolution")]
public partial class AddAdaptiveCompetencyResolution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tbl_competency_role_families",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FamilyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_competency_role_families", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tbl_competency_role_families_FamilyKey",
            table: "tbl_competency_role_families",
            column: "FamilyKey",
            unique: true);

        migrationBuilder.CreateTable(
            name: "tbl_competency_role_family_aliases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FamilyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Alias = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                MatchKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_competency_role_family_aliases", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tbl_competency_role_family_aliases_Alias",
            table: "tbl_competency_role_family_aliases",
            column: "Alias",
            unique: true);

        migrationBuilder.AddColumn<string>(
            name: "RoleFamilyKey",
            table: "tbl_competency_frameworks",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TargetScoreByLevelJson",
            table: "tbl_competency_scoring_policies",
            type: "jsonb",
            nullable: true);

        migrationBuilder.DropForeignKey(
            name: "FK_tbl_candidate_assessments_frameworks",
            table: "tbl_candidate_assessments");

        migrationBuilder.AlterColumn<Guid>(
            name: "FrameworkId",
            table: "tbl_candidate_assessments",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AddColumn<string>(
            name: "ResolutionMode",
            table: "tbl_candidate_assessments",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "FRAMEWORK");

        migrationBuilder.AddColumn<string>(
            name: "RoleFamilyKey",
            table: "tbl_candidate_assessments",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BlueprintJson",
            table: "tbl_candidate_assessments",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "BlueprintSchemaVersion",
            table: "tbl_candidate_assessments",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddForeignKey(
            name: "FK_tbl_candidate_assessments_frameworks",
            table: "tbl_candidate_assessments",
            column: "FrameworkId",
            principalTable: "tbl_competency_frameworks",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddColumn<string>(
            name: "ResolutionMode",
            table: "tbl_candidate_skill_plans",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RoleFamilyKey",
            table: "tbl_candidate_skill_plans",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ActiveBlueprintJson",
            table: "tbl_candidate_skill_plans",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TargetLevel",
            table: "tbl_candidate_skill_plans",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceMode",
            table: "tbl_candidate_skill_plan_items",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.DropForeignKey(
            name: "FK_tbl_candidate_roadmaps_frameworks",
            table: "tbl_candidate_roadmaps");

        migrationBuilder.AlterColumn<Guid>(
            name: "FrameworkId",
            table: "tbl_candidate_roadmaps",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AddColumn<string>(
            name: "SourceMode",
            table: "tbl_candidate_roadmaps",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "framework");

        migrationBuilder.AddForeignKey(
            name: "FK_tbl_candidate_roadmaps_frameworks",
            table: "tbl_candidate_roadmaps",
            column: "FrameworkId",
            principalTable: "tbl_competency_frameworks",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        SeedRoleFamilies(migrationBuilder);

        migrationBuilder.Sql(@"
UPDATE tbl_competency_frameworks
SET ""RoleFamilyKey"" = 'backend'
WHERE ""RoleKey"" = 'dotnet-backend';
");
    }

    private static void SeedRoleFamilies(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
INSERT INTO tbl_competency_role_families
    (""Id"", ""FamilyKey"", ""DisplayName"", ""Status"", ""SortOrder"", ""Description"", ""CreatedAt"", ""UpdatedAt"", ""IsActive"")
VALUES
    (gen_random_uuid(), 'backend',  'Backend Developer',  'Active', 1, 'Server-side software engineering', NOW(), NULL, TRUE),
    (gen_random_uuid(), 'frontend', 'Frontend Developer', 'Active', 2, 'Client-side / UI software engineering', NOW(), NULL, TRUE),
    (gen_random_uuid(), 'fullstack','Fullstack Developer','Active', 3, 'End-to-end application engineering', NOW(), NULL, TRUE),
    (gen_random_uuid(), 'mobile',   'Mobile Developer',   'Active', 4, 'Native / cross-platform mobile', NOW(), NULL, TRUE),
    (gen_random_uuid(), 'qa',       'QA / Test Engineer', 'Active', 5, 'Quality engineering and testing', NOW(), NULL, TRUE),
    (gen_random_uuid(), 'devops',   'DevOps / SRE',       'Active', 6, 'Platform, CI/CD, reliability', NOW(), NULL, TRUE),
    (gen_random_uuid(), 'data',     'Data / ML Engineer', 'Active', 7, 'Data platform and ML engineering', NOW(), NULL, TRUE),
    (gen_random_uuid(), 'security', 'Security Engineer',  'Active', 8, 'Application / cloud security', NOW(), NULL, TRUE)
ON CONFLICT (""FamilyKey"") DO NOTHING;
");

        migrationBuilder.Sql(@"
INSERT INTO tbl_competency_role_family_aliases
    (""Id"", ""FamilyKey"", ""Alias"", ""MatchKind"", ""SortOrder"", ""CreatedAt"", ""UpdatedAt"", ""IsActive"")
VALUES
    (gen_random_uuid(), 'backend',  'backend developer', 'exact', 1, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'backend',  'backend engineer', 'exact', 2, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'backend',  'be', 'exact', 3, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'backend',  'lập trình backend', 'exact', 4, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'backend',  'backend', 'contains', 10, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'backend',  'server-side', 'contains', 11, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'backend',  'asp.net', 'contains', 12, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'backend',  'spring boot', 'contains', 13, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'frontend', 'frontend developer', 'exact', 1, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'frontend', 'front-end developer', 'exact', 2, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'frontend', 'fe', 'exact', 3, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'frontend', 'lập trình frontend', 'exact', 4, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'frontend', 'frontend', 'contains', 10, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'frontend', 'front-end', 'contains', 11, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'frontend', 'ui developer', 'contains', 12, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'fullstack','fullstack developer', 'exact', 1, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'fullstack','full-stack developer', 'exact', 2, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'fullstack','fullstack', 'contains', 10, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'fullstack','full-stack', 'contains', 11, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'mobile',   'mobile developer', 'exact', 1, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'mobile',   'android developer', 'exact', 2, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'mobile',   'ios developer', 'exact', 3, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'mobile',   'mobile', 'contains', 10, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'mobile',   'android', 'contains', 11, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'mobile',   'react native', 'contains', 12, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'qa',       'qa engineer', 'exact', 1, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'qa',       'test engineer', 'exact', 2, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'qa',       'tester', 'exact', 3, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'qa',       'quality assurance', 'contains', 10, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'qa',       'qa', 'contains', 11, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'devops',   'devops engineer', 'exact', 1, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'devops',   'sre', 'exact', 2, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'devops',   'site reliability', 'contains', 10, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'devops',   'devops', 'contains', 11, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'data',     'data engineer', 'exact', 1, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'data',     'ml engineer', 'exact', 2, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'data',     'machine learning engineer', 'exact', 3, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'data',     'data scientist', 'contains', 10, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'data',     'machine learning', 'contains', 11, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'security', 'security engineer', 'exact', 1, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'security', 'appsec', 'exact', 2, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'security', 'cybersecurity', 'contains', 10, NOW(), NULL, TRUE)
ON CONFLICT (""Alias"") DO NOTHING;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SourceMode", table: "tbl_candidate_roadmaps");
        migrationBuilder.DropForeignKey(name: "FK_tbl_candidate_roadmaps_frameworks", table: "tbl_candidate_roadmaps");
        migrationBuilder.AlterColumn<Guid>(
            name: "FrameworkId",
            table: "tbl_candidate_roadmaps",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);
        migrationBuilder.AddForeignKey(
            name: "FK_tbl_candidate_roadmaps_frameworks",
            table: "tbl_candidate_roadmaps",
            column: "FrameworkId",
            principalTable: "tbl_competency_frameworks",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropColumn(name: "SourceMode", table: "tbl_candidate_skill_plan_items");
        migrationBuilder.DropColumn(name: "TargetLevel", table: "tbl_candidate_skill_plans");
        migrationBuilder.DropColumn(name: "ActiveBlueprintJson", table: "tbl_candidate_skill_plans");
        migrationBuilder.DropColumn(name: "RoleFamilyKey", table: "tbl_candidate_skill_plans");
        migrationBuilder.DropColumn(name: "ResolutionMode", table: "tbl_candidate_skill_plans");

        migrationBuilder.DropColumn(name: "BlueprintSchemaVersion", table: "tbl_candidate_assessments");
        migrationBuilder.DropColumn(name: "BlueprintJson", table: "tbl_candidate_assessments");
        migrationBuilder.DropColumn(name: "RoleFamilyKey", table: "tbl_candidate_assessments");
        migrationBuilder.DropColumn(name: "ResolutionMode", table: "tbl_candidate_assessments");
        migrationBuilder.DropForeignKey(name: "FK_tbl_candidate_assessments_frameworks", table: "tbl_candidate_assessments");
        migrationBuilder.AlterColumn<Guid>(
            name: "FrameworkId",
            table: "tbl_candidate_assessments",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);
        migrationBuilder.AddForeignKey(
            name: "FK_tbl_candidate_assessments_frameworks",
            table: "tbl_candidate_assessments",
            column: "FrameworkId",
            principalTable: "tbl_competency_frameworks",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropColumn(name: "TargetScoreByLevelJson", table: "tbl_competency_scoring_policies");
        migrationBuilder.DropColumn(name: "RoleFamilyKey", table: "tbl_competency_frameworks");
        migrationBuilder.DropTable(name: "tbl_competency_role_family_aliases");
        migrationBuilder.DropTable(name: "tbl_competency_role_families");
    }
}
