using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddHrRecommendationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoRecommendEnabled",
                table: "tbl_question_sets",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<double>(
                name: "RecommendationMinScore",
                table: "tbl_question_sets",
                type: "double precision",
                nullable: false,
                defaultValue: 70.0);

            migrationBuilder.AddColumn<double>(
                name: "RecDefaultMinScore",
                table: "tbl_hr_profiles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecDefaultSortBy",
                table: "tbl_hr_profiles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "score");

            migrationBuilder.AddColumn<string>(
                name: "RecDefaultSortDir",
                table: "tbl_hr_profiles",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "desc");

            migrationBuilder.AddColumn<bool>(
                name: "RecHideDismissed",
                table: "tbl_hr_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoRecommendEnabled",
                table: "tbl_question_sets");

            migrationBuilder.DropColumn(
                name: "RecommendationMinScore",
                table: "tbl_question_sets");

            migrationBuilder.DropColumn(
                name: "RecDefaultMinScore",
                table: "tbl_hr_profiles");

            migrationBuilder.DropColumn(
                name: "RecDefaultSortBy",
                table: "tbl_hr_profiles");

            migrationBuilder.DropColumn(
                name: "RecDefaultSortDir",
                table: "tbl_hr_profiles");

            migrationBuilder.DropColumn(
                name: "RecHideDismissed",
                table: "tbl_hr_profiles");
        }
    }
}
