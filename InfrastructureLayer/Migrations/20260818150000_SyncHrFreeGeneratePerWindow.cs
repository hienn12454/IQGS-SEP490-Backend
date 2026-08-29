using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using InfrastructureLayer.Database;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <summary>
    /// HR Free: 4 lượt generate / cửa sổ 24h (generatePerWindow).
    /// Data-only — không đổi schema. Xóa LastSuccessfulGenerateAt của HR_FREE
    /// để user đang cooldown 1/24h được 4 lượt ngay (FE khóa theo timestamp).
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260818150000_SyncHrFreeGeneratePerWindow")]
    public partial class SyncHrFreeGeneratePerWindow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE tbl_subscription_plans
                SET "LimitsJson" = '{"generateCooldownHours":24,"generatePerWindow":4,"generateUnlimited":false,"planRegeneratePerDraft":5,"canExport":false,"askAiPerMonth":0,"canPublish":true,"freeVisiblePercent":100,"canPersistHrRecommendation":false,"feedbackOnlyOnVisible":false,"canDetailedAiFeedback":true,"freeTeaserFeedbackCount":0}'
                WHERE "Id" = '11111111-1111-1111-1111-111111111101';
                """);

            migrationBuilder.Sql("""
                UPDATE tbl_subscriptions AS s
                SET "LimitsSnapshotJson" = p."LimitsJson",
                    "LastSuccessfulGenerateAt" = NULL
                FROM tbl_subscription_plans AS p
                WHERE s."PlanId" = p."Id"
                  AND p."Code" = 'HR_FREE'
                  AND s."Status" = 'Active';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only — không rollback snapshot / timestamp user.
        }
    }
}
