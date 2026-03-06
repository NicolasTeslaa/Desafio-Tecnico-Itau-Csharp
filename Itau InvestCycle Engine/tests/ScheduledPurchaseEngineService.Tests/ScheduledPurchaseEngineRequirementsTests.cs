using ClassLibrary.Domain.Entities;
using ClassLibrary.Domain.Entities.Cestas;
using ClassLibrary.Domain.Entities.Clientes;
using ClassLibrary.Domain.Entities.RebalanceamentoIR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ScheduledPurchaseEngineService.Data;
using ScheduledPurchaseEngineService.Interfaces;
using ScheduledPurchaseEngineService.Repositories;
using ScheduledPurchaseEngineService.Services;

namespace ScheduledPurchaseEngineService.Tests;

public sealed class ScheduledPurchaseEngineRequirementsTests
{
    [Fact]
    public async Task ExecuteAsync_DeveUsarCestaVigenteDaDataReferencia_QuandoExecucaoManualForRetroativa()
    {
        await using var db = await CreateInMemoryDbAsync();

        await SeedBasketVersionAsync(
            db,
            "Top Five - Fevereiro 2026",
            ativa: false,
            dataCriacaoUtc: new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc),
            dataDesativacaoUtc: new DateTime(2026, 3, 6, 9, 0, 0, DateTimeKind.Utc),
            [("AGRO3", 90m), ("BAHI3", 1m), ("BBSE3", 1m), ("ITUB4", 1m), ("PETR4", 7m)]);

        await SeedBasketVersionAsync(
            db,
            "Top Five - Marco 2026",
            ativa: true,
            dataCriacaoUtc: new DateTime(2026, 3, 6, 9, 0, 0, DateTimeKind.Utc),
            dataDesativacaoUtc: null,
            [("AGRO3", 20m), ("BAHI3", 20m), ("BBSE3", 20m), ("ITUB4", 20m), ("PETR4", 20m)]);

        await SeedClientAndQuotesAsync(db, "12345678901", 3000m, new DateTime(2026, 3, 5));

        var service = CreateService(db);

        await service.ExecuteAsync(new DateOnly(2026, 3, 5));

        var ordens = await db.OrdensCompra
            .AsNoTracking()
            .OrderBy(x => x.Ticker)
            .ToListAsync();

        Assert.Equal(5, ordens.Count);
        Assert.Contains(ordens, x => x.Ticker == "AGRO3F" && x.Quantidade == 90);
        Assert.Contains(ordens, x => x.Ticker == "BAHI3F" && x.Quantidade == 1);
        Assert.Contains(ordens, x => x.Ticker == "BBSE3F" && x.Quantidade == 1);
        Assert.Contains(ordens, x => x.Ticker == "ITUB4F" && x.Quantidade == 1);
        Assert.Contains(ordens, x => x.Ticker == "PETR4F" && x.Quantidade == 7);
        Assert.DoesNotContain(ordens, x => x.Quantidade == 20);
        Assert.All(ordens, ordem => Assert.EndsWith("F", ordem.Ticker));
    }

    [Fact]
    public async Task ExecuteAsync_DeveUsarNovaCesta_NoDiaEmQueElaEntraEmVigor()
    {
        await using var db = await CreateInMemoryDbAsync();

        await SeedBasketVersionAsync(
            db,
            "Top Five - Fevereiro 2026",
            ativa: false,
            dataCriacaoUtc: new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc),
            dataDesativacaoUtc: new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
            [("AGRO3", 90m), ("BAHI3", 1m), ("BBSE3", 1m), ("ITUB4", 1m), ("PETR4", 7m)]);

        await SeedBasketVersionAsync(
            db,
            "Top Five - Marco 2026",
            ativa: true,
            dataCriacaoUtc: new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
            dataDesativacaoUtc: null,
            [("AGRO3", 20m), ("BAHI3", 20m), ("BBSE3", 20m), ("ITUB4", 20m), ("PETR4", 20m)]);

        await SeedClientAndQuotesAsync(db, "12345678902", 3000m, new DateTime(2026, 3, 1));

        var service = CreateService(db);

        await service.ExecuteAsync(new DateOnly(2026, 3, 1));

        var ordens = await db.OrdensCompra
            .AsNoTracking()
            .OrderBy(x => x.Ticker)
            .ToListAsync();

        Assert.Equal(5, ordens.Count);
        Assert.All(ordens, ordem => Assert.Equal(20, ordem.Quantidade));
        Assert.All(ordens, ordem => Assert.EndsWith("F", ordem.Ticker));
        Assert.DoesNotContain(ordens, x => x.Ticker == "AGRO3F" && x.Quantidade == 90);
    }

    [Fact]
    public async Task ExecuteAsync_DeveFalhar_QuandoNaoExistirCestaVigenteNaDataReferencia()
    {
        await using var db = await CreateInMemoryDbAsync();

        await SeedBasketVersionAsync(
            db,
            "Top Five - Marco 2026",
            ativa: true,
            dataCriacaoUtc: new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
            dataDesativacaoUtc: null,
            [("AGRO3", 20m), ("BAHI3", 20m), ("BBSE3", 20m), ("ITUB4", 20m), ("PETR4", 20m)]);

        await SeedClientAndQuotesAsync(db, "12345678903", 3000m, new DateTime(2026, 3, 5));

        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteAsync(new DateOnly(2026, 2, 5)));

        Assert.Equal("CESTA_NAO_ENCONTRADA", ex.Message);
    }

    private static ScheduledPurchaseEngine CreateService(ScheduledPurchaseDbContext db)
        => new(
            new UnitOfWork(db),
            new AlwaysOpenTradingCalendarRequirement(),
            new NoOpFinanceEventsPublisherRequirement(),
            NullLogger<ScheduledPurchaseEngine>.Instance);

    private static async Task<ScheduledPurchaseDbContext> CreateInMemoryDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task SeedBasketVersionAsync(
        ScheduledPurchaseDbContext db,
        string nome,
        bool ativa,
        DateTime dataCriacaoUtc,
        DateTime? dataDesativacaoUtc,
        IReadOnlyList<(string Ticker, decimal Percentual)> itens)
    {
        var cesta = new CestasRecomendacao
        {
            Nome = nome,
            Ativa = ativa,
            DataCriacao = dataCriacaoUtc,
            DataDesativacao = dataDesativacaoUtc
        };

        db.CestasRecomendacao.Add(cesta);
        await db.SaveChangesAsync();

        db.ItensCesta.AddRange(itens.Select(item => new ItensCesta
        {
            CestaId = cesta.Id,
            Ticker = item.Ticker,
            Percentual = item.Percentual
        }));

        await db.SaveChangesAsync();
    }

    private static async Task SeedClientAndQuotesAsync(
        ScheduledPurchaseDbContext db,
        string cpf,
        decimal valorMensal,
        DateTime dataPregao)
    {
        db.Clientes.Add(new Clientes
        {
            Nome = $"Cliente {cpf}",
            CPF = cpf,
            Email = $"{cpf}@teste.com",
            ValorMensal = valorMensal,
            Ativo = true,
            DataAdesao = dataPregao.ToUniversalTime()
        });

        db.Cotacoes.AddRange(
            CreateQuote(dataPregao, "AGRO3"),
            CreateQuote(dataPregao, "BAHI3"),
            CreateQuote(dataPregao, "BBSE3"),
            CreateQuote(dataPregao, "ITUB4"),
            CreateQuote(dataPregao, "PETR4"));

        await db.SaveChangesAsync();
    }

    private static Cotacoes CreateQuote(DateTime dataPregao, string ticker)
        => new()
        {
            DataPregao = DateTime.SpecifyKind(dataPregao.Date, DateTimeKind.Utc),
            Ticker = ticker,
            PrecoAbertura = 10m,
            PrecoFechamento = 10m,
            PrecoMaximo = 10m,
            PrecoMinimo = 10m
        };

    private sealed class AlwaysOpenTradingCalendarRequirement : ITradingCalendar
    {
        public bool IsBusinessDay(DateOnly date) => true;

        public DateOnly NextBusinessDay(DateOnly date) => date;

        public DateOnly ResolveRunDate(DateOnly baseDate) => baseDate;

        public bool IsPurchaseDate(DateOnly date) => true;
    }

    private sealed class NoOpFinanceEventsPublisherRequirement : IFinanceEventsPublisher
    {
        public Task PublishIrDedoDuroAsync(EventosIR evt, string cpf, string ticker, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task PublishIrVendaAsync(EventosIR evt, string cpf, IrVendaKafkaPayload payload, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
