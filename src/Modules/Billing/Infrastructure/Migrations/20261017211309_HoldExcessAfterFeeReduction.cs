using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HoldExcessAfterFeeReduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentReviews",
                schema: "billing",
                columns: table => new
                {
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AmountVnd = table.Column<decimal>(type: "numeric(28,0)", precision: 28, scale: 0, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReviews", x => new { x.ClassId, x.StudentId, x.Period });
                    table.CheckConstraint("CK_PaymentReview_Amount", "\"AmountVnd\" > 0");
                    table.ForeignKey(
                        name: "FK_PaymentReviews_Ledgers_ClassId",
                        column: x => x.ClassId,
                        principalSchema: "billing",
                        principalTable: "Ledgers",
                        principalColumn: "ClassId",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentReviews",
                schema: "billing");
        }
    }
}
