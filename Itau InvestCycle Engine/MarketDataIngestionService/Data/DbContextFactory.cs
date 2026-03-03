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
                optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                break;
            default:
                throw new NotSupportedException($"Database provider '{provider}' is not supported.");
        }

        return new MarketDataDbContext(optionsBuilder.Options);
    }
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
                    opt.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                });
                break;
            default:
                throw new NotSupportedException($"Database provider '{provider}' is not supported.");
        }

        return services;
    }
}
