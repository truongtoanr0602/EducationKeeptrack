using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SubmissionPhotosAndRubric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Rubric",
                schema: "learning",
                table: "Assessments",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RubricVersion",
                schema: "learning",
                table: "Assessments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "AiCompletion",
                schema: "learning",
                table: "Submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiCorrectness",
                schema: "learning",
                table: "Submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AiConfidence",
                schema: "learning",
                table: "Submissions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiReasoning",
                schema: "learning",
                table: "Submissions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiWarning",
                schema: "learning",
                table: "Submissions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiStatus",
                schema: "learning",
                table: "Submissions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AiProposedAt",
                schema: "learning",
                table: "Submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubmissionPhotos",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SequenceIndex = table.Column<int>(type: "integer", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ByteLength = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionPhotos_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalSchema: "learning",
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionPhotos_SubmissionId_SequenceIndex",
                schema: "learning",
                table: "SubmissionPhotos",
                columns: new[] { "SubmissionId", "SequenceIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubmissionPhotos",
                schema: "learning");

            migrationBuilder.DropColumn(
                name: "Rubric",
                schema: "learning",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "RubricVersion",
                schema: "learning",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "AiCompletion",
                schema: "learning",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AiCorrectness",
                schema: "learning",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AiConfidence",
                schema: "learning",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AiReasoning",
                schema: "learning",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AiWarning",
                schema: "learning",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AiStatus",
                schema: "learning",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AiProposedAt",
                schema: "learning",
                table: "Submissions");
        }
    }
}
