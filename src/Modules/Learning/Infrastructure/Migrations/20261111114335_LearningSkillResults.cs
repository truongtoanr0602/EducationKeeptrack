using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LearningSkillResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssessmentSkillSets",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentSkillSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentSkills",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentSkills_AssessmentSkillSets_SetId",
                        column: x => x.SetId,
                        principalSchema: "learning",
                        principalTable: "AssessmentSkillSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SkillResults",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Correct = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillResults_AssessmentSkillSets_SetId",
                        column: x => x.SetId,
                        principalSchema: "learning",
                        principalTable: "AssessmentSkillSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SkillRevisions",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TaskCount = table.Column<int>(type: "integer", nullable: false),
                    Correct = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillRevisions_SkillResults_ResultId",
                        column: x => x.ResultId,
                        principalSchema: "learning",
                        principalTable: "SkillResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentSkills_SetId_SkillId",
                schema: "learning",
                table: "AssessmentSkills",
                columns: new[] { "SetId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentSkillSets_AssessmentId",
                schema: "learning",
                table: "AssessmentSkillSets",
                column: "AssessmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentSkillSets_OwnerId_ClassId",
                schema: "learning",
                table: "AssessmentSkillSets",
                columns: new[] { "OwnerId", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_SkillResults_OwnerId_ClassId_StudentId",
                schema: "learning",
                table: "SkillResults",
                columns: new[] { "OwnerId", "ClassId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_SkillResults_SetId_StudentId_SkillId",
                schema: "learning",
                table: "SkillResults",
                columns: new[] { "SetId", "StudentId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillRevisions_ResultId_Version",
                schema: "learning",
                table: "SkillRevisions",
                columns: new[] { "ResultId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentSkills",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "SkillRevisions",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "SkillResults",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "AssessmentSkillSets",
                schema: "learning");
        }
    }
}
