using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceAccess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstructionsStaticQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "StaticQr",
                schema: "service_access",
                table: "payment_instructions",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StaticQr",
                schema: "service_access",
                table: "payment_instructions");
        }
    }
}
