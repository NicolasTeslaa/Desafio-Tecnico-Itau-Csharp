using ClassLibrary.Domain.Entities;
using ClassLibrary.Domain.Entities.Cestas;
using ClassLibrary.Domain.Entities.Clientes;
using ClassLibrary.Domain.Entities.CompraDistribuicao;
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

public sealed class RebalanceServiceTests
{
    [Fact]
    public async Task RebalanceByDriftAsync_Rebalances_WhenPortfolioDeviationExceedsThreshold()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var agora = DateTime.UtcNow;

        var cesta = new CestasRecomendacao
        {
            Nome = "Top Five Atual",
            Ativa = true,
            DataCriacao = agora
        };

        db.CestasRecomendacao.Add(cesta);
        await db.SaveChangesAsync();

        db.ItensCesta.AddRange(
            new ItensCesta { CestaId = cesta.Id, Ticker = "PETR4", Percentual = 50m },
            new ItensCesta { CestaId = cesta.Id, Ticker = "VALE3", Percentual = 50m });

        var cliente = new Clientes
        {
            Nome = "Cliente Drift",
            CPF = "12345678909",
            Email = "drift@teste.com",
            ValorMensal = 3000m,
            Ativo = true,
            DataAdesao = agora.AddMonths(-1)
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var conta = new ContasGraficas
        {
            ClienteId = cliente.Id,
            NumeroConta = "FLH-DRIFT",
            Tipo = TipoConta.Filhote,
            DataCriacao = agora
        };

        db.ContasGraficas.Add(conta);
        await db.SaveChangesAsync();

        db.Custodias.AddRange(
            new Custodias
            {
                ContasGraficasId = conta.Id,
                Ticker = "PETR4",
                Quantidade = 30,
                PrecoMedio = 8m,
                DataUltimaAtualizacao = agora
            },
            new Custodias
            {
                ContasGraficasId = conta.Id,
                Ticker = "VALE3",
                Quantidade = 10,
                PrecoMedio = 8m,
                DataUltimaAtualizacao = agora
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
                DataPregao = agora.Date,
                Ticker = "VALE3",
                PrecoAbertura = 10m,
                PrecoFechamento = 10m,
                PrecoMaximo = 10m,
                PrecoMinimo = 10m
            });

        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new RebalanceService(
            uow,
            new NoOpFinanceEventsPublisher(),
            NullLogger<RebalanceService>.Instance);

        var result = await service.RebalanceByDriftAsync(5m, CancellationToken.None);

        Assert.Equal((1, 1), result);

        var custodiasAtualizadas = await db.Custodias
            .Where(x => x.ContasGraficasId == conta.Id)
            .OrderBy(x => x.Ticker)
            .ToListAsync();

        Assert.Equal(20, custodiasAtualizadas.Single(x => x.Ticker == "PETR4").Quantidade);
        Assert.Equal(20, custodiasAtualizadas.Single(x => x.Ticker == "VALE3").Quantidade);

        var rebalanceamentos = await db.Rebalanceamentos
            .Where(x => x.ClienteId == cliente.Id)
            .ToListAsync();

        Assert.Contains(rebalanceamentos, x => x.TickerVendido == "PETR4" && x.QuantidadeVendida == 10);
        Assert.Contains(rebalanceamentos, x => x.TickerComprado == "VALE3" && x.QuantidadeComprada == 10);
    }

    [Fact]
    public async Task RebalanceByBasketChangeAsync_Rebalances_WhenOnlyPercentagesChange()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var agora = DateTime.UtcNow;

        var cestaAnterior = new CestasRecomendacao
        {
            Nome = "Top Five A",
            Ativa = false,
            DataCriacao = agora.AddDays(-1),
            DataDesativacao = agora
        };

        var cestaNova = new CestasRecomendacao
        {
            Nome = "Top Five B",
            Ativa = true,
            DataCriacao = agora
        };

        db.CestasRecomendacao.AddRange(cestaAnterior, cestaNova);
        await db.SaveChangesAsync();

        db.ItensCesta.AddRange(
            new ItensCesta { CestaId = cestaAnterior.Id, Ticker = "PETR4", Percentual = 50m },
            new ItensCesta { CestaId = cestaAnterior.Id, Ticker = "VALE3", Percentual = 50m },
            new ItensCesta { CestaId = cestaNova.Id, Ticker = "PETR4", Percentual = 70m },
            new ItensCesta { CestaId = cestaNova.Id, Ticker = "VALE3", Percentual = 30m });

        var cliente = new Clientes
        {
            Nome = "Cliente Teste",
            CPF = "12345678909",
            Email = "cliente@teste.com",
            ValorMensal = 3000m,
            Ativo = true,
            DataAdesao = agora
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var conta = new ContasGraficas
        {
            ClienteId = cliente.Id,
            NumeroConta = "FLH-000001",
            Tipo = TipoConta.Filhote,
            DataCriacao = agora
        };

        db.ContasGraficas.Add(conta);
        await db.SaveChangesAsync();

        db.Custodias.AddRange(
            new Custodias
            {
                ContasGraficasId = conta.Id,
                Ticker = "PETR4",
                Quantidade = 10,
                PrecoMedio = 10m,
                DataUltimaAtualizacao = agora
            },
            new Custodias
            {
                ContasGraficasId = conta.Id,
                Ticker = "VALE3",
                Quantidade = 10,
                PrecoMedio = 10m,
                DataUltimaAtualizacao = agora
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
                DataPregao = agora.Date,
                Ticker = "VALE3",
                PrecoAbertura = 10m,
                PrecoFechamento = 10m,
                PrecoMaximo = 10m,
                PrecoMinimo = 10m
            });

        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new RebalanceService(
            uow,
            new NoOpFinanceEventsPublisher(),
            NullLogger<RebalanceService>.Instance);

        var clientesProcessados = await service.RebalanceByBasketChangeAsync(cestaAnterior.Id, cestaNova.Id);

        Assert.Equal(1, clientesProcessados);

        var custodiasAtualizadas = await db.Custodias
            .Where(x => x.ContasGraficasId == conta.Id)
            .ToListAsync();

        var petr4 = custodiasAtualizadas.Single(x => x.Ticker == "PETR4");
        var vale3 = custodiasAtualizadas.Single(x => x.Ticker == "VALE3");

        Assert.Equal(14, petr4.Quantidade);
        Assert.Equal(6, vale3.Quantidade);

        var rebalanceamentos = await db.Rebalanceamentos
            .Where(x => x.ClienteId == cliente.Id)
            .ToListAsync();

        Assert.Contains(rebalanceamentos, x => x.TickerVendido == "VALE3");
    }

    [Fact]
    public async Task RebalanceByBasketChangeAsync_PublishesIrVenda_WithAggregatedKafkaPayload()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var agora = DateTime.UtcNow;

        var cestaAnterior = new CestasRecomendacao
        {
            Nome = "Top Five A",
            Ativa = false,
            DataCriacao = agora.AddDays(-1),
            DataDesativacao = agora
        };

        var cestaNova = new CestasRecomendacao
        {
            Nome = "Top Five B",
            Ativa = true,
            DataCriacao = agora
        };

        db.CestasRecomendacao.AddRange(cestaAnterior, cestaNova);
        await db.SaveChangesAsync();

        db.ItensCesta.AddRange(
            new ItensCesta { CestaId = cestaAnterior.Id, Ticker = "PETR4", Percentual = 100m },
            new ItensCesta { CestaId = cestaNova.Id, Ticker = "VALE3", Percentual = 100m });

        var cliente = new Clientes
        {
            Nome = "Cliente IR",
            CPF = "12345678909",
            Email = "cliente-ir@teste.com",
            ValorMensal = 3000m,
            Ativo = true,
            DataAdesao = agora
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var conta = new ContasGraficas
        {
            ClienteId = cliente.Id,
            NumeroConta = "FLH-000002",
            Tipo = TipoConta.Filhote,
            DataCriacao = agora
        };

        db.ContasGraficas.Add(conta);
        await db.SaveChangesAsync();

        db.Custodias.Add(new Custodias
        {
            ContasGraficasId = conta.Id,
            Ticker = "PETR4",
            Quantidade = 1000,
            PrecoMedio = 10m,
            DataUltimaAtualizacao = agora
        });

        db.Cotacoes.AddRange(
            new Cotacoes
            {
                DataPregao = agora.Date,
                Ticker = "PETR4",
                PrecoAbertura = 30m,
                PrecoFechamento = 30m,
                PrecoMaximo = 30m,
                PrecoMinimo = 30m
            },
            new Cotacoes
            {
                DataPregao = agora.Date,
                Ticker = "VALE3",
                PrecoAbertura = 30m,
                PrecoFechamento = 30m,
                PrecoMaximo = 30m,
                PrecoMinimo = 30m
            });

        await db.SaveChangesAsync();

        var publisher = new SpyFinanceEventsPublisher();
        var uow = new UnitOfWork(db);
        var service = new RebalanceService(
            uow,
            publisher,
            NullLogger<RebalanceService>.Instance);

        var clientesProcessados = await service.RebalanceByBasketChangeAsync(cestaAnterior.Id, cestaNova.Id);

        Assert.Equal(1, clientesProcessados);

        Assert.NotNull(publisher.PublishedIrVendaEvent);
        Assert.Equal(cliente.CPF, publisher.PublishedIrVendaCpf);

        var payload = Assert.IsType<IrVendaKafkaPayload>(publisher.PublishedIrVendaPayload);
        Assert.Equal($"{DateTime.UtcNow:yyyy-MM}", payload.MesReferencia);
        Assert.Equal(30000m, payload.TotalVendasMes);
        Assert.Equal(20000m, payload.LucroLiquido);
        Assert.Equal(0.20m, payload.Aliquota);
        Assert.Single(payload.Detalhes);

        var detalhe = payload.Detalhes.Single();
        Assert.Equal("PETR4", detalhe.Ticker);
        Assert.Equal(1000, detalhe.Quantidade);
        Assert.Equal(30m, detalhe.PrecoVenda);
        Assert.Equal(10m, detalhe.PrecoMedio);
        Assert.Equal(20000m, detalhe.Lucro);

        var evento = await db.EventosIR.SingleAsync();
        Assert.Equal(TipoIR.IR_Venda, evento.Tipo);
        Assert.Equal(20000m, evento.ValorBase);
        Assert.Equal(4000m, evento.ValorIR);
        Assert.True(evento.PublicadoKafka);
    }

    [Fact]
    public async Task RebalanceByBasketChangeAsync_PreservesZeroQuantityCustody_WhenHistoryReferencesExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var agora = DateTime.UtcNow;

        var cestaAnterior = new CestasRecomendacao
        {
            Nome = "Top Five A",
            Ativa = false,
            DataCriacao = agora.AddDays(-1),
            DataDesativacao = agora
        };

        var cestaNova = new CestasRecomendacao
        {
            Nome = "Top Five B",
            Ativa = true,
            DataCriacao = agora
        };

        db.CestasRecomendacao.AddRange(cestaAnterior, cestaNova);
        await db.SaveChangesAsync();

        db.ItensCesta.AddRange(
            new ItensCesta { CestaId = cestaAnterior.Id, Ticker = "PETR4", Percentual = 100m },
            new ItensCesta { CestaId = cestaNova.Id, Ticker = "VALE3", Percentual = 100m });

        var cliente = new Clientes
        {
            Nome = "Cliente Historico",
            CPF = "12345678912",
            Email = "historico@teste.com",
            ValorMensal = 3000m,
            Ativo = true,
            DataAdesao = agora
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var conta = new ContasGraficas
        {
            ClienteId = cliente.Id,
            NumeroConta = "FLH-000003",
            Tipo = TipoConta.Filhote,
            DataCriacao = agora
        };

        db.ContasGraficas.Add(conta);
        await db.SaveChangesAsync();

        var custodiaPetr4 = new Custodias
        {
            ContasGraficasId = conta.Id,
            Ticker = "PETR4",
            Quantidade = 100,
            PrecoMedio = 10m,
            DataUltimaAtualizacao = agora
        };

        db.Custodias.Add(custodiaPetr4);
        await db.SaveChangesAsync();

        var ordemMaster = new OrdensCompra
        {
            ContaMasterId = conta.Id,
            Ticker = "PETR4",
            Quantidade = 100,
            QuantidadeDisponivel = 0,
            PrecoUnitario = 10m,
            TipoMercado = TipoMercado.LOTE,
            DataExecucao = agora
        };

        db.OrdensCompra.Add(ordemMaster);
        await db.SaveChangesAsync();

        db.Distribuicoes.Add(new Distribuicoes
        {
            OrdemCompraId = ordemMaster.Id,
            CustodiaFilhoteId = custodiaPetr4.Id,
            Ticker = "PETR4",
            Quantidade = 100,
            PrecoUnitario = 10m,
            Valor = 1000m,
            DataDistribuicao = agora
        });

        db.Cotacoes.AddRange(
            new Cotacoes
            {
                DataPregao = agora.Date,
                Ticker = "PETR4",
                PrecoAbertura = 30m,
                PrecoFechamento = 30m,
                PrecoMaximo = 30m,
                PrecoMinimo = 30m
            },
            new Cotacoes
            {
                DataPregao = agora.Date,
                Ticker = "VALE3",
                PrecoAbertura = 30m,
                PrecoFechamento = 30m,
                PrecoMaximo = 30m,
                PrecoMinimo = 30m
            });

        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new RebalanceService(
            uow,
            new NoOpFinanceEventsPublisher(),
            NullLogger<RebalanceService>.Instance);

        var clientesProcessados = await service.RebalanceByBasketChangeAsync(cestaAnterior.Id, cestaNova.Id);

        Assert.Equal(1, clientesProcessados);

        var custodiaPetr4Atualizada = await db.Custodias.SingleAsync(x => x.Id == custodiaPetr4.Id);
        Assert.Equal(0, custodiaPetr4Atualizada.Quantidade);

        var distribuicaoHistorica = await db.Distribuicoes.SingleAsync();
        Assert.Equal(custodiaPetr4.Id, distribuicaoHistorica.CustodiaFilhoteId);

        var custodiaVale3 = await db.Custodias.SingleAsync(x => x.ContasGraficasId == conta.Id && x.Ticker == "VALE3");
        Assert.True(custodiaVale3.Quantidade > 0);
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
        public EventosIR? PublishedIrVendaEvent { get; private set; }
        public string? PublishedIrVendaCpf { get; private set; }
        public IrVendaKafkaPayload? PublishedIrVendaPayload { get; private set; }

        public Task PublishIrDedoDuroAsync(EventosIR evt, string cpf, string ticker, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task PublishIrVendaAsync(EventosIR evt, string cpf, IrVendaKafkaPayload payload, CancellationToken ct = default)
        {
            PublishedIrVendaEvent = evt;
            PublishedIrVendaCpf = cpf;
            PublishedIrVendaPayload = payload;
            return Task.CompletedTask;
        }
    }
}
