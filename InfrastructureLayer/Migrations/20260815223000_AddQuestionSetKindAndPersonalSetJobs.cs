using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using InfrastructureLayer.Database;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260815223000_AddQuestionSetKindAndPersonalSetJobs")]
    public partial class AddQuestionSetKindAndPersonalSetJobs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "tbl_question_sets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Marketplace");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_question_sets_Kind_Status_IsActive",
                table: "tbl_question_sets",
                columns: new[] { "Kind", "Status", "IsActive" });

            migrationBuilder.CreateTable(
                name: "tbl_candidate_personal_set_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JobDescription = table.Column<string>(type: "text", nullable: false),
                    CvSkillsJson = table.Column<string>(type: "jsonb", nullable: false),
                    GapSkillsJson = table.Column<string>(type: "jsonb", nullable: false),
                    PlanJson = table.Column<string>(type: "jsonb", nullable: true),
                    QuestionSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_candidate_personal_set_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_candidate_personal_set_jobs_tbl_question_sets_QuestionSetId",
                        column: x => x.QuestionSetId,
                        principalTable: "tbl_question_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_candidate_personal_set_jobs_CandidateUserId",
                table: "tbl_candidate_personal_set_jobs",
                column: "CandidateUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_candidate_personal_set_jobs_Status",
                table: "tbl_candidate_personal_set_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_candidate_personal_set_jobs_QuestionSetId",
                table: "tbl_candidate_personal_set_jobs",
                column: "QuestionSetId");

            // UpdateData cần entity map lúc generate SQL — dùng SQL thuần để tránh lỗi tbl_subscription_plans.
            migrationBuilder.Sql("""
                UPDATE tbl_subscription_plans
                SET "LimitsJson" = '{"generateCooldownHours":0,"generateUnlimited":false,"planRegeneratePerDraft":0,"canExport":false,"askAiPerMonth":0,"canPublish":false,"freeVisiblePercent":100,"canPersistHrRecommendation":false,"feedbackOnlyOnVisible":false,"canDetailedAiFeedback":false,"freeTeaserFeedbackCount":1,"canGeneratePersonalSet":false,"personalSetPerMonth":0}'
                WHERE "Id" = '11111111-1111-1111-1111-111111111103';

                UPDATE tbl_subscription_plans
                SET "LimitsJson" = '{"generateCooldownHours":0,"generateUnlimited":false,"planRegeneratePerDraft":0,"canExport":false,"askAiPerMonth":0,"canPublish":false,"freeVisiblePercent":100,"canPersistHrRecommendation":true,"feedbackOnlyOnVisible":false,"canDetailedAiFeedback":true,"freeTeaserFeedbackCount":0,"canGeneratePersonalSet":true,"personalSetPerMonth":10}'
                WHERE "Id" = '11111111-1111-1111-1111-111111111104';
                """);

            migrationBuilder.Sql("""
                UPDATE tbl_subscriptions AS s
                SET "LimitsSnapshotJson" = p."LimitsJson"
                FROM tbl_subscription_plans AS p
                WHERE s."PlanId" = p."Id"
                  AND p."Code" IN ('CANDIDATE_FREE', 'CANDIDATE_PREMIUM')
                  AND s."Status" = 'Active';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "tbl_candidate_personal_set_jobs");
            migrationBuilder.DropIndex(
                name: "IX_tbl_question_sets_Kind_Status_IsActive",
                table: "tbl_question_sets");
            migrationBuilder.DropColumn(name: "Kind", table: "tbl_question_sets");
        }
    }
}
