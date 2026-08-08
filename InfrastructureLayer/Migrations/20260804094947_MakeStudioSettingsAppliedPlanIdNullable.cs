using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class MakeStudioSettingsAppliedPlanIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_settings_tbl_studio_interview_plans_AppliedPlanId",
                table: "tbl_studio_settings");

            migrationBuilder.AlterColumn<Guid>(
                name: "AppliedPlanId",
                table: "tbl_studio_settings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // Empty Guid / orphan → NULL (settings tạo trước khi có plan)
            migrationBuilder.Sql("""
                UPDATE tbl_studio_settings
                SET "AppliedPlanId" = NULL
                WHERE "AppliedPlanId" = '00000000-0000-0000-0000-000000000000'
                   OR (
                        "AppliedPlanId" IS NOT NULL
                        AND NOT EXISTS (
                            SELECT 1 FROM tbl_studio_interview_plans p
                            WHERE p."Id" = tbl_studio_settings."AppliedPlanId"
                        )
                      );
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_settings_tbl_studio_interview_plans_AppliedPlanId",
                table: "tbl_studio_settings",
                column: "AppliedPlanId",
                principalTable: "tbl_studio_interview_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_settings_tbl_studio_interview_plans_AppliedPlanId",
                table: "tbl_studio_settings");

            migrationBuilder.AlterColumn<Guid>(
                name: "AppliedPlanId",
                table: "tbl_studio_settings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_settings_tbl_studio_interview_plans_AppliedPlanId",
                table: "tbl_studio_settings",
                column: "AppliedPlanId",
                principalTable: "tbl_studio_interview_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
