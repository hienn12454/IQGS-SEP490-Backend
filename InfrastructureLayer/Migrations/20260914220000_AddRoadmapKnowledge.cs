using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// SCRUM-455: curated roadmap knowledge nodes + cột priority/kind trên candidate roadmap.
/// Seed node lấy từ docs/kb-seed/dotnet-junior/roadmap/*.md (dữ liệu nhóm đã có, không tự invent).
/// </summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260914220000_AddRoadmapKnowledge")]
public partial class AddRoadmapKnowledge : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "PriorityScore",
            table: "tbl_candidate_roadmaps",
            type: "double precision",
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.AddColumn<string>(
            name: "Kind",
            table: "tbl_candidate_roadmaps",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "gap");

        migrationBuilder.CreateTable(
            name: "tbl_roadmap_nodes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RoleKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Technology = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Level = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Skill = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Topic = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Subtopic = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                Importance = table.Column<double>(type: "double precision", nullable: false),
                PrerequisitesJson = table.Column<string>(type: "jsonb", nullable: false),
                NextTopicsJson = table.Column<string>(type: "jsonb", nullable: false),
                SourceTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                SourceVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                KnowledgeDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_roadmap_nodes", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tbl_roadmap_nodes_RoleKey_Level_Skill_Topic",
            table: "tbl_roadmap_nodes",
            columns: new[] { "RoleKey", "Level", "Skill", "Topic" },
            unique: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RoadmapNodeId",
            table: "tbl_candidate_roadmap_items",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_tbl_candidate_roadmap_items_RoadmapNodeId",
            table: "tbl_candidate_roadmap_items",
            column: "RoadmapNodeId");

        migrationBuilder.AddForeignKey(
            name: "FK_tbl_candidate_roadmap_items_nodes",
            table: "tbl_candidate_roadmap_items",
            column: "RoadmapNodeId",
            principalTable: "tbl_roadmap_nodes",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        // Seed từ markdown curated của nhóm (docs/kb-seed/dotnet-junior/roadmap) — không invent weight/target.
        migrationBuilder.Sql(@"
INSERT INTO tbl_roadmap_nodes (
    ""Id"", ""RoleKey"", ""Technology"", ""Level"", ""Skill"", ""Topic"", ""Subtopic"",
    ""Importance"", ""PrerequisitesJson"", ""NextTopicsJson"",
    ""SourceTitle"", ""SourceUrl"", ""SourceVersion"", ""KnowledgeDocumentId"",
    ""SortOrder"", ""CreatedAt"", ""UpdatedAt"", ""IsActive"")
VALUES
    ('c3000000-0000-0000-0000-000000000101', 'dotnet-backend', 'C#', 'Junior', 'C#',
     'Types, nullability, collections', NULL, 0.9,
     '[]'::jsonb, '[""OOP patterns used in services""]'::jsonb,
     'IQGS kb-seed csharp-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/csharp-roadmap.md', '2026-09',
     NULL, 1, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('c3000000-0000-0000-0000-000000000102', 'dotnet-backend', 'C#', 'Junior', 'C#',
     'OOP patterns used in services', NULL, 0.85,
     '[""Types, nullability, collections""]'::jsonb, '[""async/await pitfalls""]'::jsonb,
     'IQGS kb-seed csharp-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/csharp-roadmap.md', '2026-09',
     NULL, 2, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('c3000000-0000-0000-0000-000000000103', 'dotnet-backend', 'C#', 'Junior', 'C#',
     'async/await pitfalls', NULL, 0.8,
     '[""OOP patterns used in services""]'::jsonb, '[""LINQ for filtering projections""]'::jsonb,
     'IQGS kb-seed csharp-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/csharp-roadmap.md', '2026-09',
     NULL, 3, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('c3000000-0000-0000-0000-000000000104', 'dotnet-backend', 'C#', 'Junior', 'C#',
     'LINQ for filtering projections', NULL, 0.75,
     '[""async/await pitfalls""]'::jsonb, '[""Mini project: console or API helper""]'::jsonb,
     'IQGS kb-seed csharp-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/csharp-roadmap.md', '2026-09',
     NULL, 4, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('c3000000-0000-0000-0000-000000000105', 'dotnet-backend', 'C#', 'Junior', 'C#',
     'Mini project: console or API helper', NULL, 0.7,
     '[""LINQ for filtering projections""]'::jsonb, '[]'::jsonb,
     'IQGS kb-seed csharp-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/csharp-roadmap.md', '2026-09',
     NULL, 5, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),

    ('c3000000-0000-0000-0000-000000000201', 'dotnet-backend', 'ASP.NET Core', 'Junior', 'ASP.NET Core',
     'Create Web API project structure', NULL, 0.9,
     '[]'::jsonb, '[""DI registration patterns""]'::jsonb,
     'IQGS kb-seed aspnet-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/aspnet-roadmap.md', '2026-09',
     NULL, 1, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('c3000000-0000-0000-0000-000000000202', 'dotnet-backend', 'ASP.NET Core', 'Junior', 'ASP.NET Core',
     'DI registration patterns', NULL, 0.85,
     '[""Create Web API project structure""]'::jsonb, '[""Middleware + exception handling""]'::jsonb,
     'IQGS kb-seed aspnet-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/aspnet-roadmap.md', '2026-09',
     NULL, 2, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('c3000000-0000-0000-0000-000000000203', 'dotnet-backend', 'ASP.NET Core', 'Junior', 'ASP.NET Core',
     'Middleware + exception handling', NULL, 0.8,
     '[""DI registration patterns""]'::jsonb, '[""Auth JWT endpoints""]'::jsonb,
     'IQGS kb-seed aspnet-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/aspnet-roadmap.md', '2026-09',
     NULL, 3, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('c3000000-0000-0000-0000-000000000204', 'dotnet-backend', 'ASP.NET Core', 'Junior', 'ASP.NET Core',
     'Auth JWT endpoints', NULL, 0.75,
     '[""Middleware + exception handling""]'::jsonb, '[""Drill: implement CRUD controller with validation""]'::jsonb,
     'IQGS kb-seed aspnet-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/aspnet-roadmap.md', '2026-09',
     NULL, 4, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('c3000000-0000-0000-0000-000000000205', 'dotnet-backend', 'ASP.NET Core', 'Junior', 'ASP.NET Core',
     'Drill: implement CRUD controller with validation', NULL, 0.7,
     '[""Auth JWT endpoints""]'::jsonb, '[]'::jsonb,
     'IQGS kb-seed aspnet-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/aspnet-roadmap.md', '2026-09',
     NULL, 5, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),

    ('c3000000-0000-0000-0000-000000000301', 'dotnet-backend', 'SQL', 'Junior', 'SQL',
     'Schema design for 1-N relations', NULL, 0.9,
     '[]'::jsonb, '[""Write JOIN queries""]'::jsonb,
     'IQGS kb-seed sql-efcore-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/sql-efcore-roadmap.md', '2026-09',
     NULL, 1, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('c3000000-0000-0000-0000-000000000302', 'dotnet-backend', 'SQL', 'Junior', 'SQL',
     'Write JOIN queries', NULL, 0.85,
     '[""Schema design for 1-N relations""]'::jsonb, '[""Indexing""]'::jsonb,
     'IQGS kb-seed sql-efcore-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/sql-efcore-roadmap.md', '2026-09',
     NULL, 2, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('c3000000-0000-0000-0000-000000000303', 'dotnet-backend', 'SQL', 'Junior', 'SQL',
     'Indexing', NULL, 0.8,
     '[""Write JOIN queries""]'::jsonb, '[]'::jsonb,
     'IQGS kb-seed sql-efcore-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/sql-efcore-roadmap.md', '2026-09',
     NULL, 3, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),

    ('c3000000-0000-0000-0000-000000000401', 'dotnet-backend', 'EF Core', 'Junior', 'EF Core',
     'Map entities + Fluent API', NULL, 0.85,
     '[""Schema design for 1-N relations""]'::jsonb, '[""Migration workflow""]'::jsonb,
     'IQGS kb-seed sql-efcore-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/sql-efcore-roadmap.md', '2026-09',
     NULL, 1, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('c3000000-0000-0000-0000-000000000402', 'dotnet-backend', 'EF Core', 'Junior', 'EF Core',
     'Migration workflow', NULL, 0.8,
     '[""Map entities + Fluent API""]'::jsonb, '[""Fix N+1 with Include / projection""]'::jsonb,
     'IQGS kb-seed sql-efcore-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/sql-efcore-roadmap.md', '2026-09',
     NULL, 2, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE),
    ('c3000000-0000-0000-0000-000000000403', 'dotnet-backend', 'EF Core', 'Junior', 'EF Core',
     'Fix N+1 with Include / projection', NULL, 0.75,
     '[""Migration workflow""]'::jsonb, '[]'::jsonb,
     'IQGS kb-seed sql-efcore-roadmap', 'docs/kb-seed/dotnet-junior/roadmap/sql-efcore-roadmap.md', '2026-09',
     NULL, 3, TIMESTAMPTZ '2026-09-14 00:00:00+00', NULL, TRUE)
ON CONFLICT (""RoleKey"", ""Level"", ""Skill"", ""Topic"") DO NOTHING;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_tbl_candidate_roadmap_items_nodes",
            table: "tbl_candidate_roadmap_items");
        migrationBuilder.DropIndex(
            name: "IX_tbl_candidate_roadmap_items_RoadmapNodeId",
            table: "tbl_candidate_roadmap_items");
        migrationBuilder.DropColumn(name: "RoadmapNodeId", table: "tbl_candidate_roadmap_items");
        migrationBuilder.DropTable(name: "tbl_roadmap_nodes");
        migrationBuilder.DropColumn(name: "PriorityScore", table: "tbl_candidate_roadmaps");
        migrationBuilder.DropColumn(name: "Kind", table: "tbl_candidate_roadmaps");
    }
}
