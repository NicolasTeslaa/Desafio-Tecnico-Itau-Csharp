using MarketDataIngestionService.Data;
using MarketDataIngestionService.Interfaces;
using MarketDataIngestionService.Parser;
using MarketDataIngestionService.Repositories;
using MarketDataIngestionService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ✅ Swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var conn = builder.Configuration.GetConnectionString("mysql")
          ?? throw new InvalidOperationException("Missing connection string 'mysql'.");

builder.Services.AddDbContext<MarketDataDbContext>(opt =>
{
    opt.UseMySql(conn, ServerVersion.AutoDetect(conn));
});

builder.Services.AddScoped<ICotacoesRepository, CotacoesRepository>();
builder.Services.AddScoped<ICotacoesService, CotacoesService>();
builder.Services.AddScoped<CotahistParser>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();