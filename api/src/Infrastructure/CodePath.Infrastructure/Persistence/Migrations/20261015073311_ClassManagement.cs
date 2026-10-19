using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClassManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "TeacherId",
                table: "classes",
                type: "binary(16)",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "binary(16)");

            migrationBuilder.AlterColumn<Guid>(
                name: "FacultyId",
                table: "classes",
                type: "binary(16)",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "binary(16)");

            migrationBuilder.AddColumn<string>(
                name: "AccessType",
                table: "classes",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Public",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "classes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "classes",
                type: "varchar(5000)",
                maxLength: 5000,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "classes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "classes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "JoinCode",
                table: "classes",
                type: "varchar(8)",
                maxLength: 8,
                nullable: true,
                collation: "ascii_bin")
                .Annotation("MySql:CharSet", "ascii");

            migrationBuilder.AddColumn<DateTime>(
                name: "JoinCodeExpiresAt",
                table: "classes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxStudents",
                table: "classes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "classes",
                type: "binary(16)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "classes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "classes",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft",
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Thumbnail",
                table: "classes",
                type: "varchar(2048)",
                maxLength: 2048,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "class_enrollments",
                columns: table => new
                {
                    ClassId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    StudentId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    JoinMethod = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_enrollments", x => new { x.ClassId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_class_enrollments_classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_enrollments_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "class_teachers",
                columns: table => new
                {
                    ClassId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    TeacherId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "binary(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_teachers", x => new { x.ClassId, x.TeacherId });
                    table.ForeignKey(
                        name: "FK_class_teachers_classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_teachers_users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_teachers_users_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "IX_classes_Name",
                table: "classes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_classes_Status_AccessType",
                table: "classes",
                columns: new[] { "Status", "AccessType" });

            migrationBuilder.CreateIndex(
                name: "ux_classes_join_code",
                table: "classes",
                column: "JoinCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_class_enrollments_ClassId_Status",
                table: "class_enrollments",
                columns: new[] { "ClassId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_class_enrollments_StudentId",
                table: "class_enrollments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_class_teachers_AssignedBy",
                table: "class_teachers",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_class_teachers_TeacherId",
                table: "class_teachers",
                column: "TeacherId");
            // Preserve existing assignments and memberships. Existing classes stay private
            // (Draft) until an administrator explicitly publishes them.
            migrationBuilder.Sql("UPDATE classes SET RowVersion = UUID_TO_BIN(UUID())");
            migrationBuilder.Sql(@"INSERT INTO class_teachers (ClassId, TeacherId, AssignedAt, AssignedBy)
                SELECT c.Id, c.TeacherId, c.CreatedAt, c.TeacherId FROM classes c
                JOIN users u ON u.Id = c.TeacherId WHERE u.Role = 'Teacher'");
            migrationBuilder.Sql(@"INSERT INTO class_enrollments (ClassId, StudentId, JoinedAt, JoinMethod, Status)
                SELECT u.ClassId, u.Id, u.CreatedAt, 'AdminAdded', 'Active' FROM users u
                WHERE u.ClassId IS NOT NULL AND u.Role = 'Student'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A rollback cannot represent teacherless/facultyless classes in the old schema.
            // Fail before any DDL instead of replacing missing FKs with Guid.Empty.
            migrationBuilder.Sql(@"CREATE TEMPORARY TABLE class_rollback_guard (valid INT NOT NULL CHECK (valid = 1));
                INSERT INTO class_rollback_guard SELECT 0 FROM classes WHERE TeacherId IS NULL OR FacultyId IS NULL LIMIT 1;
                DROP TEMPORARY TABLE class_rollback_guard;");
            migrationBuilder.DropTable(
                name: "class_enrollments");

            migrationBuilder.DropTable(
                name: "class_teachers");

            migrationBuilder.DropIndex(
                name: "IX_classes_Name",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "IX_classes_Status_AccessType",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "ux_classes_join_code",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "AccessType",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "JoinCode",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "JoinCodeExpiresAt",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "MaxStudents",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "Thumbnail",
                table: "classes");

            migrationBuilder.AlterColumn<Guid>(
                name: "TeacherId",
                table: "classes",
                type: "binary(16)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "binary(16)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "FacultyId",
                table: "classes",
                type: "binary(16)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "binary(16)",
                oldNullable: true);
        }
    }
}
