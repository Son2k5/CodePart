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

    public static Enrollment Create(Guid courseId, Guid studentId)
    {
        if (courseId == Guid.Empty) throw new ArgumentException("Course ID cannot be empty.", nameof(courseId));
        if (studentId == Guid.Empty) throw new ArgumentException("Student ID cannot be empty.", nameof(studentId));

        return new Enrollment
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            StudentId = studentId,
            Status = EnrollmentStatus.Active
        };
    }

    public void UpdateScores(decimal midtermScore, decimal finalScore, decimal midtermWeight = 0.4m)
    {
        if (midtermScore is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(midtermScore), "Midterm score must be between 0 and 10.");
        if (finalScore is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(finalScore), "Final score must be between 0 and 10.");
        if (midtermWeight is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(midtermWeight), "Weight must be between 0 and 1.");

        MidtermScore = midtermScore;
        FinalScore = finalScore;
        decimal finalWeight = 1.0m - midtermWeight;
        TotalScore = Math.Round((midtermScore * midtermWeight) + (finalScore * finalWeight), 2);
        IsPassed = TotalScore >= 4.0m;

        if (IsPassed == true)
        {
            Status = EnrollmentStatus.Completed;
        }

        Touch();
    }

    public void Drop()
    {
        if (Status == EnrollmentStatus.Completed)
            throw new InvalidOperationException("Cannot drop a completed course.");

        Status = EnrollmentStatus.Dropped;
        Touch();
    }

    public void Complete()
    {
        Status = EnrollmentStatus.Completed;
        Touch();
    }
}
