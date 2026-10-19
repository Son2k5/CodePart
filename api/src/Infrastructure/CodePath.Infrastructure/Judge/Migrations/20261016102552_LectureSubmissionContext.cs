using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePath.Infrastructure.Judge.Migrations
{
    /// <inheritdoc />
    public partial class LectureSubmissionContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LearningProcessedAt",
                table: "judge_submissions",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TopicId",
                table: "judge_submissions",
                type: "binary(16)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LearningProcessedAt",
                table: "judge_submissions");

            migrationBuilder.DropColumn(
                name: "TopicId",
                table: "judge_submissions");
        }
    }
}
