using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BillingReserve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReservedVnd",
                schema: "billing",
                table: "Receipts",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ReserveRequests",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbsentFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    AbsentTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EvidenceNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RequestedVnd = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreateKey = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionKey = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReserveRequests", x => x.Id);
                    table.CheckConstraint("CK_Reserve_Amount", "\"RequestedVnd\" > 0");
                    table.CheckConstraint("CK_Reserve_Dates", "\"AbsentTo\" >= \"AbsentFrom\"");
                    table.CheckConstraint("CK_Reserve_Status", "\"Status\" IN ('pending', 'approved', 'rejected')");
                    table.ForeignKey(
                        name: "FK_ReserveRequests_Ledgers_ClassId",
                        column: x => x.ClassId,
                        principalSchema: "billing",
                        principalTable: "Ledgers",
                        principalColumn: "ClassId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReserveEvents",
                schema: "billing",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AmountVnd = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReserveEvents", x => new { x.RequestId, x.Version });
                    table.ForeignKey(
                        name: "FK_ReserveEvents_ReserveRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "billing",
                        principalTable: "ReserveRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReserveSources",
                schema: "billing",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountVnd = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReserveSources", x => new { x.RequestId, x.ReceiptId });
                    table.CheckConstraint("CK_ReserveSource_Amount", "\"AmountVnd\" > 0");
                    table.ForeignKey(
                        name: "FK_ReserveSources_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "billing",
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReserveSources_ReserveRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "billing",
                        principalTable: "ReserveRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Receipt_Reserved",
                schema: "billing",
                table: "Receipts",
                sql: "\"ReservedVnd\" >= 0 AND \"ReservedVnd\" <= \"UnallocatedVnd\"");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveRequests_ClassId_CreateKey",
                schema: "billing",
                table: "ReserveRequests",
                columns: new[] { "ClassId", "CreateKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReserveRequests_ClassId_StudentId_Status",
                schema: "billing",
                table: "ReserveRequests",
                columns: new[] { "ClassId", "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSources_ReceiptId",
                schema: "billing",
                table: "ReserveSources",
                column: "ReceiptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReserveEvents",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "ReserveSources",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "ReserveRequests",
                schema: "billing");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Receipt_Reserved",
                schema: "billing",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "ReservedVnd",
                schema: "billing",
                table: "Receipts");
        }
    }
}
