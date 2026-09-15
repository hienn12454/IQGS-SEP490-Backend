using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// Data-only: đồng bộ LimitsSnapshotJson của subscription Active với Plan.LimitsJson hiện tại
/// (Admin đã đổi template nhưng snapshot cũ — vd Ask-AI 1000 vs 300).
/// Không đụng UsageCounter.
/// </summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260911120000_SyncActiveSubscriptionLimitsSnapshots")]
public partial class SyncActiveSubscriptionLimitsSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "tbl_subscriptions" s
            SET "LimitsSnapshotJson" = p."LimitsJson",
                "UpdatedAt" = NOW() AT TIME ZONE 'utc'
            FROM "tbl_subscription_plans" p
            WHERE s."PlanId" = p."Id"
              AND s."IsActive" = true
              AND s."Status" = 'Active';
            """);
    }

    /// <summary>Không khôi phục snapshot cũ đã lệch template.</summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
