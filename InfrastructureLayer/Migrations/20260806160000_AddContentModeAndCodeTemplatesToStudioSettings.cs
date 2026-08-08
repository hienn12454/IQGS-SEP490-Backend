using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    public partial class AddContentModeAndCodeTemplatesToStudioSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodeTemplatesJson",
                table: "tbl_studio_settings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentMode",
                table: "tbl_studio_settings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Mixed");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodeTemplatesJson",
                table: "tbl_studio_settings");

            migrationBuilder.DropColumn(
                name: "ContentMode",
                table: "tbl_studio_settings");
        }
    }
}

