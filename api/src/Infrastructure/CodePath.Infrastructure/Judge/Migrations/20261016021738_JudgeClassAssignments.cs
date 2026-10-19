using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePath.Infrastructure.Judge.Migrations
{
    /// <inheritdoc />
    public partial class JudgeClassAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClassId",
                table: "judge_submissions",
                type: "binary(16)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClassId",
                table: "judge_problems",
                type: "binary(16)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "judge_submissions");

            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "judge_problems");
        }
    }
}
