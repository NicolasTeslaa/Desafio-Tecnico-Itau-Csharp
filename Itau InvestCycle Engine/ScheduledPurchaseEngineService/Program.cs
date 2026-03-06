using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Data;
using ScheduledPurchaseEngineService.Interfaces;
using ScheduledPurchaseEngineService.Repositories;
using ScheduledPurchaseEngineService.Services;
using ScheduledPurchaseEngineService.Settings;
using System.Data.Common;
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
builder.Services.AddScoped<ITradingCalendar, TradingCalendar>();
builder.Services.AddSingleton<IFinanceEventsPublisher, KafkaFinanceEventsPublisher>();
builder.Services.AddScoped<IRebalanceService, RebalanceService>();
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection(KafkaSettings.SectionName));
builder.Services.AddHostedService<ScheduledPurchaseHostedService>();

// CORS mais flexivel para desenvolvimento (permite localhost/127.0.0.1 em qualquer porta)
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

UseRequestIdHeader(app);
UseSanitizedExceptionHandling(app);
await EnsureScheduledPurchaseSchemaAsync(app.Services, app.Logger);

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

static async Task EnsureScheduledPurchaseSchemaAsync(IServiceProvider services, ILogger logger)
{
    const int maxAttempts = 10;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ScheduledPurchaseDbContext>();
            await db.Database.MigrateAsync();
            logger.LogInformation("Schema do ScheduledPurchase validado com sucesso.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex, "Falha ao preparar schema do ScheduledPurchase (tentativa {Attempt}/{MaxAttempts}).", attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    throw new InvalidOperationException("Nao foi possivel preparar o schema do ScheduledPurchase apos multiplas tentativas.");
}

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
