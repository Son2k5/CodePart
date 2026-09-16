using CodePath.Modules.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodePath.Modules.Users.Infrastructure.Persistence.Configurations;

public sealed class FacultyConfiguration : IEntityTypeConfiguration<Faculty>
{
    public void Configure(EntityTypeBuilder<Faculty> b)
    {
        b.ToTable("faculties", "users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Code).IsRequired().HasMaxLength(20);

        b.Property(x => x.CreatedAt).IsRequired().HasColumnType("timestamptz");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamptz");

        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_users_faculties_code");

        b.Ignore(x => x.DomainEvents);
    }
}