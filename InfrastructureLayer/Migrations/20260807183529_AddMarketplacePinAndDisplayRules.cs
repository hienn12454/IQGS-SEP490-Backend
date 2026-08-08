using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplacePinAndDisplayRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "tbl_question_sets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedAt",
                table: "tbl_question_sets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxPinnedSets",
                table: "tbl_platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "MinAttemptsForTrending",
                table: "tbl_platform_settings",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.UpdateData(
                table: "tbl_platform_settings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "MaxPinnedSets", "MinAttemptsForTrending" },
                values: new object[] { 5, 10 });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_question_sets_IsPinned_PinnedAt",
                table: "tbl_question_sets",
                columns: new[] { "IsPinned", "PinnedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tbl_question_sets_IsPinned_PinnedAt",
                table: "tbl_question_sets");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "tbl_question_sets");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "tbl_question_sets");

            migrationBuilder.DropColumn(
                name: "MaxPinnedSets",
                table: "tbl_platform_settings");

            migrationBuilder.DropColumn(
                name: "MinAttemptsForTrending",
                table: "tbl_platform_settings");
        }
    }
}
