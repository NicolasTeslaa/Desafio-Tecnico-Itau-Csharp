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