using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>SCRUM-465: JD file blob path + bản PublicJobDescription cho candidate (bộ Tuyển).</summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260921020000_AddJdBlobAndPublicJobDescription")]
public partial class AddJdBlobAndPublicJobDescription : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BlobPath",
            table: "tbl_studio_job_descriptions",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "JdBlobPath",
            table: "tbl_question_sets",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicJobDescription",
            table: "tbl_question_sets",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BlobPath",
            table: "tbl_studio_job_descriptions");

        migrationBuilder.DropColumn(
            name: "JdBlobPath",
            table: "tbl_question_sets");

        migrationBuilder.DropColumn(
            name: "PublicJobDescription",
            table: "tbl_question_sets");
    }
}
