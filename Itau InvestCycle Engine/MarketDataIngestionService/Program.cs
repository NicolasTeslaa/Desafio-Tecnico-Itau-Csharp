using MarketDataIngestionService.Data;
using MarketDataIngestionService.Interfaces;
using MarketDataIngestionService.Parser;
using MarketDataIngestionService.Repositories;
using MarketDataIngestionService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMarketDataDbContext(builder.Configuration);

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICotacoesService, CotacoesService>();
builder.Services.AddScoped<CotahistParser>();

// CORS mais flexível para desenvolvimento
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                // Permite qualquer localhost/127.0.0.1 em qualquer porta
                if (string.IsNullOrEmpty(origin))
                    return false;

                var uri = new Uri(origin);
                return uri.Host == "localhost" || uri.Host == "127.0.0.1";
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

ApplyMigrationsWithRetry<MarketDataDbContext>(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(); // usa DefaultPolicy

app.UseAuthorization();

app.MapControllers();

app.Run();

static void ApplyMigrationsWithRetry<TContext>(IServiceProvider services) where TContext : DbContext
{
    using var scope = services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");
    var db = scope.ServiceProvider.GetRequiredService<TContext>();

    const int maxAttempts = 10;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            db.Database.Migrate();
            logger.LogInformation("Migrations applied for {DbContext}.", typeof(TContext).Name);
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(
                ex,
                "Failed to apply migrations for {DbContext} (attempt {Attempt}/{MaxAttempts}). Retrying in 5 seconds.",
                typeof(TContext).Name,
                attempt,
                maxAttempts);

            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }

    db.Database.Migrate();
}
