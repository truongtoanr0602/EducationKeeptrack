using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceAccess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ServicePeriodsAndRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "service_access",
                columns: table => new
                {
                    AccountId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AiEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.AccountId);
                });

            migrationBuilder.CreateTable(
                name: "history",
                schema: "service_access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ActorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ActorEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_instructions",
                schema: "service_access",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    BankDetails = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Contact = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_instructions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_requests",
                schema: "service_access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TransactionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    VerificationNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DecidedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_requests_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "service_access",
                        principalTable: "accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "periods",
                schema: "service_access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_periods", x => x.Id);
                    table.CheckConstraint("CK_periods_range", "\"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_periods_accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "service_access",
                        principalTable: "accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_history_AccountId_At",
                schema: "service_access",
                table: "history",
                columns: new[] { "AccountId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_AccountId_IdempotencyKey",
                schema: "service_access",
                table: "payment_requests",
                columns: new[] { "AccountId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_requests_open",
                schema: "service_access",
                table: "payment_requests",
                column: "AccountId",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_periods_AccountId_StartsAt",
                schema: "service_access",
                table: "periods",
                columns: new[] { "AccountId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_periods_SourceRequestId",
                schema: "service_access",
                table: "periods",
                column: "SourceRequestId",
                unique: true);

            // Keep access for accounts provisioned by the T01 development grant before dropping it.
            migrationBuilder.Sql("""
                INSERT INTO service_access.accounts ("AccountId", "AiEnabled")
                SELECT "AccountId", false FROM service_access."DevelopmentGrants"
                ON CONFLICT DO NOTHING;
                INSERT INTO service_access.periods ("Id", "AccountId", "StartsAt", "EndsAt", "Label", "SourceRequestId", "CreatedBy", "CreatedAt", "Reason")
                SELECT gen_random_uuid(), "AccountId", LEAST(date_trunc('second', now()), "Until" - interval '30 days'), "Until",
                       'Cấp phát triển', NULL, 'migration', now(), 'Chuyển từ DevelopmentGrant (T01)'
                FROM service_access."DevelopmentGrants" WHERE "Approved";
                """);
            migrationBuilder.DropTable(
                name: "DevelopmentGrants",
                schema: "service_access");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "history",
                schema: "service_access");

            migrationBuilder.DropTable(
                name: "payment_instructions",
                schema: "service_access");

            migrationBuilder.DropTable(
                name: "payment_requests",
                schema: "service_access");

            migrationBuilder.DropTable(
                name: "periods",
                schema: "service_access");

            migrationBuilder.DropTable(
                name: "accounts",
                schema: "service_access");

            migrationBuilder.CreateTable(
                name: "DevelopmentGrants",
                schema: "service_access",
                columns: table => new
                {
                    AccountId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false),
                    Until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevelopmentGrants", x => x.AccountId);
                });
        }
    }
}
