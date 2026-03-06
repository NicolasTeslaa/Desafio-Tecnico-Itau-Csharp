using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Domain.Entities;
using Itau.InvestCycleEngine.Contracts.Common;
using MarketDataIngestionService.Data;
using MarketDataIngestionService.Repositories;
using MarketDataIngestionService.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketDataIngestionService.Tests;

public sealed class CotacoesServiceTests
{
    [Fact]
    public async Task SaveFromCotahistAsync_UpsertsNormalizedQuotes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MarketDataDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MarketDataDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await EnsureCotacoesTableAsync(db);

        db.Cotacoes.Add(new Cotacoes
        {
            DataPregao = new DateTime(2026, 3, 5),
            Ticker = "PETR4",
            PrecoAbertura = 10m,
            PrecoFechamento = 10m,
            PrecoMaximo = 10m,
            PrecoMinimo = 10m
        });
        await db.SaveChangesAsync();

        var service = new CotacoesService(new UnitOfWork(db), NullLogger<CotacoesService>.Instance);

        var saved = await service.SaveFromCotahistAsync(
        [
            new CotahistPriceRecord(" petr4 ", new DateOnly(2026, 3, 5), 11m, 13m, 10m, 12m, 1000m),
            new CotahistPriceRecord(" vale3 ", new DateOnly(2026, 3, 5), 20m, 22m, 19m, 21m, 2000m)
        ], CancellationToken.None);

        Assert.Equal(2, saved);

        var quotes = await db.Cotacoes.OrderBy(x => x.Ticker).ToListAsync();
        Assert.Equal(2, quotes.Count);
        Assert.Equal("PETR4", quotes[0].Ticker);
        Assert.Equal(11m, quotes[0].PrecoAbertura);
        Assert.Equal(12m, quotes[0].PrecoFechamento);
        Assert.Equal("VALE3", quotes[1].Ticker);
    }

    [Fact]
    public async Task ListAsync_AndListDistinctTickersAsync_ApplyFiltersPagingAndLimit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MarketDataDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MarketDataDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await EnsureCotacoesTableAsync(db);

        db.Cotacoes.AddRange(
            CreateQuote("PETR4", new DateTime(2026, 3, 6), 30m),
            CreateQuote("PETR4", new DateTime(2026, 3, 5), 29m),
            CreateQuote("VALE3", new DateTime(2026, 3, 6), 40m),
            CreateQuote("ABEV3", new DateTime(2026, 3, 6), 10m));
        await db.SaveChangesAsync();

        var service = new CotacoesService(new UnitOfWork(db), NullLogger<CotacoesService>.Instance);

        var page = await service.ListAsync(new PagedRequest(1, 1), " petr4 ", new DateTime(2026, 3, 6), CancellationToken.None);
        var tickers = await service.ListDistinctTickersAsync(null, 2, CancellationToken.None);

        Assert.Equal(1, page.Items.Count);
        Assert.Equal(1, page.TotalItems);
        Assert.Equal("PETR4", page.Items[0].Ticker);

        Assert.Equal(2, tickers.Count);
        Assert.Equal(["ABEV3", "PETR4"], tickers);
    }

    private static Cotacoes CreateQuote(string ticker, DateTime dataPregao, decimal fechamento)
        => new()
        {
            DataPregao = dataPregao.Date,
            Ticker = ticker,
            PrecoAbertura = fechamento,
            PrecoFechamento = fechamento,
            PrecoMaximo = fechamento,
            PrecoMinimo = fechamento
        };

    private static Task EnsureCotacoesTableAsync(MarketDataDbContext db)
        => db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS cotacoes (
                Id INTEGER NOT NULL CONSTRAINT PK_cotacoes PRIMARY KEY AUTOINCREMENT,
                DataPregao TEXT NOT NULL,
                Ticker TEXT NOT NULL,
                PrecoAbertura TEXT NOT NULL,
                PrecoFechamento TEXT NOT NULL,
                PrecoMaximo TEXT NOT NULL,
                PrecoMinimo TEXT NOT NULL
            );
            """);
}
