using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BillingReserveUse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Reserve_Status",
                schema: "billing",
                table: "ReserveRequests");

            migrationBuilder.AddColumn<Guid>(
                name: "CancelKey",
                schema: "billing",
                table: "ReserveRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                schema: "billing",
                table: "ReserveRequests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                schema: "billing",
                table: "ReserveRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReserveUses",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    AmountVnd = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    UsedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    UseKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ReversalKey = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReversedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReserveUses", x => x.Id);
                    table.CheckConstraint("CK_ReserveUse_Amount", "\"AmountVnd\" > 0");
                    table.CheckConstraint("CK_ReserveUse_Status", "\"Status\" IN ('active', 'reversed')");
                    table.ForeignKey(
                        name: "FK_ReserveUses_Ledgers_ClassId",
                        column: x => x.ClassId,
                        principalSchema: "billing",
                        principalTable: "Ledgers",
                        principalColumn: "ClassId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReserveUseSources",
                schema: "billing",
                columns: table => new
                {
                    UseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountVnd = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReserveUseSources", x => new { x.UseId, x.RequestId });
                    table.CheckConstraint("CK_ReserveUseSource_Amount", "\"AmountVnd\" > 0");
                    table.ForeignKey(
                        name: "FK_ReserveUseSources_ReserveRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "billing",
                        principalTable: "ReserveRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReserveUseSources_ReserveUses_UseId",
                        column: x => x.UseId,
                        principalSchema: "billing",
                        principalTable: "ReserveUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reserve_Status",
                schema: "billing",
                table: "ReserveRequests",
                sql: "\"Status\" IN ('pending', 'approved', 'rejected', 'cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveUses_ClassId_StudentId_Period",
                schema: "billing",
                table: "ReserveUses",
                columns: new[] { "ClassId", "StudentId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_ReserveUses_ClassId_UseKey",
                schema: "billing",
                table: "ReserveUses",
                columns: new[] { "ClassId", "UseKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReserveUseSources_RequestId",
                schema: "billing",
                table: "ReserveUseSources",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReserveUseSources",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "ReserveUses",
                schema: "billing");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reserve_Status",
                schema: "billing",
                table: "ReserveRequests");

            migrationBuilder.DropColumn(
                name: "CancelKey",
                schema: "billing",
                table: "ReserveRequests");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                schema: "billing",
                table: "ReserveRequests");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "billing",
                table: "ReserveRequests");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reserve_Status",
                schema: "billing",
                table: "ReserveRequests",
                sql: "\"Status\" IN ('pending', 'approved', 'rejected')");
        }
    }
}
