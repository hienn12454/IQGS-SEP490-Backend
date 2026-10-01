using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>SCRUM-497: khóa retake practice set sau khi integrity terminate (đủ 3 strike).</summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20261001010000_AddIntegrityTerminatedToPracticeSession")]
public partial class AddIntegrityTerminatedToPracticeSession : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IntegrityTerminated",
            table: "tbl_practice_sessions",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_tbl_practice_sessions_CandidateUserId_QuestionSetId_IntegrityTerminated",
            table: "tbl_practice_sessions",
            columns: new[] { "CandidateUserId", "QuestionSetId", "IntegrityTerminated" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_tbl_practice_sessions_CandidateUserId_QuestionSetId_IntegrityTerminated",
            table: "tbl_practice_sessions");

        migrationBuilder.DropColumn(
            name: "IntegrityTerminated",
            table: "tbl_practice_sessions");
    }
}
