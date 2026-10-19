using CodePath.Domain.Users.Entities;
using CodePath.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.MySql;
using Xunit;

namespace CodePath.Integration.Tests.Classes;

public class ClassMigrationSpecifications
{
    [Fact]
    public async Task Upgrade_preserves_legacy_teacher_and_students_without_publishing_classes()
    {
        await using var mysql = new MySqlBuilder("mysql:8.4").WithDatabase("class_migration_tests")
            .WithUsername("codepath").WithPassword("migration-tests-only").Build();
        await mysql.StartAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseMySql(
            mysql.GetConnectionString() + ";GuidFormat=Binary16;DateTimeKind=Utc",
            new MySqlServerVersion(new Version(8, 4, 0))).Options;
        await using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261015070254_AddSessionIdleTimeout");
        var now = DateTime.UtcNow; var classId = Guid.NewGuid();
        var teacher = User.CreateTeacher("Legacy teacher", "legacy-teacher@hanu.edu.vn", "hash", now);
        var student = User.CreateStudent("Legacy student", "legacy-student@hanu.edu.vn", "hash", "legacy-01", now);
        var faculty = Faculty.Create("Legacy faculty", "LEGACY", now);
        db.Users.AddRange(teacher, student); db.Faculties.Add(faculty); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO classes
            (Id, Name, AcademicYear, FacultyId, TeacherId, CreatedAt)
            VALUES ({classId}, {"Legacy class"}, {"2026"}, {faculty.Id}, {teacher.Id}, {now})");
        student.AssignToClass(classId, now); await db.SaveChangesAsync();
        await migrator.MigrateAsync(); db.ChangeTracker.Clear();
        var c = await db.Classes.Include(x => x.Teachers).Include(x => x.ClassEnrollments).SingleAsync();
        c.Status.Should().Be(ClassStatus.Draft); c.AccessType.Should().Be(ClassAccessType.Public);
        c.RowVersion.Should().NotBeEmpty(); c.JoinCode.Should().BeNull();
        c.Teachers.Should().ContainSingle(x => x.TeacherId == teacher.Id);
        c.ClassEnrollments.Should().ContainSingle(x => x.StudentId == student.Id && x.Status == ClassEnrollmentStatus.Active);
    }
}
