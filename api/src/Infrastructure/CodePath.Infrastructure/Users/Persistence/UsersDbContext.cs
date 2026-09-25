using CodePath.Application.Users.Abstractions;
using CodePath.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Infrastructure.Users.Persistence;

public sealed class UsersDbContext : DbContext, IUsersDbContext
{
    public const string Schema = "users";

    public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.HasDefaultSchema(Schema);
        m.ApplyConfigurationsFromAssembly(typeof(UsersDbContext).Assembly, type => 
            type.Namespace?.StartsWith("CodePath.Infrastructure.Users.Persistence.Configurations") == true);
        base.OnModelCreating(m);
    }
}
