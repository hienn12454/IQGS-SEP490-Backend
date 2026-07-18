using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationAndInvitationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowRecruiterRecommendation",
                table: "CandidateProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "candidate_recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    PracticeSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    HrOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverallScore = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_recommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_recommendations_practice_sessions_PracticeSession~",
                        column: x => x.PracticeSessionId,
                        principalTable: "practice_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_candidate_recommendations_question_sets_QuestionSetId",
                        column: x => x.QuestionSetId,
                        principalTable: "question_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    HrUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_candidate_invitations_candidate_recommendations_Recommendat~",
                        column: x => x.RecommendationId,
                        principalTable: "candidate_recommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_invitations_CandidateUserId_Status",
                table: "candidate_invitations",
                columns: new[] { "CandidateUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_invitations_RecommendationId",
                table: "candidate_invitations",
                column: "RecommendationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_recommendations_CandidateUserId_QuestionSetId",
                table: "candidate_recommendations",
                columns: new[] { "CandidateUserId", "QuestionSetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_recommendations_HrOwnerId_Status",
                table: "candidate_recommendations",
                columns: new[] { "HrOwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_recommendations_PracticeSessionId",
                table: "candidate_recommendations",
                column: "PracticeSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_recommendations_QuestionSetId",
                table: "candidate_recommendations",
                column: "QuestionSetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_invitations");

            migrationBuilder.DropTable(
                name: "candidate_recommendations");

            migrationBuilder.DropColumn(
                name: "AllowRecruiterRecommendation",
                table: "CandidateProfiles");
        }
    }
}
