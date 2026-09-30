using CodePath.Domain.Exercises.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Infrastructure.Persistence.Configurations;

public sealed class SubmissionTestResultConfiguration : IEntityTypeConfiguration<SubmissionTestResult>
{
    public void Configure(EntityTypeBuilder<SubmissionTestResult> builder)
    {
        builder.ToTable("submission_test_results");
        builder.HasKey(result => result.Id);
        builder.Property(result => result.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(result => result.ActualOutput).HasColumnType("text");
        builder.Property(result => result.ErrorMessage).HasColumnType("text");
        builder.HasIndex(result => new { result.SubmissionId, result.TestCaseId }).IsUnique()
            .HasDatabaseName("ux_submission_test_results_case");
        builder.HasOne(result => result.Submission).WithMany(submission => submission.TestResults)
            .HasForeignKey(result => result.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(result => result.TestCase).WithMany()
            .HasForeignKey(result => result.TestCaseId).OnDelete(DeleteBehavior.Restrict);
    }
}
