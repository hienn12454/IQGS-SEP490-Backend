using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateEngagementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "practice_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OverallScore = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_practice_sessions_question_sets_QuestionSetId",
                        column: x => x.QuestionSetId,
                        principalTable: "question_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "question_set_bookmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_set_bookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_set_bookmarks_question_sets_QuestionSetId",
                        column: x => x.QuestionSetId,
                        principalTable: "question_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_answers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PracticeSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionSetQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnswerText = table.Column<string>(type: "text", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_answers_practice_sessions_PracticeSessionId",
                        column: x => x.PracticeSessionId,
                        principalTable: "practice_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_candidate_answers_question_set_questions_QuestionSetQuestio~",
                        column: x => x.QuestionSetQuestionId,
                        principalTable: "question_set_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_answers_PracticeSessionId_QuestionSetQuestionId",
                table: "candidate_answers",
                columns: new[] { "PracticeSessionId", "QuestionSetQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_answers_QuestionSetQuestionId",
                table: "candidate_answers",
                column: "QuestionSetQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_practice_sessions_CandidateUserId",
                table: "practice_sessions",
                column: "CandidateUserId");

            migrationBuilder.CreateIndex(
                name: "IX_practice_sessions_QuestionSetId",
                table: "practice_sessions",
                column: "QuestionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_question_set_bookmarks_CandidateUserId_QuestionSetId",
                table: "question_set_bookmarks",
                columns: new[] { "CandidateUserId", "QuestionSetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_question_set_bookmarks_QuestionSetId",
                table: "question_set_bookmarks",
                column: "QuestionSetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_answers");

            migrationBuilder.DropTable(
                name: "question_set_bookmarks");

            migrationBuilder.DropTable(
                name: "practice_sessions");
        }
    }
}
