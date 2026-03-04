using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Data;
using ScheduledPurchaseEngineService.Interfaces;
using ScheduledPurchaseEngineService.Repositories;
using ScheduledPurchaseEngineService.Services;
using ScheduledPurchaseEngineService.Settings;
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
