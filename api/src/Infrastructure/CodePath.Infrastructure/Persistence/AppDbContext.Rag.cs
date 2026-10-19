using CodePath.Domain.Learning;
using Microsoft.EntityFrameworkCore;
namespace CodePath.Infrastructure.Persistence;

public partial class AppDbContext
{
    public DbSet<RagIndexJob> RagIndexJobs => Set<RagIndexJob>();
    private void EnqueueRagChanges()
    {
        ChangeTracker.DetectChanges();
        var changed = ChangeTracker.Entries<Topic>().Where(e =>
            e.State is EntityState.Added or EntityState.Modified && e.Entity.PublishedVersion > 0).ToArray();
        foreach (var entry in changed)
        {
            var topic = entry.Entity;
            if (!RagIndexJobs.Local.Any(j => j.TopicId == topic.Id && j.Revision == topic.RowVersion))
                RagIndexJobs.Add(new() { TopicId = topic.Id, Revision = topic.RowVersion });
        }
    }
}
