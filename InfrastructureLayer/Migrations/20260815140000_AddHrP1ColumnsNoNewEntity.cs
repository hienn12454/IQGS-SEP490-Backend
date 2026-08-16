using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using InfrastructureLayer.Database;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260815140000_AddHrP1ColumnsNoNewEntity")]
    public partial class AddHrP1ColumnsNoNewEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ViewedAt",
                table: "tbl_candidate_recommendations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalNote",
                table: "tbl_candidate_recommendations",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAtUtc",
                table: "tbl_candidate_invitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "tbl_candidate_invitations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingMode",
                table: "tbl_candidate_invitations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingLink",
                table: "tbl_candidate_invitations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "tbl_candidate_invitations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteMessageTemplate",
                table: "tbl_hr_profiles",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ViewedAt", table: "tbl_candidate_recommendations");
            migrationBuilder.DropColumn(name: "InternalNote", table: "tbl_candidate_recommendations");
            migrationBuilder.DropColumn(name: "ScheduledAtUtc", table: "tbl_candidate_invitations");
            migrationBuilder.DropColumn(name: "TimeZoneId", table: "tbl_candidate_invitations");
            migrationBuilder.DropColumn(name: "MeetingMode", table: "tbl_candidate_invitations");
            migrationBuilder.DropColumn(name: "MeetingLink", table: "tbl_candidate_invitations");
            migrationBuilder.DropColumn(name: "Location", table: "tbl_candidate_invitations");
            migrationBuilder.DropColumn(name: "InviteMessageTemplate", table: "tbl_hr_profiles");
        }
    }
}
