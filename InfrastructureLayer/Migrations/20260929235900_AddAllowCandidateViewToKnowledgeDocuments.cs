using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>SCRUM-486: Admin bật/tắt quyền Candidate xem file KB gốc.</summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260929235900_AddAllowCandidateViewToKnowledgeDocuments")]
public partial class AddAllowCandidateViewToKnowledgeDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "allow_candidate_view",
            table: "tbl_knowledge_documents",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "allow_candidate_view",
            table: "tbl_knowledge_documents");
    }
}
