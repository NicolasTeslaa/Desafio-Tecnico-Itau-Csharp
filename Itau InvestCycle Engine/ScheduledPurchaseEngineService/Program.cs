using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Data;
using ScheduledPurchaseEngineService.Interfaces;
using ScheduledPurchaseEngineService.Repositories;
using ScheduledPurchaseEngineService.Services;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScheduledPurchaseDbContext(builder.Configuration);

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IClientesRepository, ClientesRepository>();
builder.Services.AddScoped<IClienteValorMensalHistoricoRepository, ClienteValorMensalHistoricoRepository>();
builder.Services.AddScoped<IClentService, ClientService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IScheduledPurchaseEngine, ScheduledPurchaseEngine>();

// CORS mais flexível para desenvolvimento (permite localhost/127.0.0.1 em qualquer porta)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin))
                    return false;

                // Origin vem como "http(s)://host:porta"
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;

                return uri.Host == "localhost" || uri.Host == "127.0.0.1";
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

ApplyMigrationsWithRetry<ScheduledPurchaseDbContext>(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(); // DefaultPolicy

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
