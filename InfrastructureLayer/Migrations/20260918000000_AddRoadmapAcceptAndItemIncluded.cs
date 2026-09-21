using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>SCRUM-462: preview/accept lộ trình — AcceptedAt trên roadmap, IsIncluded trên item.</summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260918000000_AddRoadmapAcceptAndItemIncluded")]
public partial class AddRoadmapAcceptAndItemIncluded : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "AcceptedAt",
            table: "tbl_candidate_roadmaps",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsIncluded",
            table: "tbl_candidate_roadmap_items",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        // Gate Re-assessment luôn included.
        migrationBuilder.Sql(@"
UPDATE tbl_candidate_roadmap_items
SET ""IsIncluded"" = TRUE
WHERE ""IsReassessmentGate"" = TRUE;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AcceptedAt",
            table: "tbl_candidate_roadmaps");

        migrationBuilder.DropColumn(
            name: "IsIncluded",
            table: "tbl_candidate_roadmap_items");
    }
}
