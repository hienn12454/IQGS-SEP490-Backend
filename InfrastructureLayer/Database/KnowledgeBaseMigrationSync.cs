using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InfrastructureLayer.Database;

/// <summary>
/// Shared DB với RAG — bảng knowledge_* có thể đã tồn tại (RAG tạo knowledge_chunks snake_case).
/// Chỉ tạo bảng question_generation_* còn thiếu rồi ghi nhận migration history.
/// </summary>
public static class KnowledgeBaseMigrationSync
{
    private const string MigrationId = "20260617165057_AddKnowledgeBaseAndQuestionGenerationTables";

    public static async Task SyncIfNeededAsync(AppDbContext context, ILogger logger)
    {
        var pending = await context.Database.GetPendingMigrationsAsync();
        if (!pending.Contains(MigrationId))
            return;

        var knowledgeTableExists = await TableExistsAsync(context, "knowledge_documents");
        if (!knowledgeTableExists)
            return;

        logger.LogWarning(
            "Phát hiện bảng knowledge_documents đã tồn tại — sync migration {MigrationId} (chỉ question_generation_*).",
            MigrationId);

        await context.Database.ExecuteSqlRawAsync(QuestionGenerationTablesSql);
        await context.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('{MigrationId}', '8.0.0')
            ON CONFLICT ("MigrationId") DO NOTHING;
            """);

        logger.LogInformation("Đã sync migration {MigrationId} thành công.", MigrationId);
    }

    private static async Task<bool> TableExistsAsync(AppDbContext context, string tableName)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = @name
                );
                """;
            var param = cmd.CreateParameter();
            param.ParameterName = "name";
            param.Value = tableName;
            cmd.Parameters.Add(param);
            var result = await cmd.ExecuteScalarAsync();
            return result is bool exists && exists;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private const string QuestionGenerationTablesSql = """
        CREATE EXTENSION IF NOT EXISTS vector;

        CREATE TABLE IF NOT EXISTS question_generation_jobs (
            "Id" uuid NOT NULL,
            "OwnerId" uuid NOT NULL,
            "JobDescription" text NOT NULL,
            "HrNote" character varying(2000),
            "JdInputType" character varying(10) NOT NULL DEFAULT 'TEXT',
            "JdFileName" character varying(500),
            "NumberOfQuestions" integer NOT NULL,
            "Difficulty" character varying(20) NOT NULL,
            "QuestionTypesJson" jsonb NOT NULL,
            "SkillsJson" jsonb NOT NULL,
            "Status" character varying(30) NOT NULL,
            "ErrorMessage" character varying(2000),
            "CompletedAt" timestamp with time zone,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone,
            "IsActive" boolean NOT NULL,
            CONSTRAINT "PK_question_generation_jobs" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS generated_questions (
            "Id" uuid NOT NULL,
            "JobId" uuid NOT NULL,
            "Order" integer NOT NULL,
            "Question" text NOT NULL,
            "QuestionType" character varying(50) NOT NULL,
            "Difficulty" character varying(20) NOT NULL,
            "Skill" character varying(200),
            "FocusArea" character varying(200),
            "Rationale" text,
            "SampleAnswer" text,
            "EvaluationCriteriaJson" jsonb NOT NULL,
            "CitationsJson" jsonb NOT NULL,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone,
            "IsActive" boolean NOT NULL,
            CONSTRAINT "PK_generated_questions" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_generated_questions_question_generation_jobs_JobId"
                FOREIGN KEY ("JobId") REFERENCES question_generation_jobs ("Id") ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS question_generation_plans (
            "Id" uuid NOT NULL,
            "JobId" uuid NOT NULL,
            "PlanJson" jsonb NOT NULL,
            "IsApproved" boolean NOT NULL,
            "ApprovedAt" timestamp with time zone,
            "CreatedAt" timestamp with time zone NOT NULL,
            "UpdatedAt" timestamp with time zone,
            "IsActive" boolean NOT NULL,
            CONSTRAINT "PK_question_generation_plans" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_question_generation_plans_question_generation_jobs_JobId"
                FOREIGN KEY ("JobId") REFERENCES question_generation_jobs ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_generated_questions_JobId" ON generated_questions ("JobId");
        CREATE INDEX IF NOT EXISTS "IX_question_generation_jobs_OwnerId" ON question_generation_jobs ("OwnerId");
        CREATE INDEX IF NOT EXISTS "IX_question_generation_jobs_Status" ON question_generation_jobs ("Status");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_question_generation_plans_JobId" ON question_generation_plans ("JobId");
        """;
}
