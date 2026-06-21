using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <inheritdoc />
public partial class AddHrNoteAndJdInputToQuestionGenerationJobs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "HrNote",
            table: "question_generation_jobs",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "JdInputType",
            table: "question_generation_jobs",
            type: "character varying(10)",
            maxLength: 10,
            nullable: false,
            defaultValue: "TEXT");

        migrationBuilder.AddColumn<string>(
            name: "JdFileName",
            table: "question_generation_jobs",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "HrNote",
            table: "question_generation_jobs");

        migrationBuilder.DropColumn(
            name: "JdInputType",
            table: "question_generation_jobs");

        migrationBuilder.DropColumn(
            name: "JdFileName",
            table: "question_generation_jobs");
    }
}
