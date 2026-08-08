using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Chỉ RENAME TABLE — PostgreSQL giữ nguyên data, FK, PK, index gắn theo bảng.
    /// Tránh DropForeignKey/DropPrimaryKey vì schema drift (tên constraint lệch snapshot EF,
    /// ví dụ knowledge_chunks: FK ...DocumentId vs ...document_id).
    /// </remarks>
    public partial class RenameTablesWithTblPrefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Core (PascalCase → tbl_snake)
            RenameIfExists(migrationBuilder, "Roles", "tbl_roles");
            RenameIfExists(migrationBuilder, "Users", "tbl_users");
            RenameIfExists(migrationBuilder, "Companies", "tbl_companies");
            RenameIfExists(migrationBuilder, "HRProfiles", "tbl_hr_profiles");
            RenameIfExists(migrationBuilder, "CandidateProfiles", "tbl_candidate_profiles");

            // App domain (snake → tbl_snake)
            RenameIfExists(migrationBuilder, "knowledge_documents", "tbl_knowledge_documents");
            RenameIfExists(migrationBuilder, "knowledge_chunks", "tbl_knowledge_chunks");
            RenameIfExists(migrationBuilder, "question_sets", "tbl_question_sets");
            RenameIfExists(migrationBuilder, "question_set_questions", "tbl_question_set_questions");
            RenameIfExists(migrationBuilder, "question_set_bookmarks", "tbl_question_set_bookmarks");
            RenameIfExists(migrationBuilder, "hr_question_set_bookmarks", "tbl_hr_question_set_bookmarks");
            RenameIfExists(migrationBuilder, "practice_sessions", "tbl_practice_sessions");
            RenameIfExists(migrationBuilder, "candidate_answers", "tbl_candidate_answers");
            RenameIfExists(migrationBuilder, "ai_feedbacks", "tbl_ai_feedbacks");
            RenameIfExists(migrationBuilder, "candidate_recommendations", "tbl_candidate_recommendations");
            RenameIfExists(migrationBuilder, "candidate_invitations", "tbl_candidate_invitations");
            RenameIfExists(migrationBuilder, "candidate_offers", "tbl_candidate_offers");
            RenameIfExists(migrationBuilder, "platform_settings", "tbl_platform_settings");
            RenameIfExists(migrationBuilder, "subscription_plans", "tbl_subscription_plans");
            RenameIfExists(migrationBuilder, "subscriptions", "tbl_subscriptions");
            RenameIfExists(migrationBuilder, "usage_counters", "tbl_usage_counters");
            RenameIfExists(migrationBuilder, "subscription_transactions", "tbl_subscription_transactions");

            // Studio
            RenameIfExists(migrationBuilder, "studio_interview_projects", "tbl_studio_interview_projects");
            RenameIfExists(migrationBuilder, "studio_job_descriptions", "tbl_studio_job_descriptions");
            RenameIfExists(migrationBuilder, "studio_knowledge_documents", "tbl_studio_knowledge_documents");
            RenameIfExists(migrationBuilder, "studio_ai_chat_sessions", "tbl_studio_ai_chat_sessions");
            RenameIfExists(migrationBuilder, "studio_ai_chat_messages", "tbl_studio_ai_chat_messages");
            RenameIfExists(migrationBuilder, "studio_interview_plans", "tbl_studio_interview_plans");
            RenameIfExists(migrationBuilder, "studio_plan_sections", "tbl_studio_plan_sections");
            RenameIfExists(migrationBuilder, "studio_plan_focus_areas", "tbl_studio_plan_focus_areas");
            RenameIfExists(migrationBuilder, "studio_plan_approval_histories", "tbl_studio_plan_approval_histories");
            RenameIfExists(migrationBuilder, "studio_settings", "tbl_studio_settings");
            RenameIfExists(migrationBuilder, "studio_focus_areas", "tbl_studio_focus_areas");
            RenameIfExists(migrationBuilder, "studio_interview_questions", "tbl_studio_interview_questions");
            RenameIfExists(migrationBuilder, "studio_question_generation_runs", "tbl_studio_question_generation_runs");
            RenameIfExists(migrationBuilder, "studio_project_shares", "tbl_studio_project_shares");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RenameIfExists(migrationBuilder, "tbl_roles", "Roles");
            RenameIfExists(migrationBuilder, "tbl_users", "Users");
            RenameIfExists(migrationBuilder, "tbl_companies", "Companies");
            RenameIfExists(migrationBuilder, "tbl_hr_profiles", "HRProfiles");
            RenameIfExists(migrationBuilder, "tbl_candidate_profiles", "CandidateProfiles");

            RenameIfExists(migrationBuilder, "tbl_knowledge_documents", "knowledge_documents");
            RenameIfExists(migrationBuilder, "tbl_knowledge_chunks", "knowledge_chunks");
            RenameIfExists(migrationBuilder, "tbl_question_sets", "question_sets");
            RenameIfExists(migrationBuilder, "tbl_question_set_questions", "question_set_questions");
            RenameIfExists(migrationBuilder, "tbl_question_set_bookmarks", "question_set_bookmarks");
            RenameIfExists(migrationBuilder, "tbl_hr_question_set_bookmarks", "hr_question_set_bookmarks");
            RenameIfExists(migrationBuilder, "tbl_practice_sessions", "practice_sessions");
            RenameIfExists(migrationBuilder, "tbl_candidate_answers", "candidate_answers");
            RenameIfExists(migrationBuilder, "tbl_ai_feedbacks", "ai_feedbacks");
            RenameIfExists(migrationBuilder, "tbl_candidate_recommendations", "candidate_recommendations");
            RenameIfExists(migrationBuilder, "tbl_candidate_invitations", "candidate_invitations");
            RenameIfExists(migrationBuilder, "tbl_candidate_offers", "candidate_offers");
            RenameIfExists(migrationBuilder, "tbl_platform_settings", "platform_settings");
            RenameIfExists(migrationBuilder, "tbl_subscription_plans", "subscription_plans");
            RenameIfExists(migrationBuilder, "tbl_subscriptions", "subscriptions");
            RenameIfExists(migrationBuilder, "tbl_usage_counters", "usage_counters");
            RenameIfExists(migrationBuilder, "tbl_subscription_transactions", "subscription_transactions");

            RenameIfExists(migrationBuilder, "tbl_studio_interview_projects", "studio_interview_projects");
            RenameIfExists(migrationBuilder, "tbl_studio_job_descriptions", "studio_job_descriptions");
            RenameIfExists(migrationBuilder, "tbl_studio_knowledge_documents", "studio_knowledge_documents");
            RenameIfExists(migrationBuilder, "tbl_studio_ai_chat_sessions", "studio_ai_chat_sessions");
            RenameIfExists(migrationBuilder, "tbl_studio_ai_chat_messages", "studio_ai_chat_messages");
            RenameIfExists(migrationBuilder, "tbl_studio_interview_plans", "studio_interview_plans");
            RenameIfExists(migrationBuilder, "tbl_studio_plan_sections", "studio_plan_sections");
            RenameIfExists(migrationBuilder, "tbl_studio_plan_focus_areas", "studio_plan_focus_areas");
            RenameIfExists(migrationBuilder, "tbl_studio_plan_approval_histories", "studio_plan_approval_histories");
            RenameIfExists(migrationBuilder, "tbl_studio_settings", "studio_settings");
            RenameIfExists(migrationBuilder, "tbl_studio_focus_areas", "studio_focus_areas");
            RenameIfExists(migrationBuilder, "tbl_studio_interview_questions", "studio_interview_questions");
            RenameIfExists(migrationBuilder, "tbl_studio_question_generation_runs", "studio_question_generation_runs");
            RenameIfExists(migrationBuilder, "tbl_studio_project_shares", "studio_project_shares");
        }

        /// <summary>
        /// RENAME chỉ khi bảng nguồn còn tồn tại và bảng đích chưa có — idempotent / an toàn retry.
        /// %I giữ đúng casing PostgreSQL ("Users" vs users).
        /// </summary>
        private static void RenameIfExists(MigrationBuilder migrationBuilder, string from, string to)
        {
            var fromLit = from.Replace("'", "''");
            var toLit = to.Replace("'", "''");
            migrationBuilder.Sql($"""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = '{fromLit}'
                    ) AND NOT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = '{toLit}'
                    ) THEN
                        EXECUTE format('ALTER TABLE %I RENAME TO %I', '{fromLit}', '{toLit}');
                    END IF;
                END $$;
                """);
        }
    }
}
