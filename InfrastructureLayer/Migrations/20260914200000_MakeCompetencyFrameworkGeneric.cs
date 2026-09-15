using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// SCRUM-453: competency framework trở thành data generic (Role + Technology/Stack + Level)
/// và alias vai trò được chuyển từ code sang bảng dữ liệu.
/// Không insert framework mới: số liệu production chỉ được nạp qua importer curated data.
/// </summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260914200000_MakeCompetencyFrameworkGeneric")]
public partial class MakeCompetencyFrameworkGeneric : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Technology",
            table: "tbl_competency_frameworks",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StackJson",
            table: "tbl_competency_frameworks",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "Provenance",
            table: "tbl_competency_frameworks",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Provisional");

        migrationBuilder.AddColumn<string>(
            name: "SourceRef",
            table: "tbl_competency_frameworks",
            type: "character varying(300)",
            maxLength: 300,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceVersion",
            table: "tbl_competency_frameworks",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "tbl_competency_role_aliases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RoleKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Alias = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                MatchKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_competency_role_aliases", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_tbl_competency_role_aliases_Alias",
            table: "tbl_competency_role_aliases",
            column: "Alias",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_tbl_competency_role_aliases_RoleKey",
            table: "tbl_competency_role_aliases",
            column: "RoleKey");

        // Framework đã seed ở SCRUM-447 là số liệu MVP chưa thẩm định -> đánh dấu Provisional
        // và bổ sung Technology để resolver/roadmap dùng chung một nguồn dữ liệu.
        migrationBuilder.Sql(@"
UPDATE tbl_competency_frameworks
SET ""Provenance"" = 'Provisional',
    ""Technology"" = COALESCE(""Technology"", 'ASP.NET Core'),
    ""StackJson"" = CASE WHEN ""StackJson"" IS NULL OR ""StackJson"" = '[]'::jsonb
                        THEN '[""ASP.NET Core"",""EF Core"",""SQL""]'::jsonb ELSE ""StackJson"" END
WHERE ""RoleKey"" = 'dotnet-backend';
");

        // Alias là DATA: role .NET được resolve qua bảng này thay vì if trong repository.
        // Mỗi role mới (java-backend, react-frontend...) chỉ cần thêm row alias khi import framework.
        migrationBuilder.Sql(@"
INSERT INTO tbl_competency_role_aliases
    (""Id"", ""RoleKey"", ""Alias"", ""MatchKind"", ""SortOrder"", ""CreatedAt"", ""UpdatedAt"", ""IsActive"")
VALUES
    (gen_random_uuid(), 'dotnet-backend', 'dotnet-backend', 'exact', 1, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'dotnet-backend', '.net backend developer', 'exact', 2, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'dotnet-backend', 'asp.net core developer', 'exact', 3, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'dotnet-backend', 'asp.net core', 'contains', 10, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'dotnet-backend', 'dotnet', 'contains', 11, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'dotnet-backend', '.net', 'contains', 12, NOW(), NULL, TRUE),
    (gen_random_uuid(), 'dotnet-backend', 'c# backend', 'contains', 13, NOW(), NULL, TRUE)
ON CONFLICT (""Alias"") DO NOTHING;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tbl_competency_role_aliases");
        migrationBuilder.DropColumn(name: "SourceVersion", table: "tbl_competency_frameworks");
        migrationBuilder.DropColumn(name: "SourceRef", table: "tbl_competency_frameworks");
        migrationBuilder.DropColumn(name: "Provenance", table: "tbl_competency_frameworks");
        migrationBuilder.DropColumn(name: "StackJson", table: "tbl_competency_frameworks");
        migrationBuilder.DropColumn(name: "Technology", table: "tbl_competency_frameworks");
    }
}
