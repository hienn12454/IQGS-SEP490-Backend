using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using InfrastructureLayer.Database;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <summary>Lưu nguồn JD (paste vs file) + tên file trên Question Set để History hiển thị.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260905000900_AddJdSourceMetaToQuestionSets")]
    public partial class AddJdSourceMetaToQuestionSets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JdSourceType",
                table: "tbl_question_sets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PastedText");

            migrationBuilder.AddColumn<string>(
                name: "JdOriginalFileName",
                table: "tbl_question_sets",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JdOriginalFileName",
                table: "tbl_question_sets");

            migrationBuilder.DropColumn(
                name: "JdSourceType",
                table: "tbl_question_sets");
        }
    }
}
