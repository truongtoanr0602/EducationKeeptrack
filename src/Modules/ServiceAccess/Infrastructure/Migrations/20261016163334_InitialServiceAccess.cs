using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceAccess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialServiceAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "service_access");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DevelopmentGrants",
                schema: "service_access");
        }
    }
}
