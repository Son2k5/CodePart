using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeacherPortalAssignmentsAndGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GradedAt",
                table: "submissions",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GradedBy",
                table: "submissions",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherFeedback",
                table: "submissions",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "TeacherScore",
                table: "submissions",
                type: "decimal(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CourseId",
                table: "exercises",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercises_CourseId",
                table: "exercises",
                column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_exercises_courses_CourseId",
                table: "exercises",
                column: "CourseId",
                principalTable: "courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exercises_courses_CourseId",
                table: "exercises");

            migrationBuilder.DropIndex(
                name: "IX_exercises_CourseId",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "GradedAt",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "GradedBy",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "TeacherFeedback",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "TeacherScore",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "exercises");
        }
    }
}
