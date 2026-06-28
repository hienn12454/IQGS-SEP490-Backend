using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddCvFieldsToCandidateProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CvBlobPath",
                table: "CandidateProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvContentType",
                table: "CandidateProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvFileName",
                table: "CandidateProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CvUploadedAt",
                table: "CandidateProfiles",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CvBlobPath",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "CvContentType",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "CvFileName",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "CvUploadedAt",
                table: "CandidateProfiles");
        }
    }
}
