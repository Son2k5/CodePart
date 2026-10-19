using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LearningRagOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learning_rag_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    TopicId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Revision = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Status = table.Column<string>(type: "varchar(24)", maxLength: 24, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    ChunkCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LeaseUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "binary(16)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_rag_jobs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "IX_learning_rag_jobs_Status_NextAttemptAt",
                table: "learning_rag_jobs",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_rag_jobs_TopicId_Revision",
                table: "learning_rag_jobs",
                columns: new[] { "TopicId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "learning_rag_jobs");
        }
    }
}
