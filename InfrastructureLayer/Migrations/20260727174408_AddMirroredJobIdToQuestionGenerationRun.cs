using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddMirroredJobIdToQuestionGenerationRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MirroredJobId",
                table: "studio_question_generation_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_studio_question_generation_runs_MirroredJobId",
                table: "studio_question_generation_runs",
                column: "MirroredJobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_studio_question_generation_runs_MirroredJobId",
                table: "studio_question_generation_runs");

            migrationBuilder.DropColumn(
                name: "MirroredJobId",
                table: "studio_question_generation_runs");
        }
    }
}
