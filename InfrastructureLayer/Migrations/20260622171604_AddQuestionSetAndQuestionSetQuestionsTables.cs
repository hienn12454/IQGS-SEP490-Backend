using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionSetAndQuestionSetQuestionsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "question_sets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    JobDescription = table.Column<string>(type: "text", nullable: false),
                    HrNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PlanJson = table.Column<string>(type: "jsonb", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_sets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_sets_question_generation_jobs_SourceJobId",
                        column: x => x.SourceJobId,
                        principalTable: "question_generation_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "question_set_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    QuestionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Skill = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FocusArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Rationale = table.Column<string>(type: "text", nullable: true),
                    SampleAnswer = table.Column<string>(type: "text", nullable: true),
                    EvaluationCriteriaJson = table.Column<string>(type: "jsonb", nullable: false),
                    CitationsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_set_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_set_questions_question_sets_QuestionSetId",
                        column: x => x.QuestionSetId,
                        principalTable: "question_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_question_set_questions_QuestionSetId_Order",
                table: "question_set_questions",
                columns: new[] { "QuestionSetId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_question_sets_OwnerId",
                table: "question_sets",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_question_sets_SourceJobId",
                table: "question_sets",
                column: "SourceJobId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "question_set_questions");

            migrationBuilder.DropTable(
                name: "question_sets");
        }
    }
}
