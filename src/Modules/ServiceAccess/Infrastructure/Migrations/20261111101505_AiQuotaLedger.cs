using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceAccess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AiQuotaLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommentQuota",
                schema: "service_access",
                table: "periods",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GradingQuota",
                schema: "service_access",
                table: "periods",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "quota_reservations",
                schema: "service_access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    RequestKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Units = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SettledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quota_reservations", x => x.Id);
                    table.CheckConstraint("CK_quota_reservations_units", "\"Units\" > 0");
                    table.ForeignKey(
                        name: "FK_quota_reservations_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "service_access",
                        principalTable: "periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_periods_quota",
                schema: "service_access",
                table: "periods",
                sql: "(\"GradingQuota\" IS NULL OR \"GradingQuota\" >= 0) AND (\"CommentQuota\" IS NULL OR \"CommentQuota\" >= 0)");

            migrationBuilder.CreateIndex(
                name: "IX_quota_reservations_AccountId_Kind_RequestKey",
                schema: "service_access",
                table: "quota_reservations",
                columns: new[] { "AccountId", "Kind", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quota_reservations_PeriodId_Kind_Status",
                schema: "service_access",
                table: "quota_reservations",
                columns: new[] { "PeriodId", "Kind", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quota_reservations",
                schema: "service_access");

            migrationBuilder.DropCheckConstraint(
                name: "CK_periods_quota",
                schema: "service_access",
                table: "periods");

            migrationBuilder.DropColumn(
                name: "CommentQuota",
                schema: "service_access",
                table: "periods");

            migrationBuilder.DropColumn(
                name: "GradingQuota",
                schema: "service_access",
                table: "periods");
        }
    }
}
