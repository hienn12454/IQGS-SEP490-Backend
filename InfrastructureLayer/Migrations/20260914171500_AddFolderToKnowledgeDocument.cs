using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>SCRUM-450: cột folder để nhóm Knowledge trên UI Admin (không đổi Blob path).</summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260914171500_AddFolderToKnowledgeDocument")]
public partial class AddFolderToKnowledgeDocument : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "folder",
            table: "tbl_knowledge_documents",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_knowledge_documents_scope_folder",
            table: "tbl_knowledge_documents",
            columns: new[] { "scope", "folder" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_knowledge_documents_scope_folder",
            table: "tbl_knowledge_documents");

        migrationBuilder.DropColumn(
            name: "folder",
            table: "tbl_knowledge_documents");
    }
}
