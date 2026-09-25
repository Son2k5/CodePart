using CodePath.Shared.Kernel.Persistence;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Nạp các biến môi trường từ file .env (tìm ngược lên các thư mục cha)
Env.TraversePath().Load();

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();

// Cấu hình Console Logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Đăng ký Infrastructure (chứa các DbContext & IDatabaseMigrator)
services.AddInfrastructure(configuration);

using var serviceProvider = services.BuildServiceProvider();
using var scope = serviceProvider.CreateScope();

var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
var migrators = scope.ServiceProvider.GetServices<IDatabaseMigrator>().ToList();

logger.LogInformation("==================================================");
logger.LogInformation("CodePath Database Migration Runner");
logger.LogInformation("Found {Count} module migrator(s) registered.", migrators.Count);
logger.LogInformation("==================================================");

var hasErrors = false;

foreach (var migrator in migrators)
{
    logger.LogInformation("[START] Migrating module: {ModuleName}...", migrator.ModuleName);
    try
    {
        await migrator.MigrateAsync();
        logger.LogInformation("[OK] Module {ModuleName} migration completed successfully.", migrator.ModuleName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[FAILED] Module {ModuleName} migration failed: {ErrorMessage}", migrator.ModuleName, ex.Message);
        hasErrors = true;
    }
}

logger.LogInformation("==================================================");

if (hasErrors)
{
    logger.LogError("Database migration process finished with ERRORS.");
    return 1;
}

logger.LogInformation("All module database migrations completed SUCCESSFULLY.");
return 0;

public partial class Program { }
