using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class Add_AiFeedbacks_Table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_feedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateAnswerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: true),
                    StrengthsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ImprovementsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Suggestion = table.Column<string>(type: "text", nullable: true),
                    DimensionScoresJson = table.Column<string>(type: "jsonb", nullable: true),
                    EvaluationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_feedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_feedbacks_candidate_answers_CandidateAnswerId",
                        column: x => x.CandidateAnswerId,
                        principalTable: "candidate_answers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_feedbacks_CandidateAnswerId",
                table: "ai_feedbacks",
                column: "CandidateAnswerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_feedbacks");
        }
    }
}
