using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddGamification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_daily_progress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    XpEarned = table.Column<int>(type: "integer", nullable: false),
                    QuestionsCompleted = table.Column<int>(type: "integer", nullable: false),
                    SessionsCompleted = table.Column<int>(type: "integer", nullable: false),
                    DailyGoalCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_daily_progress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_daily_progress_tbl_users_UserId",
                        column: x => x.UserId,
                        principalTable: "tbl_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_user_achievements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AchievementCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UnlockedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_user_achievements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_user_achievements_tbl_users_UserId",
                        column: x => x.UserId,
                        principalTable: "tbl_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_user_progress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalXp = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CurrentStreak = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LongestStreak = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastActiveLocalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DailyGoalXp = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                    TotalQuestionsCompleted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalSessionsCompleted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalTechnicalQuestionsCompleted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalSystemDesignQuestionsCompleted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    DailyGoalCompletedDaysCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_user_progress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_user_progress_tbl_users_UserId",
                        column: x => x.UserId,
                        principalTable: "tbl_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_xp_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuestionAttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuestionSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    PracticeSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_xp_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tbl_xp_transactions_tbl_users_UserId",
                        column: x => x.UserId,
                        principalTable: "tbl_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_daily_progress_UserId_LocalDate",
                table: "tbl_daily_progress",
                columns: new[] { "UserId", "LocalDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_user_achievements_UserId_AchievementCode",
                table: "tbl_user_achievements",
                columns: new[] { "UserId", "AchievementCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_user_progress_UserId",
                table: "tbl_user_progress",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_xp_transactions_IdempotencyKey",
                table: "tbl_xp_transactions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_xp_transactions_UserId_CreatedAtUtc",
                table: "tbl_xp_transactions",
                columns: new[] { "UserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_daily_progress");

            migrationBuilder.DropTable(
                name: "tbl_user_achievements");

            migrationBuilder.DropTable(
                name: "tbl_user_progress");

            migrationBuilder.DropTable(
                name: "tbl_xp_transactions");
        }
    }
}
