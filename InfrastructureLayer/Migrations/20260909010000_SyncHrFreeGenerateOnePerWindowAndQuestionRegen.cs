using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// SCRUM-445: HR Free = 1 lần hoàn thành tạo bộ (sinh câu / JD-fit) / 24h + regen 2 lần / plan.
/// Data-only — cập nhật Plan.LimitsJson và snapshot Free đang Active.
/// </summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260909010000_SyncHrFreeGenerateOnePerWindowAndQuestionRegen")]
public partial class SyncHrFreeGenerateOnePerWindowAndQuestionRegen : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "tbl_subscription_plans"
            SET "LimitsJson" = '{"generateCooldownHours":24,"generatePerWindow":1,"generateUnlimited":false,"planRegeneratePerDraft":5,"questionRegenPerPlan":2,"canExport":false,"askAiPerMonth":0,"canPublish":true,"freeVisiblePercent":100,"canPersistHrRecommendation":false,"feedbackOnlyOnVisible":false,"canDetailedAiFeedback":true,"freeTeaserFeedbackCount":0}'
            WHERE "Code" = 'HR_FREE';

            UPDATE "tbl_subscription_plans"
            SET "LimitsJson" = jsonb_set(
                COALESCE("LimitsJson"::jsonb, '{}'::jsonb),
                '{questionRegenPerPlan}',
                '0'::jsonb,
                true)
            WHERE "Code" = 'HR_PREMIUM';

            UPDATE "tbl_subscriptions" s
            SET "LimitsSnapshotJson" = '{"generateCooldownHours":24,"generatePerWindow":1,"generateUnlimited":false,"planRegeneratePerDraft":5,"questionRegenPerPlan":2,"canExport":false,"askAiPerMonth":0,"canPublish":true,"freeVisiblePercent":100,"canPersistHrRecommendation":false,"feedbackOnlyOnVisible":false,"canDetailedAiFeedback":true,"freeTeaserFeedbackCount":0}'
            FROM "tbl_subscription_plans" p
            WHERE s."PlanId" = p."Id"
              AND p."Code" = 'HR_FREE'
              AND s."Status" = 'Active';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "tbl_subscription_plans"
            SET "LimitsJson" = '{"generateCooldownHours":24,"generatePerWindow":4,"generateUnlimited":false,"planRegeneratePerDraft":5,"canExport":false,"askAiPerMonth":0,"canPublish":true,"freeVisiblePercent":100,"canPersistHrRecommendation":false,"feedbackOnlyOnVisible":false,"canDetailedAiFeedback":true,"freeTeaserFeedbackCount":0}'
            WHERE "Code" = 'HR_FREE';

            UPDATE "tbl_subscriptions" s
            SET "LimitsSnapshotJson" = '{"generateCooldownHours":24,"generatePerWindow":4,"generateUnlimited":false,"planRegeneratePerDraft":5,"canExport":false,"askAiPerMonth":0,"canPublish":true,"freeVisiblePercent":100,"canPersistHrRecommendation":false,"feedbackOnlyOnVisible":false,"canDetailedAiFeedback":true,"freeTeaserFeedbackCount":0}'
            FROM "tbl_subscription_plans" p
            WHERE s."PlanId" = p."Id"
              AND p."Code" = 'HR_FREE'
              AND s."Status" = 'Active';
            """);
    }
}
