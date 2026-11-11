using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceAccess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlanningQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_periods_quota",
                schema: "service_access",
                table: "periods");

            migrationBuilder.AddColumn<int>(
                name: "PlanningQuota",
                schema: "service_access",
                table: "periods",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_periods_quota",
                schema: "service_access",
                table: "periods",
                sql: "(\"GradingQuota\" IS NULL OR \"GradingQuota\" >= 0) AND (\"CommentQuota\" IS NULL OR \"CommentQuota\" >= 0) AND (\"PlanningQuota\" IS NULL OR \"PlanningQuota\" >= 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_periods_quota",
                schema: "service_access",
                table: "periods");

            migrationBuilder.DropColumn(
                name: "PlanningQuota",
                schema: "service_access",
                table: "periods");

            migrationBuilder.AddCheckConstraint(
                name: "CK_periods_quota",
                schema: "service_access",
                table: "periods",
                sql: "(\"GradingQuota\" IS NULL OR \"GradingQuota\" >= 0) AND (\"CommentQuota\" IS NULL OR \"CommentQuota\" >= 0)");
        }
    }
}
