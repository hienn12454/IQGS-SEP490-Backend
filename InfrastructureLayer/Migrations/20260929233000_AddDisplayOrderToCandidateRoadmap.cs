using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>SCRUM-484: DisplayOrder trên roadmap — candidate sắp xếp thứ tự luyện skill trong preview.</summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260929233000_AddDisplayOrderToCandidateRoadmap")]
public partial class AddDisplayOrderToCandidateRoadmap : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DisplayOrder",
            table: "tbl_candidate_roadmaps",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        // Backfill: skill ưu tiên cao hơn (PriorityScore lớn) → DisplayOrder nhỏ hơn (học trước).
        migrationBuilder.Sql(@"
WITH ranked AS (
  SELECT ""Id"",
         ROW_NUMBER() OVER (
           PARTITION BY ""CandidateUserId""
           ORDER BY ""PriorityScore"" DESC, ""CreatedAt"" ASC
         ) - 1 AS ord
  FROM tbl_candidate_roadmaps
  WHERE ""IsActive"" = TRUE
)
UPDATE tbl_candidate_roadmaps r
SET ""DisplayOrder"" = ranked.ord
FROM ranked
WHERE r.""Id"" = ranked.""Id"";
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DisplayOrder",
            table: "tbl_candidate_roadmaps");
    }
}
