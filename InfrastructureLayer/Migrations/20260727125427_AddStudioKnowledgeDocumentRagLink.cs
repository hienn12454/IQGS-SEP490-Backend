using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddStudioKnowledgeDocumentRagLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "KnowledgeDocumentId",
                table: "studio_knowledge_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_studio_knowledge_documents_KnowledgeDocumentId",
                table: "studio_knowledge_documents",
                column: "KnowledgeDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_studio_knowledge_documents_ProjectId",
                table: "studio_knowledge_documents",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_studio_knowledge_documents_KnowledgeDocumentId",
                table: "studio_knowledge_documents");

            migrationBuilder.DropIndex(
                name: "IX_studio_knowledge_documents_ProjectId",
                table: "studio_knowledge_documents");

            migrationBuilder.DropColumn(
                name: "KnowledgeDocumentId",
                table: "studio_knowledge_documents");
        }
    }
}
