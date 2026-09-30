using CodePath.Domain.Common.Exceptions;
using CodePath.Shared.Kernel.Entities;
using CodePath.Domain.Users.Enums;

namespace CodePath.Domain.Users.Entities;

public sealed class Enrollment : BaseEntity
{
    public Guid CourseId { get; private set; }
    public Course Course { get; private set; } = default!;

    public Guid StudentId { get; private set; }
    public User Student { get; private set; } = default!;

    public EnrollmentStatus Status { get; private set; } = EnrollmentStatus.Active;

    public decimal? MidtermScore { get; private set; }
    public decimal? FinalScore { get; private set; }
    public decimal? TotalScore { get; private set; }
    public bool? IsPassed { get; private set; }

    private Enrollment() { }

    public static Enrollment Create(Guid courseId, Guid studentId, DateTime utcNow)
    {
        if (courseId == Guid.Empty)
            throw new DomainValidationException("Course ID cannot be empty.", nameof(courseId));
        if (studentId == Guid.Empty)
            throw new DomainValidationException("Student ID cannot be empty.", nameof(studentId));

        EnsureUtc(utcNow);
        return new Enrollment
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            StudentId = studentId,
            Status = EnrollmentStatus.Active,
            CreatedAt = utcNow
        };
    }

    public void UpdateScores(decimal midtermScore, decimal finalScore, DateTime utcNow, decimal midtermWeight = 0.4m)
    {
        if (midtermScore is < 0 or > 10)
            throw new DomainValidationException("Midterm score must be between 0 and 10.", nameof(midtermScore));
        if (finalScore is < 0 or > 10)
            throw new DomainValidationException("Final score must be between 0 and 10.", nameof(finalScore));
        if (midtermWeight is <= 0 or >= 1)
            throw new DomainValidationException("Weight must be between 0 and 1.", nameof(midtermWeight));

        MidtermScore = midtermScore;
        FinalScore = finalScore;
        decimal finalWeight = 1.0m - midtermWeight;
        TotalScore = Math.Round((midtermScore * midtermWeight) + (finalScore * finalWeight), 2);
        IsPassed = TotalScore >= 4.0m;

        if (IsPassed == true)
        {
            Status = EnrollmentStatus.Completed;
        }

        Touch(utcNow);
    }

    public void Drop(DateTime utcNow)
    {
        if (Status == EnrollmentStatus.Completed)
            throw new DomainRuleViolationException("Cannot drop a completed course.");

        Status = EnrollmentStatus.Dropped;
        Touch(utcNow);
    }

    public void Complete(DateTime utcNow)
    {
        Status = EnrollmentStatus.Completed;
        Touch(utcNow);
    }
}
