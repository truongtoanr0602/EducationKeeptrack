using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Classroom.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SessionStatusAndMakeup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "classroom",
                table: "Sessions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacesSessionId",
                schema: "classroom",
                table: "Sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ReplacesSessionId",
                schema: "classroom",
                table: "Sessions",
                column: "ReplacesSessionId",
                unique: true,
                filter: "\"ReplacesSessionId\" IS NOT NULL AND \"Status\" <> 'cancelled'");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Sessions_ReplacesSessionId",
                schema: "classroom",
                table: "Sessions",
                column: "ReplacesSessionId",
                principalSchema: "classroom",
                principalTable: "Sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Sessions_ReplacesSessionId",
                schema: "classroom",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_ReplacesSessionId",
                schema: "classroom",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "classroom",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ReplacesSessionId",
                schema: "classroom",
                table: "Sessions");
        }
    }
}
