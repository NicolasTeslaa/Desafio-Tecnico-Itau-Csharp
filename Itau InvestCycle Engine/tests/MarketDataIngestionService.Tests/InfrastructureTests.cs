using System.Reflection;
using MarketDataIngestionService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketDataIngestionService.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public void MarketDataDbContextFactory_CreatesContext_FromAppSettings()
    {
        var tempDir = CreateTempConfigDirectory();
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDir);

            var factory = new MarketDataDbContextFactory();
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
    public void AddMarketDataDbContext_RegistersDbContext_ForSupportedProvider()
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
        services.AddMarketDataDbContext(configuration);

        Assert.Contains(services, x => x.ServiceType == typeof(DbContextOptions<MarketDataDbContext>));
    }

    [Fact]
    public void MarketDataDbContextFactory_Throws_ForUnsupportedProvider()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"market-factory-invalid-{Guid.NewGuid():N}");
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
            var factory = new MarketDataDbContextFactory();

            Assert.Throws<NotSupportedException>(() => factory.CreateDbContext([]));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void AddMarketDataDbContext_Throws_ForMissingConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "MySql",
                ["Database:ConnectionStringName"] = "mysql"
            })
            .Build();

        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddMarketDataDbContext(configuration));
    }

    [Fact]
    public void ProjectPaths_ResolvesCotacoesDirectory_FromSolutionRoot()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"project-paths-{Guid.NewGuid():N}");
        var nested = Path.Combine(tempDir, "src", "nested");
        Directory.CreateDirectory(Path.Combine(tempDir, "cotacoes"));
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(tempDir, "Itau.InvestCycleEngine.slnx"), string.Empty);

        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(nested);

            var type = typeof(MarketDataDbContextFactory).Assembly.GetType("MarketDataIngestionService.Support.ProjectPaths");
            var method = type!.GetMethod("GetCotacoesDirectory", BindingFlags.Static | BindingFlags.Public);
            var path = Assert.IsType<string>(method!.Invoke(null, null));

            Assert.Equal(Path.Combine(tempDir, "cotacoes"), path);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ProjectPaths_FallsBackToCurrentDirectory_WhenSolutionRootIsMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"project-paths-fallback-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);

            var type = typeof(MarketDataDbContextFactory).Assembly.GetType("MarketDataIngestionService.Support.ProjectPaths");
            var method = type!.GetMethod("GetCotacoesDirectory", BindingFlags.Static | BindingFlags.Public);
            var path = Assert.IsType<string>(method!.Invoke(null, null));
            var expectedFromCurrentDirectory = Path.Combine(tempDir, "cotacoes");
            var expectedFromAppBase = ResolveExpectedFromAppBaseDirectory();

            Assert.True(
                string.Equals(path, expectedFromCurrentDirectory, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, expectedFromAppBase, StringComparison.OrdinalIgnoreCase));
            Assert.True(Directory.Exists(path));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string ResolveExpectedFromAppBaseDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var existingCotacoes = Path.Combine(current.FullName, "cotacoes");
            if (Directory.Exists(existingCotacoes))
            {
                return existingCotacoes;
            }

            var solutionPath = Path.Combine(current.FullName, "Itau.InvestCycleEngine.slnx");
            if (File.Exists(solutionPath))
            {
                return existingCotacoes;
            }

            current = current.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "cotacoes");
    }

    private static string CreateTempConfigDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"market-factory-{Guid.NewGuid():N}");
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
