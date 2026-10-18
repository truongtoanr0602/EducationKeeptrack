using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentRecipient",
                schema: "billing",
                columns: table => new
                {
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    BankBin = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountNo = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StaticQr = table.Column<byte[]>(type: "bytea", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRecipient", x => x.OwnerId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentRecipient",
                schema: "billing");
        }
    }
}
