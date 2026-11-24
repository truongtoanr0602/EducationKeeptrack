using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LessonsQuestionBankExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The mastery rule changes for results of online exams (ADR-0007): profiles cite the new rule version.
            migrationBuilder.Sql("UPDATE learning.\"Curricula\" SET \"RuleVersion\" = \"RuleVersion\" + 1;");

            migrationBuilder.CreateTable(
                name: "CurriculumLessons",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    No = table.Column<int>(type: "integer", nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Knowledge = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Outcomes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SkillsText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Competencies = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SkillIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    Archived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumLessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumLessons_Curricula_CurriculumId",
                        column: x => x.CurriculumId,
                        principalSchema: "learning",
                        principalTable: "Curricula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamBlueprints",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Counts = table.Column<List<int>>(type: "integer[]", nullable: false),
                    LessonFrom = table.Column<int>(type: "integer", nullable: true),
                    LessonTo = table.Column<int>(type: "integer", nullable: true),
                    SkillIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamBlueprints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Exams",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    ShowResults = table.Column<bool>(type: "boolean", nullable: false),
                    PublicToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Counts = table.Column<List<int>>(type: "integer[]", nullable: false),
                    LessonFrom = table.Column<int>(type: "integer", nullable: true),
                    LessonTo = table.Column<int>(type: "integer", nullable: true),
                    SkillIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LessonSessionLinks",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonSessionLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuestionImages",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ByteLength = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionImages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PrimarySkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecondarySkillIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    LessonNo = table.Column<int>(type: "integer", nullable: true),
                    Stem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StemImage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OptionsJson = table.Column<string>(type: "text", nullable: false),
                    AnswerJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExamItems",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    SeatId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PrimarySkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamItems_Exams_ExamId",
                        column: x => x.ExamId,
                        principalSchema: "learning",
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamSeats",
                schema: "learning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FailedTries = table.Column<int>(type: "integer", nullable: false),
                    Locked = table.Column<bool>(type: "boolean", nullable: false),
                    AttemptStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttemptNo = table.Column<int>(type: "integer", nullable: false),
                    Nonce = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AnswersJson = table.Column<string>(type: "text", nullable: false),
                    AnsweredCount = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamSeats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamSeats_Exams_ExamId",
                        column: x => x.ExamId,
                        principalSchema: "learning",
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumLessons_CurriculumId_No",
                schema: "learning",
                table: "CurriculumLessons",
                columns: new[] { "CurriculumId", "No" },
                unique: true,
                filter: "\"Archived\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumLessons_OwnerId_ClassId",
                schema: "learning",
                table: "CurriculumLessons",
                columns: new[] { "OwnerId", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamBlueprints_OwnerId_ClassId_Name",
                schema: "learning",
                table: "ExamBlueprints",
                columns: new[] { "OwnerId", "ClassId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamItems_ExamId_SeatId_Order",
                schema: "learning",
                table: "ExamItems",
                columns: new[] { "ExamId", "SeatId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_Exams_AssessmentId",
                schema: "learning",
                table: "Exams",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_OwnerId_ClassId",
                schema: "learning",
                table: "Exams",
                columns: new[] { "OwnerId", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_Exams_PublicToken",
                schema: "learning",
                table: "Exams",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamSeats_ExamId_StudentId",
                schema: "learning",
                table: "ExamSeats",
                columns: new[] { "ExamId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonSessionLinks_LessonId",
                schema: "learning",
                table: "LessonSessionLinks",
                column: "LessonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonSessionLinks_OwnerId_ClassId",
                schema: "learning",
                table: "LessonSessionLinks",
                columns: new[] { "OwnerId", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionImages_OwnerId_ClassId_NormalizedName",
                schema: "learning",
                table: "QuestionImages",
                columns: new[] { "OwnerId", "ClassId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_OwnerId_ClassId_Code",
                schema: "learning",
                table: "Questions",
                columns: new[] { "OwnerId", "ClassId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE learning.\"Curricula\" SET \"RuleVersion\" = \"RuleVersion\" - 1;");

            migrationBuilder.DropTable(
                name: "CurriculumLessons",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "ExamBlueprints",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "ExamItems",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "ExamSeats",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "LessonSessionLinks",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "QuestionImages",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "Questions",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "Exams",
                schema: "learning");
        }
    }
}
