using MarketDataIngestionService.Data;
using MarketDataIngestionService.Interfaces;
using MarketDataIngestionService.Parser;
using MarketDataIngestionService.Repositories;
using MarketDataIngestionService.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

builder.Services.AddMarketDataDbContext(builder.Configuration);

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICotacoesService, CotacoesService>();
builder.Services.AddScoped<CotahistParser>();
builder.Services.AddSingleton<IngestJobBackgroundService>();
builder.Services.AddSingleton<IIngestJobService>(sp => sp.GetRequiredService<IngestJobBackgroundService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<IngestJobBackgroundService>());

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

UseRequestIdHeader(app);
UseSanitizedExceptionHandling(app);

await EnsureMarketDataSchemaAsync(app.Services, app.Logger);

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
app.MapHealthChecks("/health");

app.Run();

static void UseRequestIdHeader(WebApplication app)
{
    app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
            return Task.CompletedTask;
        });

        await next();
    });
}

static void UseSanitizedExceptionHandling(WebApplication app)
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var exception = feature?.Error;
            var traceId = context.TraceIdentifier;

            var isDependencyFailure = exception is DbException or TimeoutException;
            var statusCode = isDependencyFailure
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status500InternalServerError;

            var errorCode = isDependencyFailure ? "DEPENDENCY_UNAVAILABLE" : "INTERNAL_ERROR";

            app.Logger.LogError(exception, "Unhandled exception. TraceId={TraceId}, Path={Path}", traceId, context.Request.Path);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            context.Response.Headers["X-Request-Id"] = traceId;
            await context.Response.WriteAsJsonAsync(new
            {
                code = errorCode,
                message = "Nao foi possivel concluir a requisicao no momento.",
                traceId
            });
        });
    });
}

static async Task EnsureMarketDataSchemaAsync(IServiceProvider services, ILogger logger)
{
    const int maxAttempts = 10;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();

            var hasMigrations = db.Database.GetMigrations().Any();
            if (!hasMigrations)
            {
                throw new InvalidOperationException("Nenhuma migration encontrada para MarketDataDbContext.");
            }

            await db.Database.MigrateAsync();

            logger.LogInformation("Schema do MarketData validado com sucesso.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex, "Falha ao preparar schema do MarketData (tentativa {Attempt}/{MaxAttempts}).", attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    throw new InvalidOperationException("Nao foi possivel preparar o schema do MarketData apos multiplas tentativas.");
}
