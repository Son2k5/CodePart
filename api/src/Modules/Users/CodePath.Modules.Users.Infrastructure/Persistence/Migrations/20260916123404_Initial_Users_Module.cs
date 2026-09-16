using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePath.Modules.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Users_Module : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "users");

            migrationBuilder.CreateTable(
                name: "faculties",
                schema: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faculties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "classes",
                schema: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AcademicYear = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FacultyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_classes_faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalSchema: "users",
                        principalTable: "faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EmailVerifiedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LockoutEnd = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    StudentCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: true),
                    FacultyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_classes_ClassId",
                        column: x => x.ClassId,
                        principalSchema: "users",
                        principalTable: "classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_users_faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalSchema: "users",
                        principalTable: "faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "courses",
                schema: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SectionCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Semester = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    JoinCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MaxCapacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_courses_users_TeacherId",
                        column: x => x.TeacherId,
                        principalSchema: "users",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                schema: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MidtermScore = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    FinalScore = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    TotalScore = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    IsPassed = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_enrollments_courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "users",
                        principalTable: "courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_enrollments_users_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "users",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_classes_faculty_id",
                schema: "users",
                table: "classes",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "ix_users_classes_teacher_id",
                schema: "users",
                table: "classes",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "ux_users_classes_name_year_faculty",
                schema: "users",
                table: "classes",
                columns: new[] { "Name", "AcademicYear", "FacultyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_courses_teacher_id",
                schema: "users",
                table: "courses",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "ux_users_courses_join_code",
                schema: "users",
                table: "courses",
                column: "JoinCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_enrollments_student_id",
                schema: "users",
                table: "enrollments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "ux_users_enrollments_course_student",
                schema: "users",
                table: "enrollments",
                columns: new[] { "CourseId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_faculties_code",
                schema: "users",
                table: "faculties",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_class_id",
                schema: "users",
                table: "users",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "ix_users_faculty_id",
                schema: "users",
                table: "users",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "ix_users_role",
                schema: "users",
                table: "users",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                schema: "users",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_student_code",
                schema: "users",
                table: "users",
                column: "StudentCode",
                unique: true,
                filter: "\"StudentCode\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_classes_users_TeacherId",
                schema: "users",
                table: "classes",
                column: "TeacherId",
                principalSchema: "users",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_classes_faculties_FacultyId",
                schema: "users",
                table: "classes");

            migrationBuilder.DropForeignKey(
                name: "FK_users_faculties_FacultyId",
                schema: "users",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_classes_users_TeacherId",
                schema: "users",
                table: "classes");

            migrationBuilder.DropTable(
                name: "enrollments",
                schema: "users");

            migrationBuilder.DropTable(
                name: "courses",
                schema: "users");

            migrationBuilder.DropTable(
                name: "faculties",
                schema: "users");

            migrationBuilder.DropTable(
                name: "users",
                schema: "users");

            migrationBuilder.DropTable(
                name: "classes",
                schema: "users");
        }
    }
}
