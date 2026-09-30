using CodePath.Domain.Exercises.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class ExerciseTestCaseConfiguration : IEntityTypeConfiguration<ExerciseTestCase>
{
    public void Configure(EntityTypeBuilder<ExerciseTestCase> builder)
    {
        builder.ToTable("exercise_test_cases");
        builder.HasKey(test => test.Id);
        builder.Property(test => test.Input).HasColumnType("text").IsRequired();
        builder.Property(test => test.ExpectedOutput).HasColumnType("text").IsRequired();
        builder.Property(test => test.Explanation).HasColumnType("text");
        builder.HasIndex(test => new { test.ExerciseId, test.Order }).IsUnique()
            .HasDatabaseName("ux_exercise_test_cases_order");
        builder.HasOne(test => test.Exercise).WithMany(exercise => exercise.TestCases)
            .HasForeignKey(test => test.ExerciseId).OnDelete(DeleteBehavior.Cascade);
    }
}
