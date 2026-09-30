using CodePath.Domain.Exercises.Entities;

namespace CodePath.Application.Exercises.Abstractions;

public interface IExerciseDbContext
{
    Task<Exercise?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<Exercise?> GetPublishedByIdAsync(Guid exerciseId, CancellationToken cancellationToken);
    Task AddSubmissionAsync(Submission submission, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
