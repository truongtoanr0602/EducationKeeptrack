using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.CreateTable(
                name: "Inbox",
                schema: "billing",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inbox", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "Ledgers",
                schema: "billing",
                columns: table => new
                {
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RateVersion = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    NeedsRefresh = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ledgers", x => x.ClassId);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TuitionRate",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    UnitPriceVnd = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TuitionRate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TuitionRate_Ledgers_ClassId",
                        column: x => x.ClassId,
                        principalSchema: "billing",
                        principalTable: "Ledgers",
                        principalColumn: "ClassId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Charges",
                schema: "billing",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceVersion = table.Column<int>(type: "integer", nullable: false),
                    RateId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitPriceVnd = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Charges", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_Charges_Ledgers_ClassId",
                        column: x => x.ClassId,
                        principalSchema: "billing",
                        principalTable: "Ledgers",
                        principalColumn: "ClassId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Charges_TuitionRate_RateId",
                        column: x => x.RateId,
                        principalSchema: "billing",
                        principalTable: "TuitionRate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentCharge",
                schema: "billing",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttendanceVersion = table.Column<int>(type: "integer", nullable: false),
                    AmountVnd = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCharge", x => new { x.SessionId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_StudentCharge_Charges_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "billing",
                        principalTable: "Charges",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_ClassId_Date",
                schema: "billing",
                table: "Charges",
                columns: new[] { "ClassId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_RateId",
                schema: "billing",
                table: "Charges",
                column: "RateId");

            migrationBuilder.CreateIndex(
                name: "IX_Ledgers_NeedsRefresh",
                schema: "billing",
                table: "Ledgers",
                column: "NeedsRefresh");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_ClassId_Version",
                schema: "billing",
                table: "Outbox",
                columns: new[] { "ClassId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_DispatchedAt",
                schema: "billing",
                table: "Outbox",
                column: "DispatchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TuitionRate_ClassId_EffectiveFrom",
                schema: "billing",
                table: "TuitionRate",
                columns: new[] { "ClassId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TuitionRate_ClassId_Version",
                schema: "billing",
                table: "TuitionRate",
                columns: new[] { "ClassId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inbox",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "StudentCharge",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Charges",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "TuitionRate",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Ledgers",
                schema: "billing");
        }
    }
}
