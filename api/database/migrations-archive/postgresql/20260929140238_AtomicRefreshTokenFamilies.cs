using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AtomicRefreshTokenFamilies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AbsoluteExpiresAt",
                table: "refresh_tokens",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                table: "refresh_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentTokenId",
                table: "refresh_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE refresh_tokens SET \"FamilyId\" = \"Id\", \"AbsoluteExpiresAt\" = \"ExpiresAt\";");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AbsoluteExpiresAt",
                table: "refresh_tokens",
                type: "timestamptz",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamptz",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "FamilyId",
                table: "refresh_tokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_active",
                table: "refresh_tokens",
                columns: new[] { "UserId", "FamilyId" },
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_family",
                table: "refresh_tokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_ParentTokenId",
                table: "refresh_tokens",
                column: "ParentTokenId");

            migrationBuilder.AddForeignKey(
                name: "FK_refresh_tokens_refresh_tokens_ParentTokenId",
                table: "refresh_tokens",
                column: "ParentTokenId",
                principalTable: "refresh_tokens",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_refresh_tokens_refresh_tokens_ParentTokenId",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_active",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_family",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_ParentTokenId",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "AbsoluteExpiresAt",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "ParentTokenId",
                table: "refresh_tokens");
        }
    }
}
