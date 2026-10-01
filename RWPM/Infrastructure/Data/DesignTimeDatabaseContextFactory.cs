using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RWPM.Infrastructure.Data;

// Migration commands must not execute Program's automatic migration/seeding path.
public class DesignTimeDatabaseContextFactory : IDesignTimeDbContextFactory<DefaultDatabaseContext>
{
    public DefaultDatabaseContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(basePath, "appsettings.json")) && Directory.Exists(Path.Combine(basePath, "RWPM")))
            basePath = Path.Combine(basePath, "RWPM");
        var config = new ConfigurationBuilder().SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddEnvironmentVariables().Build();
        var connection = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection before running migration commands.");
        return new DefaultDatabaseContext(new DbContextOptionsBuilder<DefaultDatabaseContext>().UseSqlServer(connection).Options);
    }
}
