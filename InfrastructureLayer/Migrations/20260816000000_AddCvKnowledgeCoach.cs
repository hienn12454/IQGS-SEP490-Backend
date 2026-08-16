using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using InfrastructureLayer.Database;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260816000000_AddCvKnowledgeCoach")]
    public partial class AddCvKnowledgeCoach : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "tbl_candidate_personal_set_jobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "JdGap");

            migrationBuilder.AddColumn<string>(
                name: "FocusSkillsJson",
                table: "tbl_candidate_personal_set_jobs",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_candidate_personal_set_jobs_Purpose",
                table: "tbl_candidate_personal_set_jobs",
                column: "Purpose");

            migrationBuilder.CreateTable(
                name: "tbl_candidate_skill_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDiagnosticSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_candidate_skill_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_candidate_skill_plans_tbl_question_sets_SourceDiagnosticSetId",
                        column: x => x.SourceDiagnosticSetId,
                        principalTable: "tbl_question_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_candidate_skill_plans_CandidateUserId",
                table: "tbl_candidate_skill_plans",
                column: "CandidateUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_candidate_skill_plans_SourceDiagnosticSetId",
                table: "tbl_candidate_skill_plans",
                column: "SourceDiagnosticSetId");

            migrationBuilder.CreateTable(
                name: "tbl_candidate_skill_plan_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Skill = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BaselineScore = table.Column<double>(type: "double precision", nullable: true),
                    CurrentScore = table.Column<double>(type: "double precision", nullable: true),
                    TargetScore = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_candidate_skill_plan_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_candidate_skill_plan_items_tbl_candidate_skill_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "tbl_candidate_skill_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_candidate_skill_plan_items_tbl_practice_sessions_LastSessionId",
                        column: x => x.LastSessionId,
                        principalTable: "tbl_practice_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_candidate_skill_plan_items_PlanId_Skill",
                table: "tbl_candidate_skill_plan_items",
                columns: new[] { "PlanId", "Skill" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_candidate_skill_plan_items_LastSessionId",
                table: "tbl_candidate_skill_plan_items",
                column: "LastSessionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "tbl_candidate_skill_plan_items");
            migrationBuilder.DropTable(name: "tbl_candidate_skill_plans");
            migrationBuilder.DropIndex(
                name: "IX_tbl_candidate_personal_set_jobs_Purpose",
                table: "tbl_candidate_personal_set_jobs");
            migrationBuilder.DropColumn(name: "Purpose", table: "tbl_candidate_personal_set_jobs");
            migrationBuilder.DropColumn(name: "FocusSkillsJson", table: "tbl_candidate_personal_set_jobs");
        }
    }
}
