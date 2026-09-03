using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using InfrastructureLayer.Database;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <summary>SCRUM-429: regen nền — callback biết câu cần update.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260906001500_AddTargetQuestionIdToQuestionGenerationRun")]
    public partial class AddTargetQuestionIdToQuestionGenerationRun : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TargetQuestionId",
                table: "tbl_studio_question_generation_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_question_generation_runs_TargetQuestionId",
                table: "tbl_studio_question_generation_runs",
                column: "TargetQuestionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_question_generation_runs_TargetQuestionId",
                table: "tbl_studio_question_generation_runs");

            migrationBuilder.DropColumn(
                name: "TargetQuestionId",
                table: "tbl_studio_question_generation_runs");
        }
    }
}
