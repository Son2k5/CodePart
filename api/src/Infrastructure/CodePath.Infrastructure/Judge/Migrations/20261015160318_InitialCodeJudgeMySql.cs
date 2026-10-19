using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePath.Infrastructure.Judge.Migrations
{
    /// <inheritdoc />
    public partial class InitialCodeJudgeMySql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "judge_awards",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    ProblemId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_judge_awards", x => new { x.UserId, x.ProblemId });
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "judge_failure_outbox",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    TestCaseId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Verdict = table.Column<int>(type: "int", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_judge_failure_outbox", x => x.SubmissionId);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "judge_problems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Title = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Difficulty = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TimeLimitMs = table.Column<int>(type: "int", nullable: false),
                    MemoryLimitMb = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    ComparisonMode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<Guid>(type: "binary(16)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "binary(16)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_judge_problems", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "judge_submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    UserId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    ProblemId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    CourseId = table.Column<Guid>(type: "binary(16)", nullable: true),
                    Language = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceCode = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PassedCount = table.Column<int>(type: "int", nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    AwardedPoints = table.Column<int>(type: "int", nullable: false),
                    MaxRuntimeMs = table.Column<int>(type: "int", nullable: false),
                    MaxMemoryKb = table.Column<int>(type: "int", nullable: false),
                    CompileOutput = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "binary(16)", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Snapshot = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActiveSlot = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN `Status` IN (7, 8) THEN 1 ELSE NULL END", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_judge_submissions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "judge_starters",
                columns: table => new
                {
                    ProblemId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Language = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Template = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_judge_starters", x => new { x.ProblemId, x.Language });
                    table.ForeignKey(
                        name: "FK_judge_starters_judge_problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "judge_problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "judge_tests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    ProblemId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Input = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpectedOutput = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSample = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_judge_tests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_judge_tests_judge_problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "judge_problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "judge_results",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    TestCaseId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    IsSample = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Input = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpectedOutput = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Verdict = table.Column<int>(type: "int", nullable: false),
                    RuntimeMs = table.Column<int>(type: "int", nullable: false),
                    MemoryKb = table.Column<int>(type: "int", nullable: false),
                    ActualOutput = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "longtext", nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_judge_results", x => new { x.SubmissionId, x.TestCaseId });
                    table.ForeignKey(
                        name: "FK_judge_results_judge_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "judge_submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "IX_judge_submissions_Status_CreatedAt",
                table: "judge_submissions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_judge_submissions_UserId_ProblemId_ActiveSlot",
                table: "judge_submissions",
                columns: new[] { "UserId", "ProblemId", "ActiveSlot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_judge_tests_ProblemId_Order",
                table: "judge_tests",
                columns: new[] { "ProblemId", "Order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "judge_awards");

            migrationBuilder.DropTable(
                name: "judge_failure_outbox");

            migrationBuilder.DropTable(
                name: "judge_results");

            migrationBuilder.DropTable(
                name: "judge_starters");

            migrationBuilder.DropTable(
                name: "judge_tests");

            migrationBuilder.DropTable(
                name: "judge_submissions");

            migrationBuilder.DropTable(
                name: "judge_problems");
        }
    }
}
