using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptsAndAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Receipts",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AmountVnd = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UnallocatedVnd = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.CheckConstraint("CK_Receipt_Amount", "\"AmountVnd\" > 0");
                    table.CheckConstraint("CK_Receipt_Unallocated", "\"UnallocatedVnd\" >= 0 AND \"UnallocatedVnd\" <= \"AmountVnd\"");
                    table.ForeignKey(
                        name: "FK_Receipts_Ledgers_ClassId",
                        column: x => x.ClassId,
                        principalSchema: "billing",
                        principalTable: "Ledgers",
                        principalColumn: "ClassId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptAllocation",
                schema: "billing",
                columns: table => new
                {
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    AmountVnd = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptAllocation", x => new { x.ReceiptId, x.Period });
                    table.CheckConstraint("CK_Allocation_Amount", "\"AmountVnd\" > 0");
                    table.ForeignKey(
                        name: "FK_ReceiptAllocation_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "billing",
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_ClassId_IdempotencyKey",
                schema: "billing",
                table: "Receipts",
                columns: new[] { "ClassId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_ClassId_StudentId_ReceivedOn",
                schema: "billing",
                table: "Receipts",
                columns: new[] { "ClassId", "StudentId", "ReceivedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiptAllocation",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Receipts",
                schema: "billing");
        }
    }
}
