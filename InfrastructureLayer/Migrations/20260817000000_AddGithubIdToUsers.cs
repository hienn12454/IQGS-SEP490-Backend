using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using InfrastructureLayer.Database;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260817000000_AddGithubIdToUsers")]
    public partial class AddGithubIdToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GithubId",
                table: "tbl_users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            // Unique trên GithubId nhưng cho phép nhiều row NULL (chưa liên kết GitHub) —
            // giống IX_Users_GoogleId.
            migrationBuilder.CreateIndex(
                name: "IX_Users_GithubId",
                table: "tbl_users",
                column: "GithubId",
                unique: true,
                filter: "\"GithubId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_GithubId",
                table: "tbl_users");

            migrationBuilder.DropColumn(
                name: "GithubId",
                table: "tbl_users");
        }
    }
}
