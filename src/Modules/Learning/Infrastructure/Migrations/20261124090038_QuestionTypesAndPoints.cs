using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QuestionTypesAndPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioFile",
                schema: "learning",
                table: "Questions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FillMode",
                schema: "learning",
                table: "Questions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "chips");

            migrationBuilder.AddColumn<bool>(
                name: "IgnoreDiacritics",
                schema: "learning",
                table: "Questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Points",
                schema: "learning",
                table: "Questions",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "SampleAnswer",
                schema: "learning",
                table: "Questions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StemHtml",
                schema: "learning",
                table: "Questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingEssays",
                schema: "learning",
                table: "ExamSeats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Score",
                schema: "learning",
                table: "ExamSeats",
                type: "numeric(7,2)",
                nullable: false,
                defaultValue: 0m);

            // Attempts made before points existed earned one point for every question they got right.
            migrationBuilder.Sql("UPDATE learning.\"ExamSeats\" SET \"Score\" = \"CorrectCount\" WHERE \"CorrectCount\" > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioFile",
                schema: "learning",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "FillMode",
                schema: "learning",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "IgnoreDiacritics",
                schema: "learning",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Points",
                schema: "learning",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "SampleAnswer",
                schema: "learning",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "StemHtml",
                schema: "learning",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "PendingEssays",
                schema: "learning",
                table: "ExamSeats");

            migrationBuilder.DropColumn(
                name: "Score",
                schema: "learning",
                table: "ExamSeats");
        }
    }
}
