using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionAiChatMessageTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "question_ai_chat_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    SuggestionJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_ai_chat_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_ai_chat_messages_generated_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "generated_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_question_ai_chat_messages_question_generation_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "question_generation_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_question_ai_chat_messages_JobId_QuestionId_CreatedAt",
                table: "question_ai_chat_messages",
                columns: new[] { "JobId", "QuestionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_question_ai_chat_messages_QuestionId",
                table: "question_ai_chat_messages",
                column: "QuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "question_ai_chat_messages");
        }
    }
}
