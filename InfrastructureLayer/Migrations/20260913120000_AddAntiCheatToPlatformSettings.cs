using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>SCRUM-446: Admin anti-cheat toggle + snapshot/cột đếm rời tab trên practice session.</summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260913120000_AddAntiCheatToPlatformSettings")]
public partial class AddAntiCheatToPlatformSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "AntiCheatEnabled",
            table: "tbl_platform_settings",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "AntiCheatMaxTabLeaves",
            table: "tbl_platform_settings",
            type: "integer",
            nullable: false,
            defaultValue: 3);

        // defaultValue trên AddColumn đã gán giá trị cho dòng seed hiện có — không cần UpdateData
        // (UpdateData lỗi với migration hand-written thiếu Designer model mapping).

        migrationBuilder.AddColumn<bool>(
            name: "AntiCheatEnabled",
            table: "tbl_practice_sessions",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "AntiCheatMaxTabLeaves",
            table: "tbl_practice_sessions",
            type: "integer",
            nullable: false,
            defaultValue: 3);

        migrationBuilder.AddColumn<int>(
            name: "TabLeaveCount",
            table: "tbl_practice_sessions",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastTabLeaveAt",
            table: "tbl_practice_sessions",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AntiCheatEnabled",
            table: "tbl_platform_settings");

        migrationBuilder.DropColumn(
            name: "AntiCheatMaxTabLeaves",
            table: "tbl_platform_settings");

        migrationBuilder.DropColumn(
            name: "AntiCheatEnabled",
            table: "tbl_practice_sessions");

        migrationBuilder.DropColumn(
            name: "AntiCheatMaxTabLeaves",
            table: "tbl_practice_sessions");

        migrationBuilder.DropColumn(
            name: "TabLeaveCount",
            table: "tbl_practice_sessions");

        migrationBuilder.DropColumn(
            name: "LastTabLeaveAt",
            table: "tbl_practice_sessions");
    }
}
