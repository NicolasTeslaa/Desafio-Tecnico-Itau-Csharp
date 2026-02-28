using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Data;
using ScheduledPurchaseEngineService.Interfaces;
using ScheduledPurchaseEngineService.Repositories;
using ScheduledPurchaseEngineService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var conn = builder.Configuration.GetConnectionString("mysql")
          ?? throw new InvalidOperationException("Missing connection string 'mysql'.");

builder.Services.AddDbContext<ScheduledPurchaseDbContext>(opt =>
{
    opt.UseMySql(conn, ServerVersion.AutoDetect(conn));
});

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IClientesRepository, ClientesRepository>();
builder.Services.AddScoped<IClienteValorMensalHistoricoRepository, ClienteValorMensalHistoricoRepository>();
builder.Services.AddScoped<IClentService, ClientService>();
builder.Services.AddScoped<IScheduledPurchaseEngine, ScheduledPurchaseEngine>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
