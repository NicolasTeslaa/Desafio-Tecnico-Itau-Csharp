using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ScheduledPurchaseEngineService.Data;

public sealed class DbContextFactory : IDesignTimeDbContextFactory<ScheduledPurchaseDbContext>
{
    public ScheduledPurchaseDbContext CreateDbContext(string[] args)
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

        var optionsBuilder = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>();

        switch (provider.Trim().ToLowerInvariant())
        {
            case "mysql":
                optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(ParseMySqlServerVersion(configuration["Database:MySqlServerVersion"])));
                break;
            default:
                throw new NotSupportedException($"Database provider '{provider}' is not supported.");
        }

        return new ScheduledPurchaseDbContext(optionsBuilder.Options);
    }

    private static Version ParseMySqlServerVersion(string? versionRaw)
        => Version.TryParse(versionRaw, out var version)
            ? version
            : new Version(8, 4, 0);
}

public static class ScheduledPurchaseDbContextRegistrationExtensions
{
    public static IServiceCollection AddScheduledPurchaseDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "MySql";
        var connectionStringName = configuration["Database:ConnectionStringName"] ?? "mysql";
        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException($"Missing connection string '{connectionStringName}'.");

        switch (provider.Trim().ToLowerInvariant())
        {
            case "mysql":
                services.AddDbContext<ScheduledPurchaseDbContext>(opt =>
                {
                    opt.UseMySql(connectionString, new MySqlServerVersion(ParseMySqlServerVersion(configuration["Database:MySqlServerVersion"])));
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
