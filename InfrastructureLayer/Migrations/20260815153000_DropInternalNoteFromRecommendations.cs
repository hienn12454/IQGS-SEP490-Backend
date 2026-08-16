using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using InfrastructureLayer.Database;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260815153000_DropInternalNoteFromRecommendations")]
    public partial class DropInternalNoteFromRecommendations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InternalNote",
                table: "tbl_candidate_recommendations");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InternalNote",
                table: "tbl_candidate_recommendations",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }
    }
}
