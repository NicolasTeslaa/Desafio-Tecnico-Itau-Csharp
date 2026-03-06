using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScheduledPurchaseEngineService.Data;

namespace ScheduledPurchaseEngineService.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public void DbContextFactory_CreatesContext_FromAppSettings()
    {
        var tempDir = CreateTempConfigDirectory();
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDir);

            var factory = new DbContextFactory();
            using var db = factory.CreateDbContext([]);

            Assert.NotNull(db);
            Assert.NotNull(db.Database.ProviderName);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void AddScheduledPurchaseDbContext_RegistersDbContext_ForSupportedProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "MySql",
                ["Database:ConnectionStringName"] = "mysql",
                ["ConnectionStrings:mysql"] = "Server=localhost;Database=test;Uid=root;Pwd=root;"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddScheduledPurchaseDbContext(configuration);

        Assert.Contains(services, x => x.ServiceType == typeof(DbContextOptions<ScheduledPurchaseDbContext>));
    }

    [Fact]
    public void DbContextFactory_Throws_ForUnsupportedProvider()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"scheduled-factory-invalid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "Database": {
                "Provider": "SqlServer",
                "ConnectionStringName": "mysql"
              },
              "ConnectionStrings": {
                "mysql": "Server=localhost;Database=test;Uid=root;Pwd=root;"
              }
            }
            """);

        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);
            var factory = new DbContextFactory();

            Assert.Throws<NotSupportedException>(() => factory.CreateDbContext([]));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void AddScheduledPurchaseDbContext_Throws_ForMissingConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "MySql",
                ["Database:ConnectionStringName"] = "mysql"
            })
            .Build();

        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddScheduledPurchaseDbContext(configuration));
    }

    private static string CreateTempConfigDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"scheduled-factory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "Database": {
                "Provider": "MySql",
                "ConnectionStringName": "mysql"
              },
              "ConnectionStrings": {
                "mysql": "Server=localhost;Database=test;Uid=root;Pwd=root;"
              }
            }
            """);
        return tempDir;
    }
}
