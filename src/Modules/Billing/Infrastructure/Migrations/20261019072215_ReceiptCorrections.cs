using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptAllocation_Receipts_ReceiptId",
                schema: "billing",
                table: "ReceiptAllocation");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CorrectedAt",
                schema: "billing",
                table: "Receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrectionKey",
                schema: "billing",
                table: "Receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionReason",
                schema: "billing",
                table: "Receipts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionRequest",
                schema: "billing",
                table: "Receipts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionResult",
                schema: "billing",
                table: "Receipts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacementId",
                schema: "billing",
                table: "Receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "billing",
                table: "Receipts",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "active");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_ClassId_CorrectionKey",
                schema: "billing",
                table: "Receipts",
                columns: new[] { "ClassId", "CorrectionKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptAllocation_Receipts_ReceiptId",
                schema: "billing",
                table: "ReceiptAllocation",
                column: "ReceiptId",
                principalSchema: "billing",
                principalTable: "Receipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptAllocation_Receipts_ReceiptId",
                schema: "billing",
                table: "ReceiptAllocation");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_ClassId_CorrectionKey",
                schema: "billing",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "CorrectedAt",
                schema: "billing",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "CorrectionKey",
                schema: "billing",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "CorrectionReason",
                schema: "billing",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "CorrectionRequest",
                schema: "billing",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "CorrectionResult",
                schema: "billing",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "ReplacementId",
                schema: "billing",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "billing",
                table: "Receipts");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptAllocation_Receipts_ReceiptId",
                schema: "billing",
                table: "ReceiptAllocation",
                column: "ReceiptId",
                principalSchema: "billing",
                principalTable: "Receipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
