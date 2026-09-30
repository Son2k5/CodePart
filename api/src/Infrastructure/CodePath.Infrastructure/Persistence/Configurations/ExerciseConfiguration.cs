using CodePath.Domain.Exercises.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("exercises");
        builder.HasKey(exercise => exercise.Id);
        builder.Property(exercise => exercise.Slug).HasMaxLength(160).IsRequired();
        builder.Property(exercise => exercise.Title).HasMaxLength(250).IsRequired();
        builder.Property(exercise => exercise.Difficulty).HasMaxLength(30).IsRequired();
        builder.Property(exercise => exercise.Description).HasColumnType("text").IsRequired();
        builder.Property(exercise => exercise.Constraints).HasColumnType("text").IsRequired();
        builder.Property(exercise => exercise.StarterCode).HasColumnType("text").IsRequired();
        builder.HasIndex(exercise => exercise.Slug).IsUnique().HasDatabaseName("ux_exercises_slug");
        builder.HasIndex(exercise => exercise.IsPublished).HasDatabaseName("ix_exercises_published");
    }
}
