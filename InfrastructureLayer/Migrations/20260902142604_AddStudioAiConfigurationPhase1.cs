using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddStudioAiConfigurationPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 1 Studio AI config only — candidate tables/columns đã có trên Azure DB.
            migrationBuilder.AddColumn<DateTime>(
                name: "AiRecommendationGeneratedAt",
                table: "tbl_studio_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiRecommendationJson",
                table: "tbl_studio_settings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuestionDistributionJson",
                table: "tbl_studio_settings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuestionStylesJson",
                table: "tbl_studio_settings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedInformationJson",
                table: "tbl_studio_job_descriptions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "tbl_studio_focus_areas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReason",
                table: "tbl_studio_focus_areas",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiRecommendationGeneratedAt",
                table: "tbl_studio_settings");

            migrationBuilder.DropColumn(
                name: "AiRecommendationJson",
                table: "tbl_studio_settings");

            migrationBuilder.DropColumn(
                name: "QuestionDistributionJson",
                table: "tbl_studio_settings");

            migrationBuilder.DropColumn(
                name: "QuestionStylesJson",
                table: "tbl_studio_settings");

            migrationBuilder.DropColumn(
                name: "ExtractedInformationJson",
                table: "tbl_studio_job_descriptions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "tbl_studio_focus_areas");

            migrationBuilder.DropColumn(
                name: "SourceReason",
                table: "tbl_studio_focus_areas");
        }
    }
}
