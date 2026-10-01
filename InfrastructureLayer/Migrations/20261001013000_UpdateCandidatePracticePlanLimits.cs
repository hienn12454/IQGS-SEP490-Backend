using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>SCRUM-498: Candidate PracticePerMonth / MaxSavedSessions / FullAiFeedbackPerMonth + sync Active.</summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20261001013000_UpdateCandidatePracticePlanLimits")]
public partial class UpdateCandidatePracticePlanLimits : Migration
{
    private const string CandidateFreeLimits =
        "{\"generateCooldownHours\":0,\"generateUnlimited\":false,\"planRegeneratePerDraft\":0,\"canExport\":false,\"askAiPerMonth\":0,\"canPublish\":false,\"freeVisiblePercent\":100,\"canPersistHrRecommendation\":false,\"feedbackOnlyOnVisible\":false,\"canDetailedAiFeedback\":false,\"freeTeaserFeedbackCount\":0,\"canGeneratePersonalSet\":false,\"personalSetPerMonth\":0,\"practicePerMonth\":5,\"maxSavedSessions\":10,\"fullAiFeedbackPerMonth\":1}";

    private const string CandidatePremiumLimits =
        "{\"generateCooldownHours\":0,\"generateUnlimited\":false,\"planRegeneratePerDraft\":0,\"canExport\":false,\"askAiPerMonth\":0,\"canPublish\":false,\"freeVisiblePercent\":100,\"canPersistHrRecommendation\":true,\"feedbackOnlyOnVisible\":false,\"canDetailedAiFeedback\":true,\"freeTeaserFeedbackCount\":0,\"canGeneratePersonalSet\":true,\"personalSetPerMonth\":10,\"practicePerMonth\":0,\"maxSavedSessions\":0,\"fullAiFeedbackPerMonth\":0}";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            UPDATE "tbl_subscription_plans"
            SET "LimitsJson" = '{CandidateFreeLimits}'::jsonb
            WHERE "Code" = 'CANDIDATE_FREE';

            UPDATE "tbl_subscription_plans"
            SET "LimitsJson" = '{CandidatePremiumLimits}'::jsonb
            WHERE "Code" = 'CANDIDATE_PREMIUM';

            UPDATE "tbl_subscriptions" AS s
            SET "LimitsSnapshotJson" = p."LimitsJson"
            FROM "tbl_subscription_plans" AS p
            WHERE s."PlanId" = p."Id"
              AND p."Code" IN ('CANDIDATE_FREE', 'CANDIDATE_PREMIUM')
              AND s."Status" = 'Active';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "tbl_subscription_plans"
            SET "LimitsJson" = '{"generateCooldownHours":0,"generateUnlimited":false,"planRegeneratePerDraft":0,"canExport":false,"askAiPerMonth":0,"canPublish":false,"freeVisiblePercent":100,"canPersistHrRecommendation":false,"feedbackOnlyOnVisible":false,"canDetailedAiFeedback":false,"freeTeaserFeedbackCount":1,"canGeneratePersonalSet":false,"personalSetPerMonth":0}'::jsonb
            WHERE "Code" = 'CANDIDATE_FREE';

            UPDATE "tbl_subscription_plans"
            SET "LimitsJson" = '{"generateCooldownHours":0,"generateUnlimited":false,"planRegeneratePerDraft":0,"canExport":false,"askAiPerMonth":0,"canPublish":false,"freeVisiblePercent":100,"canPersistHrRecommendation":true,"feedbackOnlyOnVisible":false,"canDetailedAiFeedback":true,"freeTeaserFeedbackCount":0,"canGeneratePersonalSet":true,"personalSetPerMonth":10}'::jsonb
            WHERE "Code" = 'CANDIDATE_PREMIUM';
            """);
    }
}
