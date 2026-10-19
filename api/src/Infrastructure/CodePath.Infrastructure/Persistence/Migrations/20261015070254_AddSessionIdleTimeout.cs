using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionIdleTimeout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastActiveAt",
                table: "refresh_tokens",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<string>(
                name: "RevokedReason",
                table: "refresh_tokens",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "refresh_tokens",
                type: "binary(16)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Legacy tokens have no trustworthy idle history. Force one fresh login.
            migrationBuilder.Sql("""
                UPDATE refresh_tokens
                SET SessionId = FamilyId, LastActiveAt = CreatedAt,
                    RevokedReason = CASE WHEN RevokedAt IS NULL THEN 'SESSION_MIGRATION' ELSE RevokedReason END,
                    RevokedAt = COALESCE(RevokedAt, UTC_TIMESTAMP(6));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_SessionId_RevokedAt",
                table: "refresh_tokens",
                columns: new[] { "SessionId", "RevokedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_SessionId_RevokedAt",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "LastActiveAt",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "RevokedReason",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "refresh_tokens");
        }
    }
}
