using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>SCRUM-468: metadata tin tuyển trên bộ câu hỏi (location, salary, expertise, domain).</summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260921060000_AddHiringPostingFieldsToQuestionSet")]
public partial class AddHiringPostingFieldsToQuestionSet : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "JobLocation",
            table: "tbl_question_sets",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "WorkplaceType",
            table: "tbl_question_sets",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "SalaryMin",
            table: "tbl_question_sets",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "SalaryMax",
            table: "tbl_question_sets",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "SalaryNegotiable",
            table: "tbl_question_sets",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "JobExpertise",
            table: "tbl_question_sets",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "JobDomain",
            table: "tbl_question_sets",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "JobLocation", table: "tbl_question_sets");
        migrationBuilder.DropColumn(name: "WorkplaceType", table: "tbl_question_sets");
        migrationBuilder.DropColumn(name: "SalaryMin", table: "tbl_question_sets");
        migrationBuilder.DropColumn(name: "SalaryMax", table: "tbl_question_sets");
        migrationBuilder.DropColumn(name: "SalaryNegotiable", table: "tbl_question_sets");
        migrationBuilder.DropColumn(name: "JobExpertise", table: "tbl_question_sets");
        migrationBuilder.DropColumn(name: "JobDomain", table: "tbl_question_sets");
    }
}
