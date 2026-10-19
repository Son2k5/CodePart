using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class ClassTeacherConfiguration : IEntityTypeConfiguration<ClassTeacher>
{
    public void Configure(EntityTypeBuilder<ClassTeacher> b)
    {
        b.ToTable("class_teachers"); b.HasKey(x => new { x.ClassId, x.TeacherId });
        b.HasQueryFilter(x => !x.Class.IsDeleted);
        b.HasOne(x => x.Class).WithMany(x => x.Teachers).HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ClassEnrollmentConfiguration : IEntityTypeConfiguration<ClassEnrollment>
{
    public void Configure(EntityTypeBuilder<ClassEnrollment> b)
    {
        b.ToTable("class_enrollments"); b.HasKey(x => new { x.ClassId, x.StudentId });
        b.HasQueryFilter(x => !x.Class.IsDeleted);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.JoinMethod).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.ClassId, x.Status });
        b.HasOne(x => x.Class).WithMany(x => x.ClassEnrollments).HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
