using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddSePayFieldsToSubscriptionTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "subscription_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "subscription_transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalTransactionId",
                table: "subscription_transactions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderCode",
                table: "subscription_transactions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawPayloadJson",
                table: "subscription_transactions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscription_transactions_ExternalTransactionId",
                table: "subscription_transactions",
                column: "ExternalTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscription_transactions_OrderCode",
                table: "subscription_transactions",
                column: "OrderCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subscription_transactions_ExternalTransactionId",
                table: "subscription_transactions");

            migrationBuilder.DropIndex(
                name: "IX_subscription_transactions_OrderCode",
                table: "subscription_transactions");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "subscription_transactions");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "subscription_transactions");

            migrationBuilder.DropColumn(
                name: "ExternalTransactionId",
                table: "subscription_transactions");

            migrationBuilder.DropColumn(
                name: "OrderCode",
                table: "subscription_transactions");

            migrationBuilder.DropColumn(
                name: "RawPayloadJson",
                table: "subscription_transactions");
        }
    }
}
