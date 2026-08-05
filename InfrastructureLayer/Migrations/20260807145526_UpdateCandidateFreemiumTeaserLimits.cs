using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCandidateFreemiumTeaserLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "tbl_subscription_plans",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111101"),
                column: "LimitsJson",
                value: "{\"generateCooldownHours\":24,\"generateUnlimited\":false,\"planRegeneratePerDraft\":5,\"canExport\":false,\"askAiPerMonth\":0,\"canPublish\":true,\"freeVisiblePercent\":100,\"canPersistHrRecommendation\":false,\"feedbackOnlyOnVisible\":false,\"canDetailedAiFeedback\":true,\"freeTeaserFeedbackCount\":0}");

            migrationBuilder.UpdateData(
                table: "tbl_subscription_plans",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111102"),
                column: "LimitsJson",
                value: "{\"generateCooldownHours\":0,\"generateUnlimited\":true,\"planRegeneratePerDraft\":5,\"canExport\":true,\"askAiPerMonth\":1000,\"canPublish\":true,\"freeVisiblePercent\":100,\"canPersistHrRecommendation\":false,\"feedbackOnlyOnVisible\":false,\"canDetailedAiFeedback\":true,\"freeTeaserFeedbackCount\":0}");

            migrationBuilder.UpdateData(
                table: "tbl_subscription_plans",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111103"),
                column: "LimitsJson",
                value: "{\"generateCooldownHours\":0,\"generateUnlimited\":false,\"planRegeneratePerDraft\":0,\"canExport\":false,\"askAiPerMonth\":0,\"canPublish\":false,\"freeVisiblePercent\":100,\"canPersistHrRecommendation\":false,\"feedbackOnlyOnVisible\":false,\"canDetailedAiFeedback\":false,\"freeTeaserFeedbackCount\":1}");

            migrationBuilder.UpdateData(
                table: "tbl_subscription_plans",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111104"),
                column: "LimitsJson",
                value: "{\"generateCooldownHours\":0,\"generateUnlimited\":false,\"planRegeneratePerDraft\":0,\"canExport\":false,\"askAiPerMonth\":0,\"canPublish\":false,\"freeVisiblePercent\":100,\"canPersistHrRecommendation\":true,\"feedbackOnlyOnVisible\":false,\"canDetailedAiFeedback\":true,\"freeTeaserFeedbackCount\":0}");

            // Cập nhật snapshot đang active của Candidate Free / Premium để khớp Teaser Freemium
            migrationBuilder.Sql("""
                UPDATE tbl_subscriptions AS s
                SET "LimitsSnapshotJson" = p."LimitsJson"
                FROM tbl_subscription_plans AS p
                WHERE s."PlanId" = p."Id"
                  AND p."Code" IN ('CANDIDATE_FREE', 'CANDIDATE_PREMIUM')
                  AND s."Status" = 'Active';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "tbl_subscription_plans",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111101"),
                column: "LimitsJson",
                value: "{\"generateCooldownHours\":24,\"generateUnlimited\":false,\"planRegeneratePerDraft\":5,\"canExport\":false,\"askAiPerMonth\":0,\"canPublish\":true,\"freeVisiblePercent\":100,\"canPersistHrRecommendation\":false,\"feedbackOnlyOnVisible\":false}");

            migrationBuilder.UpdateData(
                table: "tbl_subscription_plans",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111102"),
                column: "LimitsJson",
                value: "{\"generateCooldownHours\":0,\"generateUnlimited\":true,\"planRegeneratePerDraft\":5,\"canExport\":true,\"askAiPerMonth\":1000,\"canPublish\":true,\"freeVisiblePercent\":100,\"canPersistHrRecommendation\":false,\"feedbackOnlyOnVisible\":false}");

            migrationBuilder.UpdateData(
                table: "tbl_subscription_plans",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111103"),
                column: "LimitsJson",
                value: "{\"generateCooldownHours\":0,\"generateUnlimited\":false,\"planRegeneratePerDraft\":0,\"canExport\":false,\"askAiPerMonth\":0,\"canPublish\":false,\"freeVisiblePercent\":20,\"canPersistHrRecommendation\":false,\"feedbackOnlyOnVisible\":true}");

            migrationBuilder.UpdateData(
                table: "tbl_subscription_plans",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111104"),
                column: "LimitsJson",
                value: "{\"generateCooldownHours\":0,\"generateUnlimited\":false,\"planRegeneratePerDraft\":0,\"canExport\":false,\"askAiPerMonth\":0,\"canPublish\":false,\"freeVisiblePercent\":100,\"canPersistHrRecommendation\":true,\"feedbackOnlyOnVisible\":false}");
        }
    }
}
