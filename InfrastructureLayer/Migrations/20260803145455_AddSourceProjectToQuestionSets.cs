using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceProjectToQuestionSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_question_sets_question_generation_jobs_SourceJobId",
                table: "question_sets");

            migrationBuilder.DropIndex(
                name: "IX_question_sets_SourceJobId",
                table: "question_sets");

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceJobId",
                table: "question_sets",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePlanId",
                table: "question_sets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceProjectId",
                table: "question_sets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceRunId",
                table: "question_sets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_question_sets_SourceJobId",
                table: "question_sets",
                column: "SourceJobId",
                unique: true,
                filter: "\"SourceJobId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_question_sets_SourceProjectId",
                table: "question_sets",
                column: "SourceProjectId",
                unique: true,
                filter: "\"SourceProjectId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_question_sets_question_generation_jobs_SourceJobId",
                table: "question_sets",
                column: "SourceJobId",
                principalTable: "question_generation_jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_question_sets_question_generation_jobs_SourceJobId",
                table: "question_sets");

            migrationBuilder.DropIndex(
                name: "IX_question_sets_SourceJobId",
                table: "question_sets");

            migrationBuilder.DropIndex(
                name: "IX_question_sets_SourceProjectId",
                table: "question_sets");

            migrationBuilder.DropColumn(
                name: "SourcePlanId",
                table: "question_sets");

            migrationBuilder.DropColumn(
                name: "SourceProjectId",
                table: "question_sets");

            migrationBuilder.DropColumn(
                name: "SourceRunId",
                table: "question_sets");

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceJobId",
                table: "question_sets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_question_sets_SourceJobId",
                table: "question_sets",
                column: "SourceJobId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_question_sets_question_generation_jobs_SourceJobId",
                table: "question_sets",
                column: "SourceJobId",
                principalTable: "question_generation_jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
