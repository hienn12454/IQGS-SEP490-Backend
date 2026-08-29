using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using InfrastructureLayer.Database;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <summary>
    /// Vá LimitsJson template Candidate + snapshot Active CANDIDATE_PREMIUM
    /// khi thiếu cờ AI Coach (canGeneratePersonalSet / canDetailedAiFeedback).
    /// Data-only — không đổi schema.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260818140000_SyncCandidatePremiumCoachLimits")]
    public partial class SyncCandidatePremiumCoachLimits : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                  AND p."Code" = 'CANDIDATE_PREMIUM'
                  AND s."Status" = 'Active';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only sync — không rollback snapshot user.
        }
    }
}
