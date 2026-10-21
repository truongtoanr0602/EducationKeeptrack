using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Migrations
{
    /// <inheritdoc />
    public partial class ExportLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LeaseOwner",
                schema: "reporting",
                table: "Exports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseUntil",
                schema: "reporting",
                table: "Exports",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeaseOwner",
                schema: "reporting",
                table: "Exports");

            migrationBuilder.DropColumn(
                name: "LeaseUntil",
                schema: "reporting",
                table: "Exports");
        }
    }
}
