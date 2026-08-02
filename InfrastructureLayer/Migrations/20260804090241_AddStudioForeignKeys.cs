using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddStudioForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dọn orphan an toàn trước ADD FK (không xóa question_sets đã publish).
            migrationBuilder.Sql("""
                -- Product bridge: nullify Source* orphan
                UPDATE tbl_question_sets qs
                SET "SourceProjectId" = NULL
                WHERE qs."SourceProjectId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM tbl_studio_interview_projects p WHERE p."Id" = qs."SourceProjectId");

                UPDATE tbl_question_sets qs
                SET "SourcePlanId" = NULL
                WHERE qs."SourcePlanId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM tbl_studio_interview_plans p WHERE p."Id" = qs."SourcePlanId");

                UPDATE tbl_question_sets qs
                SET "SourceRunId" = NULL
                WHERE qs."SourceRunId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM tbl_studio_question_generation_runs r WHERE r."Id" = qs."SourceRunId");

                -- Optional Studio links
                UPDATE tbl_studio_knowledge_documents d
                SET "KnowledgeDocumentId" = NULL
                WHERE d."KnowledgeDocumentId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM tbl_knowledge_documents k WHERE k.id = d."KnowledgeDocumentId");

                UPDATE tbl_studio_ai_chat_messages m
                SET "RelatedPlanId" = NULL
                WHERE m."RelatedPlanId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM tbl_studio_interview_plans p WHERE p."Id" = m."RelatedPlanId");

                UPDATE tbl_studio_interview_plans pl
                SET "ApprovedBy" = NULL
                WHERE pl."ApprovedBy" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM tbl_users u WHERE u."Id" = pl."ApprovedBy");

                -- Re-attach plan.session to any session of same project if session orphan
                UPDATE tbl_studio_interview_plans pl
                SET "SessionId" = (
                    SELECT s."Id" FROM tbl_studio_ai_chat_sessions s
                    WHERE s."ProjectId" = pl."ProjectId"
                    ORDER BY s."CreatedAt"
                    LIMIT 1
                )
                WHERE NOT EXISTS (
                    SELECT 1 FROM tbl_studio_ai_chat_sessions s WHERE s."Id" = pl."SessionId"
                )
                AND EXISTS (
                    SELECT 1 FROM tbl_studio_ai_chat_sessions s WHERE s."ProjectId" = pl."ProjectId"
                );

                -- Tạo session stub cho plan còn orphan SessionId (ProjectId + OwnerId hợp lệ)
                INSERT INTO tbl_studio_ai_chat_sessions (
                    "Id", "ProjectId", "UserId", "SelectionMode", "SelectedModelDisplayName",
                    "IsActive", "CreatedAt", "UpdatedAt"
                )
                SELECT gen_random_uuid(),
                       pl."ProjectId",
                       p."OwnerId",
                       'Auto',
                       'Migration FK repair',
                       TRUE,
                       NOW() AT TIME ZONE 'utc',
                       NULL
                FROM tbl_studio_interview_plans pl
                INNER JOIN tbl_studio_interview_projects p ON p."Id" = pl."ProjectId"
                WHERE NOT EXISTS (
                    SELECT 1 FROM tbl_studio_ai_chat_sessions s WHERE s."Id" = pl."SessionId"
                )
                AND NOT EXISTS (
                    SELECT 1 FROM tbl_studio_ai_chat_sessions s WHERE s."ProjectId" = pl."ProjectId"
                )
                GROUP BY pl."ProjectId", p."OwnerId";

                -- Gán lại SessionId sau khi tạo stub (một session / project)
                UPDATE tbl_studio_interview_plans pl
                SET "SessionId" = (
                    SELECT s."Id" FROM tbl_studio_ai_chat_sessions s
                    WHERE s."ProjectId" = pl."ProjectId"
                    ORDER BY s."CreatedAt"
                    LIMIT 1
                )
                WHERE NOT EXISTS (
                    SELECT 1 FROM tbl_studio_ai_chat_sessions s WHERE s."Id" = pl."SessionId"
                );

                -- Plan không còn project hợp lệ → xóa con rồi xóa plan
                DELETE FROM tbl_studio_plan_approval_histories h
                WHERE NOT EXISTS (SELECT 1 FROM tbl_studio_interview_plans p WHERE p."Id" = h."InterviewPlanId")
                   OR NOT EXISTS (SELECT 1 FROM tbl_studio_interview_projects p WHERE p."Id" = h."ProjectId");

                DELETE FROM tbl_studio_interview_plans pl
                WHERE NOT EXISTS (SELECT 1 FROM tbl_studio_ai_chat_sessions s WHERE s."Id" = pl."SessionId")
                   OR NOT EXISTS (SELECT 1 FROM tbl_studio_interview_projects p WHERE p."Id" = pl."ProjectId");

                -- Remove pure-orphan children (no valid parent)
                DELETE FROM tbl_studio_ai_chat_messages m
                WHERE NOT EXISTS (SELECT 1 FROM tbl_studio_ai_chat_sessions s WHERE s."Id" = m."SessionId");

                DELETE FROM tbl_studio_plan_sections s
                WHERE NOT EXISTS (SELECT 1 FROM tbl_studio_interview_plans p WHERE p."Id" = s."InterviewPlanId");

                DELETE FROM tbl_studio_plan_focus_areas f
                WHERE NOT EXISTS (SELECT 1 FROM tbl_studio_interview_plans p WHERE p."Id" = f."InterviewPlanId");

                DELETE FROM tbl_studio_focus_areas f
                WHERE NOT EXISTS (SELECT 1 FROM tbl_studio_settings s WHERE s."Id" = f."StudioSettingsId");

                DELETE FROM tbl_studio_interview_questions q
                WHERE NOT EXISTS (SELECT 1 FROM tbl_studio_interview_projects p WHERE p."Id" = q."ProjectId")
                   OR NOT EXISTS (SELECT 1 FROM tbl_studio_interview_plans p WHERE p."Id" = q."InterviewPlanId")
                   OR NOT EXISTS (SELECT 1 FROM tbl_studio_plan_sections s WHERE s."Id" = q."PlanSectionId")
                   OR NOT EXISTS (SELECT 1 FROM tbl_studio_question_generation_runs r WHERE r."Id" = q."GenerationRunId");

                -- Settings AppliedPlan orphan → gắn plan revision cao nhất cùng project
                UPDATE tbl_studio_settings s
                SET "AppliedPlanId" = (
                    SELECT pl."Id" FROM tbl_studio_interview_plans pl
                    WHERE pl."ProjectId" = s."ProjectId"
                    ORDER BY pl."Revision" DESC
                    LIMIT 1
                )
                WHERE NOT EXISTS (
                    SELECT 1 FROM tbl_studio_interview_plans p WHERE p."Id" = s."AppliedPlanId"
                )
                AND EXISTS (
                    SELECT 1 FROM tbl_studio_interview_plans pl WHERE pl."ProjectId" = s."ProjectId"
                );

                DELETE FROM tbl_studio_focus_areas f
                WHERE f."StudioSettingsId" IN (
                    SELECT s."Id" FROM tbl_studio_settings s
                    WHERE NOT EXISTS (SELECT 1 FROM tbl_studio_interview_plans p WHERE p."Id" = s."AppliedPlanId")
                       OR NOT EXISTS (SELECT 1 FROM tbl_studio_interview_projects p WHERE p."Id" = s."ProjectId")
                );

                DELETE FROM tbl_studio_settings s
                WHERE NOT EXISTS (SELECT 1 FROM tbl_studio_interview_plans p WHERE p."Id" = s."AppliedPlanId")
                   OR NOT EXISTS (SELECT 1 FROM tbl_studio_interview_projects p WHERE p."Id" = s."ProjectId");

                -- Runs without plan/project
                DELETE FROM tbl_studio_question_generation_runs r
                WHERE NOT EXISTS (SELECT 1 FROM tbl_studio_interview_plans p WHERE p."Id" = r."InterviewPlanId")
                   OR NOT EXISTS (SELECT 1 FROM tbl_studio_interview_projects p WHERE p."Id" = r."ProjectId")
                   OR NOT EXISTS (SELECT 1 FROM tbl_users u WHERE u."Id" = r."RequestedBy");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_settings_AppliedPlanId",
                table: "tbl_studio_settings",
                column: "AppliedPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_question_generation_runs_InterviewPlanId",
                table: "tbl_studio_question_generation_runs",
                column: "InterviewPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_question_generation_runs_ProjectId",
                table: "tbl_studio_question_generation_runs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_question_generation_runs_RequestedBy",
                table: "tbl_studio_question_generation_runs",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_project_shares_CreatedBy",
                table: "tbl_studio_project_shares",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_project_shares_ProjectId",
                table: "tbl_studio_project_shares",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_plan_sections_InterviewPlanId",
                table: "tbl_studio_plan_sections",
                column: "InterviewPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_plan_focus_areas_InterviewPlanId",
                table: "tbl_studio_plan_focus_areas",
                column: "InterviewPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_plan_approval_histories_ActorId",
                table: "tbl_studio_plan_approval_histories",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_plan_approval_histories_InterviewPlanId",
                table: "tbl_studio_plan_approval_histories",
                column: "InterviewPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_plan_approval_histories_ProjectId",
                table: "tbl_studio_plan_approval_histories",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_interview_questions_GenerationRunId",
                table: "tbl_studio_interview_questions",
                column: "GenerationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_interview_questions_InterviewPlanId",
                table: "tbl_studio_interview_questions",
                column: "InterviewPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_interview_questions_PlanSectionId",
                table: "tbl_studio_interview_questions",
                column: "PlanSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_interview_questions_ProjectId",
                table: "tbl_studio_interview_questions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_interview_questions_ProjectId_OrderIndex",
                table: "tbl_studio_interview_questions",
                columns: new[] { "ProjectId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_interview_projects_OwnerId",
                table: "tbl_studio_interview_projects",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_interview_plans_ApprovedBy",
                table: "tbl_studio_interview_plans",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_interview_plans_SessionId",
                table: "tbl_studio_interview_plans",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_focus_areas_StudioSettingsId",
                table: "tbl_studio_focus_areas",
                column: "StudioSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_ai_chat_sessions_ProjectId",
                table: "tbl_studio_ai_chat_sessions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_ai_chat_sessions_UserId",
                table: "tbl_studio_ai_chat_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_ai_chat_messages_RelatedPlanId",
                table: "tbl_studio_ai_chat_messages",
                column: "RelatedPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_studio_ai_chat_messages_SessionId",
                table: "tbl_studio_ai_chat_messages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_question_sets_SourcePlanId",
                table: "tbl_question_sets",
                column: "SourcePlanId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_question_sets_SourceRunId",
                table: "tbl_question_sets",
                column: "SourceRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_question_sets_tbl_studio_interview_plans_SourcePlanId",
                table: "tbl_question_sets",
                column: "SourcePlanId",
                principalTable: "tbl_studio_interview_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_question_sets_tbl_studio_interview_projects_SourceProje~",
                table: "tbl_question_sets",
                column: "SourceProjectId",
                principalTable: "tbl_studio_interview_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_question_sets_tbl_studio_question_generation_runs_Sourc~",
                table: "tbl_question_sets",
                column: "SourceRunId",
                principalTable: "tbl_studio_question_generation_runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_ai_chat_messages_tbl_studio_ai_chat_sessions_Ses~",
                table: "tbl_studio_ai_chat_messages",
                column: "SessionId",
                principalTable: "tbl_studio_ai_chat_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_ai_chat_messages_tbl_studio_interview_plans_Rela~",
                table: "tbl_studio_ai_chat_messages",
                column: "RelatedPlanId",
                principalTable: "tbl_studio_interview_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_ai_chat_sessions_tbl_studio_interview_projects_P~",
                table: "tbl_studio_ai_chat_sessions",
                column: "ProjectId",
                principalTable: "tbl_studio_interview_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_ai_chat_sessions_tbl_users_UserId",
                table: "tbl_studio_ai_chat_sessions",
                column: "UserId",
                principalTable: "tbl_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_focus_areas_tbl_studio_settings_StudioSettingsId",
                table: "tbl_studio_focus_areas",
                column: "StudioSettingsId",
                principalTable: "tbl_studio_settings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_interview_plans_tbl_studio_ai_chat_sessions_Sess~",
                table: "tbl_studio_interview_plans",
                column: "SessionId",
                principalTable: "tbl_studio_ai_chat_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_interview_plans_tbl_studio_interview_projects_Pr~",
                table: "tbl_studio_interview_plans",
                column: "ProjectId",
                principalTable: "tbl_studio_interview_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_interview_plans_tbl_users_ApprovedBy",
                table: "tbl_studio_interview_plans",
                column: "ApprovedBy",
                principalTable: "tbl_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_interview_projects_tbl_users_OwnerId",
                table: "tbl_studio_interview_projects",
                column: "OwnerId",
                principalTable: "tbl_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_interview_questions_tbl_studio_interview_plans_I~",
                table: "tbl_studio_interview_questions",
                column: "InterviewPlanId",
                principalTable: "tbl_studio_interview_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_interview_questions_tbl_studio_interview_project~",
                table: "tbl_studio_interview_questions",
                column: "ProjectId",
                principalTable: "tbl_studio_interview_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_interview_questions_tbl_studio_plan_sections_Pla~",
                table: "tbl_studio_interview_questions",
                column: "PlanSectionId",
                principalTable: "tbl_studio_plan_sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_interview_questions_tbl_studio_question_generati~",
                table: "tbl_studio_interview_questions",
                column: "GenerationRunId",
                principalTable: "tbl_studio_question_generation_runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_job_descriptions_tbl_studio_interview_projects_P~",
                table: "tbl_studio_job_descriptions",
                column: "ProjectId",
                principalTable: "tbl_studio_interview_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_knowledge_documents_tbl_knowledge_documents_Know~",
                table: "tbl_studio_knowledge_documents",
                column: "KnowledgeDocumentId",
                principalTable: "tbl_knowledge_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_knowledge_documents_tbl_studio_interview_project~",
                table: "tbl_studio_knowledge_documents",
                column: "ProjectId",
                principalTable: "tbl_studio_interview_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_plan_approval_histories_tbl_studio_interview_pla~",
                table: "tbl_studio_plan_approval_histories",
                column: "InterviewPlanId",
                principalTable: "tbl_studio_interview_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_plan_approval_histories_tbl_studio_interview_pro~",
                table: "tbl_studio_plan_approval_histories",
                column: "ProjectId",
                principalTable: "tbl_studio_interview_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_plan_approval_histories_tbl_users_ActorId",
                table: "tbl_studio_plan_approval_histories",
                column: "ActorId",
                principalTable: "tbl_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_plan_focus_areas_tbl_studio_interview_plans_Inte~",
                table: "tbl_studio_plan_focus_areas",
                column: "InterviewPlanId",
                principalTable: "tbl_studio_interview_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_plan_sections_tbl_studio_interview_plans_Intervi~",
                table: "tbl_studio_plan_sections",
                column: "InterviewPlanId",
                principalTable: "tbl_studio_interview_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_project_shares_tbl_studio_interview_projects_Pro~",
                table: "tbl_studio_project_shares",
                column: "ProjectId",
                principalTable: "tbl_studio_interview_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_project_shares_tbl_users_CreatedBy",
                table: "tbl_studio_project_shares",
                column: "CreatedBy",
                principalTable: "tbl_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_question_generation_runs_tbl_studio_interview_pl~",
                table: "tbl_studio_question_generation_runs",
                column: "InterviewPlanId",
                principalTable: "tbl_studio_interview_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_question_generation_runs_tbl_studio_interview_pr~",
                table: "tbl_studio_question_generation_runs",
                column: "ProjectId",
                principalTable: "tbl_studio_interview_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_question_generation_runs_tbl_users_RequestedBy",
                table: "tbl_studio_question_generation_runs",
                column: "RequestedBy",
                principalTable: "tbl_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_settings_tbl_studio_interview_plans_AppliedPlanId",
                table: "tbl_studio_settings",
                column: "AppliedPlanId",
                principalTable: "tbl_studio_interview_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_studio_settings_tbl_studio_interview_projects_ProjectId",
                table: "tbl_studio_settings",
                column: "ProjectId",
                principalTable: "tbl_studio_interview_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbl_question_sets_tbl_studio_interview_plans_SourcePlanId",
                table: "tbl_question_sets");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_question_sets_tbl_studio_interview_projects_SourceProje~",
                table: "tbl_question_sets");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_question_sets_tbl_studio_question_generation_runs_Sourc~",
                table: "tbl_question_sets");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_ai_chat_messages_tbl_studio_ai_chat_sessions_Ses~",
                table: "tbl_studio_ai_chat_messages");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_ai_chat_messages_tbl_studio_interview_plans_Rela~",
                table: "tbl_studio_ai_chat_messages");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_ai_chat_sessions_tbl_studio_interview_projects_P~",
                table: "tbl_studio_ai_chat_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_ai_chat_sessions_tbl_users_UserId",
                table: "tbl_studio_ai_chat_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_focus_areas_tbl_studio_settings_StudioSettingsId",
                table: "tbl_studio_focus_areas");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_interview_plans_tbl_studio_ai_chat_sessions_Sess~",
                table: "tbl_studio_interview_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_interview_plans_tbl_studio_interview_projects_Pr~",
                table: "tbl_studio_interview_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_interview_plans_tbl_users_ApprovedBy",
                table: "tbl_studio_interview_plans");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_interview_projects_tbl_users_OwnerId",
                table: "tbl_studio_interview_projects");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_interview_questions_tbl_studio_interview_plans_I~",
                table: "tbl_studio_interview_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_interview_questions_tbl_studio_interview_project~",
                table: "tbl_studio_interview_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_interview_questions_tbl_studio_plan_sections_Pla~",
                table: "tbl_studio_interview_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_interview_questions_tbl_studio_question_generati~",
                table: "tbl_studio_interview_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_job_descriptions_tbl_studio_interview_projects_P~",
                table: "tbl_studio_job_descriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_knowledge_documents_tbl_knowledge_documents_Know~",
                table: "tbl_studio_knowledge_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_knowledge_documents_tbl_studio_interview_project~",
                table: "tbl_studio_knowledge_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_plan_approval_histories_tbl_studio_interview_pla~",
                table: "tbl_studio_plan_approval_histories");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_plan_approval_histories_tbl_studio_interview_pro~",
                table: "tbl_studio_plan_approval_histories");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_plan_approval_histories_tbl_users_ActorId",
                table: "tbl_studio_plan_approval_histories");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_plan_focus_areas_tbl_studio_interview_plans_Inte~",
                table: "tbl_studio_plan_focus_areas");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_plan_sections_tbl_studio_interview_plans_Intervi~",
                table: "tbl_studio_plan_sections");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_project_shares_tbl_studio_interview_projects_Pro~",
                table: "tbl_studio_project_shares");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_project_shares_tbl_users_CreatedBy",
                table: "tbl_studio_project_shares");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_question_generation_runs_tbl_studio_interview_pl~",
                table: "tbl_studio_question_generation_runs");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_question_generation_runs_tbl_studio_interview_pr~",
                table: "tbl_studio_question_generation_runs");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_question_generation_runs_tbl_users_RequestedBy",
                table: "tbl_studio_question_generation_runs");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_settings_tbl_studio_interview_plans_AppliedPlanId",
                table: "tbl_studio_settings");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_studio_settings_tbl_studio_interview_projects_ProjectId",
                table: "tbl_studio_settings");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_settings_AppliedPlanId",
                table: "tbl_studio_settings");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_question_generation_runs_InterviewPlanId",
                table: "tbl_studio_question_generation_runs");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_question_generation_runs_ProjectId",
                table: "tbl_studio_question_generation_runs");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_question_generation_runs_RequestedBy",
                table: "tbl_studio_question_generation_runs");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_project_shares_CreatedBy",
                table: "tbl_studio_project_shares");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_project_shares_ProjectId",
                table: "tbl_studio_project_shares");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_plan_sections_InterviewPlanId",
                table: "tbl_studio_plan_sections");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_plan_focus_areas_InterviewPlanId",
                table: "tbl_studio_plan_focus_areas");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_plan_approval_histories_ActorId",
                table: "tbl_studio_plan_approval_histories");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_plan_approval_histories_InterviewPlanId",
                table: "tbl_studio_plan_approval_histories");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_plan_approval_histories_ProjectId",
                table: "tbl_studio_plan_approval_histories");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_interview_questions_GenerationRunId",
                table: "tbl_studio_interview_questions");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_interview_questions_InterviewPlanId",
                table: "tbl_studio_interview_questions");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_interview_questions_PlanSectionId",
                table: "tbl_studio_interview_questions");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_interview_questions_ProjectId",
                table: "tbl_studio_interview_questions");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_interview_questions_ProjectId_OrderIndex",
                table: "tbl_studio_interview_questions");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_interview_projects_OwnerId",
                table: "tbl_studio_interview_projects");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_interview_plans_ApprovedBy",
                table: "tbl_studio_interview_plans");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_interview_plans_SessionId",
                table: "tbl_studio_interview_plans");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_focus_areas_StudioSettingsId",
                table: "tbl_studio_focus_areas");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_ai_chat_sessions_ProjectId",
                table: "tbl_studio_ai_chat_sessions");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_ai_chat_sessions_UserId",
                table: "tbl_studio_ai_chat_sessions");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_ai_chat_messages_RelatedPlanId",
                table: "tbl_studio_ai_chat_messages");

            migrationBuilder.DropIndex(
                name: "IX_tbl_studio_ai_chat_messages_SessionId",
                table: "tbl_studio_ai_chat_messages");

            migrationBuilder.DropIndex(
                name: "IX_tbl_question_sets_SourcePlanId",
                table: "tbl_question_sets");

            migrationBuilder.DropIndex(
                name: "IX_tbl_question_sets_SourceRunId",
                table: "tbl_question_sets");
        }
    }
}
