using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Migrations
{
    /// <inheritdoc />
    public partial class InitialReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.CreateTable(
                name: "Drafts",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ParentDisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ContactDisplay = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Comment = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Roadmap = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Emoji = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drafts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drafts_OwnerId_ClassId_StudentId_Month",
                schema: "reporting",
                table: "Drafts",
                columns: new[] { "OwnerId", "ClassId", "StudentId", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Drafts",
                schema: "reporting");
        }
    }
}
