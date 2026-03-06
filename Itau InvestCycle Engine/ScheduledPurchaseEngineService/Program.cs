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
            await EnsureCriticalTablesAsync(db, logger);
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

static async Task EnsureCriticalTablesAsync(ScheduledPurchaseDbContext db, ILogger logger)
{
    if (!await TableExistsAsync(db, "conta_master"))
    {
        logger.LogWarning("Tabela 'conta_master' ausente apos migrations. Aplicando bootstrap idempotente.");
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS conta_master (
                Id int NOT NULL AUTO_INCREMENT,
                ContaGraficaId int NOT NULL,
                DataCriacao datetime(6) NOT NULL,
                CONSTRAINT PK_conta_master PRIMARY KEY (Id),
                CONSTRAINT FK_conta_master_contas_graficas_ContaGraficaId
                    FOREIGN KEY (ContaGraficaId) REFERENCES contas_graficas (Id)
                    ON DELETE RESTRICT,
                CONSTRAINT UX_conta_master_contagrafica UNIQUE (ContaGraficaId)
            );
            """);
    }

    if (!await TableExistsAsync(db, "precos_medios"))
    {
        logger.LogWarning("Tabela 'precos_medios' ausente apos migrations. Aplicando bootstrap idempotente.");
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS precos_medios (
                Id int NOT NULL AUTO_INCREMENT,
                CustodiaId int NOT NULL,
                Valor decimal(18,6) NOT NULL,
                DataAtualizacao datetime(6) NOT NULL,
                CONSTRAINT PK_precos_medios PRIMARY KEY (Id),
                CONSTRAINT FK_precos_medios_custodias_CustodiaId
                    FOREIGN KEY (CustodiaId) REFERENCES custodias (Id)
                    ON DELETE CASCADE,
                CONSTRAINT UX_precos_medios_custodia UNIQUE (CustodiaId)
            );
            """);
    }

    await db.Database.ExecuteSqlRawAsync("""
        INSERT IGNORE INTO conta_master (ContaGraficaId, DataCriacao)
        SELECT cg.Id, cg.DataCriacao
        FROM contas_graficas cg
        WHERE cg.Tipo = 1;
        """);

    await db.Database.ExecuteSqlRawAsync("""
        INSERT IGNORE INTO precos_medios (CustodiaId, Valor, DataAtualizacao)
        SELECT c.Id, c.PrecoMedio, c.DataUltimaAtualizacao
        FROM custodias c;
        """);
}

static async Task<bool> TableExistsAsync(ScheduledPurchaseDbContext db, string tableName)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
        await connection.OpenAsync();

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName;
            """;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }
    finally
    {
        if (shouldClose)
            await connection.CloseAsync();
    }
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
