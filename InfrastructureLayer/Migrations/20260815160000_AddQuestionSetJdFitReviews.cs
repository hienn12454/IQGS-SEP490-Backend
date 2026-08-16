using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using InfrastructureLayer.Database;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260815160000_AddQuestionSetJdFitReviews")]
    public partial class AddQuestionSetJdFitReviews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_question_set_jd_fit_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewJson = table.Column<string>(type: "jsonb", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_question_set_jd_fit_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_question_set_jd_fit_reviews_tbl_question_sets_QuestionSetId",
                        column: x => x.QuestionSetId,
                        principalTable: "tbl_question_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_question_set_jd_fit_reviews_QuestionSetId",
                table: "tbl_question_set_jd_fit_reviews",
                column: "QuestionSetId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "tbl_question_set_jd_fit_reviews");
        }
    }
}
