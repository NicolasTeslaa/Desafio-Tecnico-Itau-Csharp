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

    private sealed class NoOpFinanceEventsPublisher : IFinanceEventsPublisher
    {
        public Task PublishIrDedoDuroAsync(EventosIR evt, string cpf, string ticker, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task PublishIrVendaAsync(EventosIR evt, string cpf, string ticker, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
