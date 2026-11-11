using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LearningCurriculum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Curricula",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Grade = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Term = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Textbook = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MasteryThreshold = table.Column<int>(type: "integer", nullable: false),
                    MinTasks = table.Column<int>(type: "integer", nullable: false),
                    RequiredAttempts = table.Column<int>(type: "integer", nullable: false),
                    RuleVersion = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Curricula", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Strand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Criteria = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Archived = table.Column<bool>(type: "boolean", nullable: false),
                    TaughtOn = table.Column<DateOnly>(type: "date", nullable: true),
                    TaughtBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skills_Curricula_CurriculumId",
                        column: x => x.CurriculumId,
                        principalSchema: "learning",
                        principalTable: "Curricula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SkillEvents",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumVersion = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Detail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillEvents_Skills_SkillId",
                        column: x => x.SkillId,
                        principalSchema: "learning",
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Curricula_OwnerId_ClassId",
                schema: "learning",
                table: "Curricula",
                columns: new[] { "OwnerId", "ClassId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillEvents_SkillId_CurriculumVersion",
                schema: "learning",
                table: "SkillEvents",
                columns: new[] { "SkillId", "CurriculumVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_Skills_CurriculumId_Code",
                schema: "learning",
                table: "Skills",
                columns: new[] { "CurriculumId", "Code" },
                unique: true,
                filter: "\"Archived\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_OwnerId_ClassId",
                schema: "learning",
                table: "Skills",
                columns: new[] { "OwnerId", "ClassId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SkillEvents",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "Skills",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "Curricula",
                schema: "learning");
        }
    }
}
