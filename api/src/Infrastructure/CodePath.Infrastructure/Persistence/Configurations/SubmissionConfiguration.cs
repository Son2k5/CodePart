using CodePath.Domain.Exercises.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("submissions");
        builder.HasKey(submission => submission.Id);
        builder.Property(submission => submission.Language).HasMaxLength(32).IsRequired();
        builder.Property(submission => submission.SourceCode).HasColumnType("text").IsRequired();
        builder.Property(submission => submission.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(submission => submission.ErrorMessage).HasColumnType("text");
        builder.HasIndex(submission => new { submission.StudentId, submission.ExerciseId, submission.CreatedAt })
            .HasDatabaseName("ix_submissions_student_exercise_created");
        builder.HasOne(submission => submission.Exercise).WithMany(exercise => exercise.Submissions)
            .HasForeignKey(submission => submission.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CodePath.Domain.Users.Entities.User>().WithMany()
            .HasForeignKey(submission => submission.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
