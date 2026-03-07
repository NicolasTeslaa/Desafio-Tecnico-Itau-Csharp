using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketDataIngestionService.Data;

public sealed class MarketDataDbContextFactory : IDesignTimeDbContextFactory<MarketDataDbContext>
{
    public MarketDataDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var provider = configuration["Database:Provider"] ?? "MySql";
        var connectionStringName = configuration["Database:ConnectionStringName"] ?? "mysql";
        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException($"Missing connection string '{connectionStringName}'.");

        var optionsBuilder = new DbContextOptionsBuilder<MarketDataDbContext>();

        switch (provider.Trim().ToLowerInvariant())
        {
            case "mysql":
                optionsBuilder.UseMySql(
                    connectionString,
                    new MySqlServerVersion(ParseMySqlServerVersion(configuration["Database:MySqlServerVersion"])),
                    mysql => mysql.MigrationsHistoryTable("__EFMigrationsHistory_MarketData"));
                break;
            default:
                throw new NotSupportedException($"Database provider '{provider}' is not supported.");
        }

        return new MarketDataDbContext(optionsBuilder.Options);
    }

    private static Version ParseMySqlServerVersion(string? versionRaw)
        => Version.TryParse(versionRaw, out var version)
            ? version
            : new Version(8, 4, 0);
}

public static class MarketDataDbContextRegistrationExtensions
{
    public static IServiceCollection AddMarketDataDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "MySql";
        var connectionStringName = configuration["Database:ConnectionStringName"] ?? "mysql";
        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException($"Missing connection string '{connectionStringName}'.");

        switch (provider.Trim().ToLowerInvariant())
        {
            case "mysql":
                services.AddDbContext<MarketDataDbContext>(opt =>
                {
                    opt.UseMySql(
                        connectionString,
                        new MySqlServerVersion(ParseMySqlServerVersion(configuration["Database:MySqlServerVersion"])),
                        mysql => mysql.MigrationsHistoryTable("__EFMigrationsHistory_MarketData"));
                });
                break;
            default:
                throw new NotSupportedException($"Database provider '{provider}' is not supported.");
        }

        return services;
    }

    private static Version ParseMySqlServerVersion(string? versionRaw)
        => Version.TryParse(versionRaw, out var version)
            ? version
            : new Version(8, 4, 0);
}
