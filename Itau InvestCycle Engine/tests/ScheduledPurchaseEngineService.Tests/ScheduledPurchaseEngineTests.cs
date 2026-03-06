using ClassLibrary.Domain.Entities;
using ClassLibrary.Domain.Entities.Cestas;
using ClassLibrary.Domain.Entities.Clientes;
using ClassLibrary.Domain.Entities.RebalanceamentoIR;
using Itau.InvestCycleEngine.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ScheduledPurchaseEngineService.Data;
using ScheduledPurchaseEngineService.Interfaces;
using ScheduledPurchaseEngineService.Repositories;
using ScheduledPurchaseEngineService.Services;

namespace ScheduledPurchaseEngineService.Tests;

public sealed class ScheduledPurchaseEngineTests
{
    [Fact]
    public async Task ExecuteAsync_PublicaIrDedoDuro_ParaCadaDistribuicao()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var agora = new DateTime(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc);

        var cesta = new CestasRecomendacao
        {
            Nome = "Top Five Teste",
            Ativa = true,
            DataCriacao = agora
        };

        db.CestasRecomendacao.Add(cesta);
        await db.SaveChangesAsync();

        db.ItensCesta.Add(new ItensCesta
        {
            CestaId = cesta.Id,
            Ticker = "PETR4",
            Percentual = 100m
        });

        db.Clientes.Add(new Clientes
        {
            Nome = "Cliente IR",
            CPF = "12345678909",
            Email = "cliente-ir@teste.com",
            ValorMensal = 600m,
            Ativo = true,
            DataAdesao = agora
        });

        db.Cotacoes.Add(new Cotacoes
        {
            DataPregao = agora.Date,
            Ticker = "PETR4",
            PrecoAbertura = 10m,
            PrecoFechamento = 10m,
            PrecoMaximo = 10m,
            PrecoMinimo = 10m
        });

        await db.SaveChangesAsync();

        var publisher = new SpyFinanceEventsPublisher();
        var service = new ScheduledPurchaseEngine(
            new UnitOfWork(db),
            new AlwaysOpenTradingCalendar(),
            publisher,
            NullLogger<ScheduledPurchaseEngine>.Instance);

        var result = await service.ExecuteAsync(new DateOnly(2026, 3, 5));

        Assert.Equal(1, result.IrEventsPublished);
        Assert.Single(publisher.DedoDuroPublications);
        Assert.Equal("12345678909", publisher.DedoDuroPublications[0].Cpf);
        Assert.Equal("PETR4", publisher.DedoDuroPublications[0].Ticker);

        var evento = await db.EventosIR.SingleAsync();
        Assert.Equal(TipoIR.DEDO_DURO, evento.Tipo);
        Assert.Equal(200m, evento.ValorBase);
        Assert.Equal(0.01m, evento.ValorIR);
        Assert.True(evento.PublicadoKafka);
    }

    [Fact]
    public async Task ExecuteAsync_Rejeita_DataQueNaoEhDataDeCompra()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var service = new ScheduledPurchaseEngine(
            new UnitOfWork(db),
            new ClosedTradingCalendar(),
            new NoOpFinanceEventsPublisher(),
            NullLogger<ScheduledPurchaseEngine>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(new DateOnly(2026, 3, 6)));

        Assert.Equal("DATA_EXECUCAO_INVALIDA", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_PersistsDistributionTraceability_ForPurchasedAssets()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var agora = new DateTime(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc);

        var cesta = new CestasRecomendacao
        {
            Nome = "Top Five Teste",
            Ativa = true,
            DataCriacao = agora
        };

        db.CestasRecomendacao.Add(cesta);
        await db.SaveChangesAsync();

        db.ItensCesta.Add(new ItensCesta
        {
            CestaId = cesta.Id,
            Ticker = "PETR4",
            Percentual = 100m
        });

        db.Clientes.Add(new Clientes
        {
            Nome = "Cliente Teste",
            CPF = "12345678909",
            Email = "cliente@teste.com",
            ValorMensal = 300m,
            Ativo = true,
            DataAdesao = agora
        });

        db.Cotacoes.Add(new Cotacoes
        {
            DataPregao = agora.Date,
            Ticker = "PETR4",
            PrecoAbertura = 10m,
            PrecoFechamento = 10m,
            PrecoMaximo = 10m,
            PrecoMinimo = 10m
        });

        await db.SaveChangesAsync();

        var service = new ScheduledPurchaseEngine(
            new UnitOfWork(db),
            new AlwaysOpenTradingCalendar(),
            new NoOpFinanceEventsPublisher(),
            NullLogger<ScheduledPurchaseEngine>.Instance);

        await service.ExecuteAsync(new DateOnly(2026, 3, 5));

        var ordem = await db.OrdensCompra.SingleAsync();
        var custodiaFilhote = await db.Custodias
            .Join(
                db.ContasGraficas,
                custodia => custodia.ContasGraficasId,
                conta => conta.Id,
                (custodia, conta) => new { Custodia = custodia, conta.Tipo })
            .Where(x => x.Tipo == TipoConta.Filhote)
            .Select(x => x.Custodia)
            .SingleAsync();
        var distribuicao = await db.Distribuicoes.SingleAsync();

        Assert.Equal(ordem.Id, distribuicao.OrdemCompraId);
        Assert.Equal(custodiaFilhote.Id, distribuicao.CustodiaFilhoteId);
        Assert.Equal("PETR4F", ordem.Ticker);
        Assert.Equal(0, ordem.QuantidadeDisponivel);
        Assert.Equal("PETR4", distribuicao.Ticker);
        Assert.Equal(10, distribuicao.Quantidade);
        Assert.Equal(10m, distribuicao.PrecoUnitario);
        Assert.Equal(100m, distribuicao.Valor);
        Assert.NotEqual(default, distribuicao.DataDistribuicao);
    }

    [Fact]
    public async Task ExecuteAsync_UsesResidualMasterBalance_WithOriginalOrderTraceability()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var agora = new DateTime(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc);

        var cesta = new CestasRecomendacao
        {
            Nome = "Top Five Teste",
            Ativa = true,
            DataCriacao = agora
        };

        db.CestasRecomendacao.Add(cesta);
        await db.SaveChangesAsync();

        db.ItensCesta.Add(new ItensCesta
        {
            CestaId = cesta.Id,
            Ticker = "PETR4",
            Percentual = 100m
        });

        db.Clientes.AddRange(
            new Clientes
            {
                Nome = "Cliente A",
                CPF = "12345678909",
                Email = "clientea@teste.com",
                ValorMensal = 110m,
                Ativo = true,
                DataAdesao = agora
            },
            new Clientes
            {
                Nome = "Cliente B",
                CPF = "12345678910",
                Email = "clienteb@teste.com",
                ValorMensal = 110m,
                Ativo = true,
                DataAdesao = agora
            },
            new Clientes
            {
                Nome = "Cliente C",
                CPF = "12345678911",
                Email = "clientec@teste.com",
                ValorMensal = 110m,
                Ativo = true,
                DataAdesao = agora
            });

        db.Cotacoes.AddRange(
            new Cotacoes
            {
                DataPregao = agora.Date,
                Ticker = "PETR4",
                PrecoAbertura = 10m,
                PrecoFechamento = 10m,
                PrecoMaximo = 10m,
                PrecoMinimo = 10m
            },
            new Cotacoes
            {
                DataPregao = agora.Date.AddDays(10),
                Ticker = "PETR4",
                PrecoAbertura = 10m,
                PrecoFechamento = 10m,
                PrecoMaximo = 10m,
                PrecoMinimo = 10m
            });

        await db.SaveChangesAsync();

        var service = new ScheduledPurchaseEngine(
            new UnitOfWork(db),
            new AlwaysOpenTradingCalendar(),
            new NoOpFinanceEventsPublisher(),
            NullLogger<ScheduledPurchaseEngine>.Instance);

        await service.ExecuteAsync(new DateOnly(2026, 3, 5));

        var ordemOriginal = await db.OrdensCompra.SingleAsync();
        var distribuicoesPrimeiraExecucao = await db.Distribuicoes.CountAsync();
        Assert.Equal(2, ordemOriginal.QuantidadeDisponivel);

        var clientes = await db.Clientes.OrderBy(x => x.Id).ToListAsync();
        clientes[1].Ativo = false;
        clientes[2].Ativo = false;
        clientes[0].ValorMensal = 30m;
        await db.SaveChangesAsync();

        await service.ExecuteAsync(new DateOnly(2026, 3, 15));

        var distribuicoesSegundaExecucao = await db.Distribuicoes
            .OrderBy(x => x.Id)
            .Skip(distribuicoesPrimeiraExecucao)
            .ToListAsync();

        Assert.Single(distribuicoesSegundaExecucao);
        Assert.Equal(ordemOriginal.Id, distribuicoesSegundaExecucao[0].OrdemCompraId);
        Assert.Equal(2, distribuicoesSegundaExecucao[0].Quantidade);

        var ordemAposSegundaExecucao = await db.OrdensCompra.SingleAsync();
        Assert.Equal(0, ordemAposSegundaExecucao.QuantidadeDisponivel);
    }

    [Fact]
    public async Task ExecuteAsync_UsesBasketVigenteNaDataReferencia_EmExecucaoManualRetroativa()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var cestaFevereiro = new CestasRecomendacao
        {
            Nome = "Top Five - Fevereiro",
            Ativa = false,
            DataCriacao = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc),
            DataDesativacao = new DateTime(2026, 3, 6, 9, 0, 0, DateTimeKind.Utc)
        };

        var cestaMarco = new CestasRecomendacao
        {
            Nome = "Top Five - Marco",
            Ativa = true,
            DataCriacao = new DateTime(2026, 3, 6, 9, 0, 0, DateTimeKind.Utc),
            DataDesativacao = null
        };

        db.CestasRecomendacao.AddRange(cestaFevereiro, cestaMarco);
        await db.SaveChangesAsync();

        db.ItensCesta.AddRange(
            new ItensCesta { CestaId = cestaFevereiro.Id, Ticker = "AGRO3", Percentual = 90m },
            new ItensCesta { CestaId = cestaFevereiro.Id, Ticker = "BAHI3", Percentual = 1m },
            new ItensCesta { CestaId = cestaFevereiro.Id, Ticker = "BBSE3", Percentual = 1m },
            new ItensCesta { CestaId = cestaFevereiro.Id, Ticker = "ITUB4", Percentual = 1m },
            new ItensCesta { CestaId = cestaFevereiro.Id, Ticker = "PETR4", Percentual = 7m },
            new ItensCesta { CestaId = cestaMarco.Id, Ticker = "AGRO3", Percentual = 20m },
            new ItensCesta { CestaId = cestaMarco.Id, Ticker = "BAHI3", Percentual = 20m },
            new ItensCesta { CestaId = cestaMarco.Id, Ticker = "BBSE3", Percentual = 20m },
            new ItensCesta { CestaId = cestaMarco.Id, Ticker = "ITUB4", Percentual = 20m },
            new ItensCesta { CestaId = cestaMarco.Id, Ticker = "PETR4", Percentual = 20m });

        db.Clientes.Add(new Clientes
        {
            Nome = "Cliente Retroativo",
            CPF = "12345678909",
            Email = "retroativo@teste.com",
            ValorMensal = 3000m,
            Ativo = true,
            DataAdesao = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc)
        });

        db.Cotacoes.AddRange(
            new Cotacoes { DataPregao = new DateTime(2026, 3, 5), Ticker = "AGRO3", PrecoAbertura = 10m, PrecoFechamento = 10m, PrecoMaximo = 10m, PrecoMinimo = 10m },
            new Cotacoes { DataPregao = new DateTime(2026, 3, 5), Ticker = "BAHI3", PrecoAbertura = 10m, PrecoFechamento = 10m, PrecoMaximo = 10m, PrecoMinimo = 10m },
            new Cotacoes { DataPregao = new DateTime(2026, 3, 5), Ticker = "BBSE3", PrecoAbertura = 10m, PrecoFechamento = 10m, PrecoMaximo = 10m, PrecoMinimo = 10m },
            new Cotacoes { DataPregao = new DateTime(2026, 3, 5), Ticker = "ITUB4", PrecoAbertura = 10m, PrecoFechamento = 10m, PrecoMaximo = 10m, PrecoMinimo = 10m },
            new Cotacoes { DataPregao = new DateTime(2026, 3, 5), Ticker = "PETR4", PrecoAbertura = 10m, PrecoFechamento = 10m, PrecoMaximo = 10m, PrecoMinimo = 10m });

        await db.SaveChangesAsync();

        var service = new ScheduledPurchaseEngine(
            new UnitOfWork(db),
            new AlwaysOpenTradingCalendar(),
            new NoOpFinanceEventsPublisher(),
            NullLogger<ScheduledPurchaseEngine>.Instance);

        await service.ExecuteAsync(new DateOnly(2026, 3, 5));

        var ordens = await db.OrdensCompra
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
    public async Task ExecuteAsync_UsesNovaBasketNaDataEmQueElaPassaAViger()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var cestaFevereiro = new CestasRecomendacao
        {
            Nome = "Top Five - Fevereiro",
            Ativa = false,
            DataCriacao = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc),
            DataDesativacao = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc)
        };

        var cestaMarco = new CestasRecomendacao
        {
            Nome = "Top Five - Marco",
            Ativa = true,
            DataCriacao = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
            DataDesativacao = null
        };

        db.CestasRecomendacao.AddRange(cestaFevereiro, cestaMarco);
        await db.SaveChangesAsync();

        db.ItensCesta.AddRange(
            new ItensCesta { CestaId = cestaFevereiro.Id, Ticker = "AGRO3", Percentual = 90m },
            new ItensCesta { CestaId = cestaFevereiro.Id, Ticker = "BAHI3", Percentual = 1m },
            new ItensCesta { CestaId = cestaFevereiro.Id, Ticker = "BBSE3", Percentual = 1m },
            new ItensCesta { CestaId = cestaFevereiro.Id, Ticker = "ITUB4", Percentual = 1m },
            new ItensCesta { CestaId = cestaFevereiro.Id, Ticker = "PETR4", Percentual = 7m },
            new ItensCesta { CestaId = cestaMarco.Id, Ticker = "AGRO3", Percentual = 20m },
            new ItensCesta { CestaId = cestaMarco.Id, Ticker = "BAHI3", Percentual = 20m },
            new ItensCesta { CestaId = cestaMarco.Id, Ticker = "BBSE3", Percentual = 20m },
            new ItensCesta { CestaId = cestaMarco.Id, Ticker = "ITUB4", Percentual = 20m },
            new ItensCesta { CestaId = cestaMarco.Id, Ticker = "PETR4", Percentual = 20m });

        db.Clientes.Add(new Clientes
        {
            Nome = "Cliente Vigencia",
            CPF = "12345678910",
            Email = "vigencia@teste.com",
            ValorMensal = 3000m,
            Ativo = true,
            DataAdesao = new DateTime(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc)
        });

        db.Cotacoes.AddRange(
            new Cotacoes { DataPregao = new DateTime(2026, 3, 1), Ticker = "AGRO3", PrecoAbertura = 10m, PrecoFechamento = 10m, PrecoMaximo = 10m, PrecoMinimo = 10m },
            new Cotacoes { DataPregao = new DateTime(2026, 3, 1), Ticker = "BAHI3", PrecoAbertura = 10m, PrecoFechamento = 10m, PrecoMaximo = 10m, PrecoMinimo = 10m },
            new Cotacoes { DataPregao = new DateTime(2026, 3, 1), Ticker = "BBSE3", PrecoAbertura = 10m, PrecoFechamento = 10m, PrecoMaximo = 10m, PrecoMinimo = 10m },
            new Cotacoes { DataPregao = new DateTime(2026, 3, 1), Ticker = "ITUB4", PrecoAbertura = 10m, PrecoFechamento = 10m, PrecoMaximo = 10m, PrecoMinimo = 10m },
            new Cotacoes { DataPregao = new DateTime(2026, 3, 1), Ticker = "PETR4", PrecoAbertura = 10m, PrecoFechamento = 10m, PrecoMaximo = 10m, PrecoMinimo = 10m });

        await db.SaveChangesAsync();

        var service = new ScheduledPurchaseEngine(
            new UnitOfWork(db),
            new AlwaysOpenTradingCalendar(),
            new NoOpFinanceEventsPublisher(),
            NullLogger<ScheduledPurchaseEngine>.Instance);

        await service.ExecuteAsync(new DateOnly(2026, 3, 1));

        var ordens = await db.OrdensCompra
            .OrderBy(x => x.Ticker)
            .ToListAsync();

        Assert.Equal(5, ordens.Count);
        Assert.All(ordens, ordem => Assert.Equal(20, ordem.Quantidade));
        Assert.All(ordens, ordem => Assert.EndsWith("F", ordem.Ticker));
        Assert.DoesNotContain(ordens, x => x.Ticker == "AGRO3F" && x.Quantidade == 90);
    }

    private sealed class AlwaysOpenTradingCalendar : ITradingCalendar
    {
        public bool IsBusinessDay(DateOnly date) => true;

        public DateOnly NextBusinessDay(DateOnly date) => date;

        public DateOnly ResolveRunDate(DateOnly baseDate) => baseDate;

        public bool IsPurchaseDate(DateOnly date) => true;
    }

    private sealed class ClosedTradingCalendar : ITradingCalendar
    {
        public bool IsBusinessDay(DateOnly date) => false;

        public DateOnly NextBusinessDay(DateOnly date) => date;

        public DateOnly ResolveRunDate(DateOnly baseDate) => baseDate;

        public bool IsPurchaseDate(DateOnly date) => false;
    }

    private sealed class NoOpFinanceEventsPublisher : IFinanceEventsPublisher
    {
        public Task PublishIrDedoDuroAsync(EventosIR evt, string cpf, string ticker, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task PublishIrVendaAsync(EventosIR evt, string cpf, IrVendaKafkaPayload payload, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class SpyFinanceEventsPublisher : IFinanceEventsPublisher
    {
        public List<(EventosIR Event, string Cpf, string Ticker)> DedoDuroPublications { get; } = [];

        public Task PublishIrDedoDuroAsync(EventosIR evt, string cpf, string ticker, CancellationToken ct = default)
        {
            DedoDuroPublications.Add((evt, cpf, ticker));
            return Task.CompletedTask;
        }

        public Task PublishIrVendaAsync(EventosIR evt, string cpf, IrVendaKafkaPayload payload, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
