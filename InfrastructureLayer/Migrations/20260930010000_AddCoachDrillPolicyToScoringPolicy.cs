using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>SCRUM-488: Admin cấu hình số câu drill / remix / ngưỡng pass.</summary>
[DbContext(typeof(InfrastructureLayer.Database.AppDbContext))]
[Migration("20260930010000_AddCoachDrillPolicyToScoringPolicy")]
public partial class AddCoachDrillPolicyToScoringPolicy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "DrillPassScoreExclusiveMin",
            table: "tbl_competency_scoring_policies",
            type: "double precision",
            nullable: false,
            defaultValue: 70.0);

        migrationBuilder.AddColumn<int>(
            name: "DrillQuestionCountWeak",
            table: "tbl_competency_scoring_policies",
            type: "integer",
            nullable: false,
            defaultValue: 20);

        migrationBuilder.AddColumn<int>(
            name: "DrillQuestionCountMid",
            table: "tbl_competency_scoring_policies",
            type: "integer",
            nullable: false,
            defaultValue: 15);

        migrationBuilder.AddColumn<int>(
            name: "DrillQuestionCountStrong",
            table: "tbl_competency_scoring_policies",
            type: "integer",
            nullable: false,
            defaultValue: 10);

        migrationBuilder.AddColumn<double>(
            name: "DrillWeakBandRatio",
            table: "tbl_competency_scoring_policies",
            type: "double precision",
            nullable: false,
            defaultValue: 0.6);

        migrationBuilder.AddColumn<bool>(
            name: "DrillRemixEnabled",
            table: "tbl_competency_scoring_policies",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<double>(
            name: "DrillRemixRatio",
            table: "tbl_competency_scoring_policies",
            type: "double precision",
            nullable: false,
            defaultValue: 0.35);

        migrationBuilder.AddColumn<double>(
            name: "DrillWeakAnswerScoreMaxExclusive",
            table: "tbl_competency_scoring_policies",
            type: "double precision",
            nullable: false,
            defaultValue: 50.0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DrillPassScoreExclusiveMin", table: "tbl_competency_scoring_policies");
        migrationBuilder.DropColumn(name: "DrillQuestionCountWeak", table: "tbl_competency_scoring_policies");
        migrationBuilder.DropColumn(name: "DrillQuestionCountMid", table: "tbl_competency_scoring_policies");
        migrationBuilder.DropColumn(name: "DrillQuestionCountStrong", table: "tbl_competency_scoring_policies");
        migrationBuilder.DropColumn(name: "DrillWeakBandRatio", table: "tbl_competency_scoring_policies");
        migrationBuilder.DropColumn(name: "DrillRemixEnabled", table: "tbl_competency_scoring_policies");
        migrationBuilder.DropColumn(name: "DrillRemixRatio", table: "tbl_competency_scoring_policies");
        migrationBuilder.DropColumn(name: "DrillWeakAnswerScoreMaxExclusive", table: "tbl_competency_scoring_policies");
    }
}
