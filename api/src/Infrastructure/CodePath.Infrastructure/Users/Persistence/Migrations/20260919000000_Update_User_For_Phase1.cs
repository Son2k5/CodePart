using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePath.Infrastructure.Users.Persistence.Migrations;

[DbContext(typeof(UsersDbContext))]
[Migration("20260919000000_Update_User_For_Phase1")]
public partial class Update_User_For_Phase1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Status",
            schema: "users",
            table: "users",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "Active");

        migrationBuilder.Sql(@"
            ALTER TABLE users.users 
            ALTER COLUMN ""Role"" TYPE character varying(50) 
            USING (
                CASE ""Role""::integer
                    WHEN 0 THEN 'Student'
                    WHEN 1 THEN 'Teacher'
                    WHEN 2 THEN 'Admin'
                    ELSE 'Student'
                END
            );
        ");

        migrationBuilder.RenameColumn(
            name: "StudentCode",
            schema: "users",
            table: "users",
            newName: "StudentId");

        migrationBuilder.DropColumn(
            name: "FailedLoginCount",
            schema: "users",
            table: "users");

        migrationBuilder.DropColumn(
            name: "LockoutEnd",
            schema: "users",
            table: "users");

        migrationBuilder.AddColumn<string>(
            name: "CreatedBy",
            schema: "users",
            table: "users",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UpdatedBy",
            schema: "users",
            table: "users",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_users_status",
            schema: "users",
            table: "users",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "ux_users_student_id",
            schema: "users",
            table: "users",
            column: "StudentId",
            unique: true,
            filter: "\"StudentId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "ix_users_status", schema: "users", table: "users");
        migrationBuilder.DropIndex(name: "ux_users_student_id", schema: "users", table: "users");
        migrationBuilder.DropColumn(name: "Status", schema: "users", table: "users");
        migrationBuilder.DropColumn(name: "CreatedBy", schema: "users", table: "users");
        migrationBuilder.DropColumn(name: "UpdatedBy", schema: "users", table: "users");
        migrationBuilder.RenameColumn(name: "StudentId", schema: "users", table: "users", newName: "StudentCode");

        migrationBuilder.Sql(@"
            ALTER TABLE users.users 
            ALTER COLUMN ""Role"" TYPE integer 
            USING (
                CASE ""Role""
                    WHEN 'Student' THEN 0
                    WHEN 'Teacher' THEN 1
                    WHEN 'Admin' THEN 2
                    ELSE 0
                END
            );
        ");

        migrationBuilder.AddColumn<int>(
            name: "FailedLoginCount",
            schema: "users",
            table: "users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LockoutEnd",
            schema: "users",
            table: "users",
            type: "timestamptz",
            nullable: true);
    }
}

