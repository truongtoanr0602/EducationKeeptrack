using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersonalizedLearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClassLearningProfiles",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    Textbook = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Semester = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SkillsJson = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassLearningProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SkillEvidence",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Total = table.Column<int>(type: "integer", nullable: false),
                    Correct = table.Column<int>(type: "integer", nullable: false),
                    Confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillEvidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SkillObservations",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillObservations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassLearningProfiles_OwnerId_ClassId",
                schema: "learning",
                table: "ClassLearningProfiles",
                columns: new[] { "OwnerId", "ClassId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillEvidence_OwnerId_ClassId_RequestId",
                schema: "learning",
                table: "SkillEvidence",
                columns: new[] { "OwnerId", "ClassId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillEvidence_OwnerId_ClassId_StudentId_SkillId_Date",
                schema: "learning",
                table: "SkillEvidence",
                columns: new[] { "OwnerId", "ClassId", "StudentId", "SkillId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_SkillObservations_OwnerId_ClassId_RequestId",
                schema: "learning",
                table: "SkillObservations",
                columns: new[] { "OwnerId", "ClassId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillObservations_OwnerId_ClassId_StudentId_SkillId_Date",
                schema: "learning",
                table: "SkillObservations",
                columns: new[] { "OwnerId", "ClassId", "StudentId", "SkillId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassLearningProfiles",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "SkillEvidence",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "SkillObservations",
                schema: "learning");
        }
    }
}
