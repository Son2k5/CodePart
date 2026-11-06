using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CodePath.Infrastructure.Persistence;

/// <summary>
/// Persists ASP.NET Core Data Protection keys in MySQL (ASPNETCore_DataProtection) so
/// stateless password reset tokens survive restarts and are shared across instances.
/// </summary>
public partial class AppDbContext : IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}
