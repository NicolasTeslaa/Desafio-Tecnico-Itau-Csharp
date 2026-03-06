using MarketDataIngestionService.Data;
using MarketDataIngestionService.Interfaces;
using MarketDataIngestionService.Parser;
using MarketDataIngestionService.Repositories;
using MarketDataIngestionService.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

            await db.Database.MigrateAsync();

            if (!await TableExistsAsync(db, "ingestao_jobs"))
            {
                logger.LogWarning("Tabela 'ingestao_jobs' ausente apos migrate. Criando tabela de seguranca.");
                await CreateIngestaoJobsTableAsync(db);
            }

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

static async Task<bool> TableExistsAsync(MarketDataDbContext db, string tableName)
{
    await using var connection = db.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @tableName";

    var parameter = command.CreateParameter();
    parameter.ParameterName = "@tableName";
    parameter.Value = tableName;
    command.Parameters.Add(parameter);

    var result = await command.ExecuteScalarAsync();
    return Convert.ToInt32(result) > 0;
}

static Task CreateIngestaoJobsTableAsync(MarketDataDbContext db)
    => db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS `ingestao_jobs` (
          `Id` char(36) COLLATE ascii_general_ci NOT NULL,
          `File` varchar(255) NOT NULL,
          `StoredPath` varchar(1024) NOT NULL,
          `Status` varchar(20) NOT NULL,
          `CreatedAtUtc` datetime(6) NOT NULL,
          `StartedAtUtc` datetime(6) NULL,
          `FinishedAtUtc` datetime(6) NULL,
          `Saved` int NOT NULL,
          `Error` varchar(2000) NULL,
          PRIMARY KEY (`Id`),
          KEY `ix_ingestao_jobs_createdatutc` (`CreatedAtUtc`),
          KEY `ix_ingestao_jobs_status` (`Status`)
        ) CHARACTER SET utf8mb4;
        """);
