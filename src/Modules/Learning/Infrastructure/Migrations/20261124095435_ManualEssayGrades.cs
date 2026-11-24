using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ManualEssayGrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManualJson",
                schema: "learning",
                table: "ExamSeats",
                type: "text",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualJson",
                schema: "learning",
                table: "ExamSeats");
        }
    }
}
