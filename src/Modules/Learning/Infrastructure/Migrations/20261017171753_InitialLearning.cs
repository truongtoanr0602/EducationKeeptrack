using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialLearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "learning");

            migrationBuilder.CreateTable(
                name: "Assessments",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DefaultMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Completion = table.Column<int>(type: "integer", nullable: true),
                    Correctness = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Submissions_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalSchema: "learning",
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultRevision",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Completion = table.Column<int>(type: "integer", nullable: true),
                    Correctness = table.Column<int>(type: "integer", nullable: true),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultRevision", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultRevision_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalSchema: "learning",
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_OwnerId_ClassId",
                schema: "learning",
                table: "Assessments",
                columns: new[] { "OwnerId", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultRevision_DispatchedAt",
                schema: "learning",
                table: "ResultRevision",
                column: "DispatchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResultRevision_SubmissionId_Version",
                schema: "learning",
                table: "ResultRevision",
                columns: new[] { "SubmissionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_AssessmentId_StudentId",
                schema: "learning",
                table: "Submissions",
                columns: new[] { "AssessmentId", "StudentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResultRevision",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "Submissions",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "Assessments",
                schema: "learning");
        }
    }
}
