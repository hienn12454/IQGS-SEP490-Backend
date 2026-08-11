using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionSetFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_question_set_feedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_question_set_feedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_question_set_feedbacks_tbl_question_sets_QuestionSetId",
                        column: x => x.QuestionSetId,
                        principalTable: "tbl_question_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_question_set_feedbacks_tbl_users_CandidateUserId",
                        column: x => x.CandidateUserId,
                        principalTable: "tbl_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_question_set_feedbacks_CandidateUserId",
                table: "tbl_question_set_feedbacks",
                column: "CandidateUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_question_set_feedbacks_QuestionSetId_CandidateUserId",
                table: "tbl_question_set_feedbacks",
                columns: new[] { "QuestionSetId", "CandidateUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_question_set_feedbacks");
        }
    }
}
