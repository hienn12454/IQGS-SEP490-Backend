using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>SCRUM-464: HR Practice vs Tuyển — flags Studio/QuestionSet + IsOfficialTest trên session.</summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260921010000_AddHiringAssessmentToQuestionSet")]
public partial class AddHiringAssessmentToQuestionSet : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsHiringAssessment",
            table: "tbl_question_sets",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "HrAntiCheatEnabled",
            table: "tbl_question_sets",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsHiringAssessment",
            table: "tbl_studio_settings",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "HrAntiCheatEnabled",
            table: "tbl_studio_settings",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsOfficialTest",
            table: "tbl_practice_sessions",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsHiringAssessment",
            table: "tbl_question_sets");

        migrationBuilder.DropColumn(
            name: "HrAntiCheatEnabled",
            table: "tbl_question_sets");

        migrationBuilder.DropColumn(
            name: "IsHiringAssessment",
            table: "tbl_studio_settings");

        migrationBuilder.DropColumn(
            name: "HrAntiCheatEnabled",
            table: "tbl_studio_settings");

        migrationBuilder.DropColumn(
            name: "IsOfficialTest",
            table: "tbl_practice_sessions");
    }
}
