using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>SCRUM-494: GroupName cho Role Family catalog (optgroup dropdown).</summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260930100000_AddGroupNameToCompetencyRoleFamily")]
public partial class AddGroupNameToCompetencyRoleFamily : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "GroupName",
            table: "tbl_competency_role_families",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "GroupName",
            table: "tbl_competency_role_families");
    }
}
