using ClassLibrary.Contracts.DTOs.Admin;
using ClassLibrary.Domain.Entities;
using ClassLibrary.Domain.Entities.Cestas;
using ClassLibrary.Domain.Entities.Clientes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ScheduledPurchaseEngineService.Data;
using ScheduledPurchaseEngineService.Interfaces;
using ScheduledPurchaseEngineService.Repositories;
using ScheduledPurchaseEngineService.Services;

namespace ScheduledPurchaseEngineService.Tests;

public sealed class AdminServiceTests
{
    [Fact]
    public async Task CadastrarOuAlterarCestaAsync_CadastraPrimeiraCesta_NormalizandoTickers_SemDispararRebalanceamento()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var agora = DateTime.UtcNow;
        db.Cotacoes.AddRange(
            CreateCotacao("PETR4", agora),
            CreateCotacao("VALE3", agora),
            CreateCotacao("ITUB4", agora),
            CreateCotacao("ABEV3", agora),
            CreateCotacao("RENT3", agora));
        await db.SaveChangesAsync();

        var rebalanceSpy = new RebalanceSpy();
        var service = new AdminService(
            new UnitOfWork(db),
            rebalanceSpy,
            NullLogger<AdminService>.Instance);

        var request = new CadastrarOuAlterarCestaRequest(
            Nome: "Top Five - Inicial",
            Itens:
            [
                new CestaItemRequest(" petr4 ", 20m),
                new CestaItemRequest("vale3", 20m),
                new CestaItemRequest("Itub4", 20m),
                new CestaItemRequest("abev3", 20m),
                new CestaItemRequest("rent3", 20m)
            ]);

        var result = await service.CadastrarOuAlterarCestaAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Ok!.RebalanceamentoDisparado);
        Assert.Null(result.Ok.CestaAnteriorDesativada);
        Assert.Equal(["ABEV3", "ITUB4", "PETR4", "RENT3", "VALE3"], result.Ok.Itens.Select(x => x.Ticker).OrderBy(x => x).ToArray());
        Assert.Null(rebalanceSpy.PreviousCestaId);
    }

    [Fact]
    public async Task EditarCestaAsync_CriaNovaVersao_E_DisparaRebalanceamento()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var agora = DateTime.UtcNow;
        var cestaAtiva = new CestasRecomendacao
        {
            Nome = "Top Five - Fevereiro 2026",
            Ativa = true,
            DataCriacao = agora.AddDays(-10)
        };

        db.CestasRecomendacao.Add(cestaAtiva);
        db.Clientes.Add(new Clientes
        {
            Nome = "Cliente Teste",
            CPF = "12345678909",
            Email = "cliente@teste.com",
            ValorMensal = 1000m,
            Ativo = true,
            DataAdesao = agora.AddMonths(-1)
        });

        await db.SaveChangesAsync();

        db.ItensCesta.AddRange(
            new ItensCesta { CestaId = cestaAtiva.Id, Ticker = "PETR4", Percentual = 30m },
            new ItensCesta { CestaId = cestaAtiva.Id, Ticker = "VALE3", Percentual = 25m },
            new ItensCesta { CestaId = cestaAtiva.Id, Ticker = "ITUB4", Percentual = 20m },
            new ItensCesta { CestaId = cestaAtiva.Id, Ticker = "BBDC4", Percentual = 15m },
            new ItensCesta { CestaId = cestaAtiva.Id, Ticker = "WEGE3", Percentual = 10m });

        db.Cotacoes.AddRange(
            CreateCotacao("PETR4", agora),
            CreateCotacao("VALE3", agora),
            CreateCotacao("ITUB4", agora),
            CreateCotacao("BBDC4", agora),
            CreateCotacao("WEGE3", agora),
            CreateCotacao("ABEV3", agora),
            CreateCotacao("RENT3", agora));

        await db.SaveChangesAsync();

        var rebalanceSpy = new RebalanceSpy();
        var service = new AdminService(
            new UnitOfWork(db),
            rebalanceSpy,
            NullLogger<AdminService>.Instance);

        var request = new CadastrarOuAlterarCestaRequest(
            Nome: "Top Five - Marco 2026",
            Itens:
            [
                new CestaItemRequest("PETR4", 25m),
                new CestaItemRequest("VALE3", 20m),
                new CestaItemRequest("ITUB4", 20m),
                new CestaItemRequest("ABEV3", 20m),
                new CestaItemRequest("RENT3", 15m)
            ]);

        var result = await service.EditarCestaAsync(cestaAtiva.Id, request, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var response = result.Ok!;
        Assert.NotEqual(cestaAtiva.Id, response.CestaId);
        Assert.True(response.RebalanceamentoDisparado);
        Assert.NotNull(response.CestaAnteriorDesativada);
        Assert.Equal(cestaAtiva.Id, response.CestaAnteriorDesativada!.CestaId);
        Assert.Equal(cestaAtiva.Id, rebalanceSpy.PreviousCestaId);
        Assert.Equal(response.CestaId, rebalanceSpy.NewCestaId);

        var cestaAntiga = await db.CestasRecomendacao.SingleAsync(x => x.Id == cestaAtiva.Id);
        var cestaNova = await db.CestasRecomendacao.SingleAsync(x => x.Id == response.CestaId);
        var itensNovos = await db.ItensCesta
            .Where(x => x.CestaId == cestaNova.Id)
            .OrderBy(x => x.Ticker)
            .ToListAsync();

        Assert.False(cestaAntiga.Ativa);
        Assert.NotNull(cestaAntiga.DataDesativacao);
        Assert.True(cestaNova.Ativa);
        Assert.Equal("Top Five - Marco 2026", cestaNova.Nome);
        Assert.Equal(5, itensNovos.Count);
        Assert.Equal(["ABEV3", "ITUB4", "PETR4", "RENT3", "VALE3"], itensNovos.Select(x => x.Ticker).ToArray());
    }

    [Fact]
    public async Task EditarCestaAsync_Rejeita_Alteracao_De_Cesta_Inativa()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var agora = DateTime.UtcNow;
        var cestaInativa = new CestasRecomendacao
        {
            Nome = "Top Five - Janeiro 2026",
            Ativa = false,
            DataCriacao = agora.AddDays(-20),
            DataDesativacao = agora.AddDays(-10)
        };

        var cestaAtiva = new CestasRecomendacao
        {
            Nome = "Top Five - Fevereiro 2026",
            Ativa = true,
            DataCriacao = agora.AddDays(-5)
        };

        db.CestasRecomendacao.AddRange(cestaInativa, cestaAtiva);
        db.Cotacoes.AddRange(
            CreateCotacao("PETR4", agora),
            CreateCotacao("VALE3", agora),
            CreateCotacao("ITUB4", agora),
            CreateCotacao("ABEV3", agora),
            CreateCotacao("RENT3", agora));

        await db.SaveChangesAsync();

        var rebalanceSpy = new RebalanceSpy();
        var service = new AdminService(
            new UnitOfWork(db),
            rebalanceSpy,
            NullLogger<AdminService>.Instance);

        var request = new CadastrarOuAlterarCestaRequest(
            Nome: "Top Five - Marco 2026",
            Itens:
            [
                new CestaItemRequest("PETR4", 20m),
                new CestaItemRequest("VALE3", 20m),
                new CestaItemRequest("ITUB4", 20m),
                new CestaItemRequest("ABEV3", 20m),
                new CestaItemRequest("RENT3", 20m)
            ]);

        var result = await service.EditarCestaAsync(cestaInativa.Id, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("CESTA_INATIVA", result.Err!.Codigo);
        Assert.Null(rebalanceSpy.PreviousCestaId);
        Assert.Equal(2, await db.CestasRecomendacao.CountAsync());
        Assert.Equal(1, await db.CestasRecomendacao.CountAsync(x => x.Ativa));
    }

    [Fact]
    public async Task CadastrarOuAlterarCestaAsync_Rejeita_Cesta_Com_Tickers_Repetidos()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var agora = DateTime.UtcNow;
        db.Cotacoes.AddRange(
            CreateCotacao("PETR4", agora),
            CreateCotacao("VALE3", agora),
            CreateCotacao("ITUB4", agora),
            CreateCotacao("ABEV3", agora));
        await db.SaveChangesAsync();

        var service = new AdminService(
            new UnitOfWork(db),
            new RebalanceSpy(),
            NullLogger<AdminService>.Instance);

        var request = new CadastrarOuAlterarCestaRequest(
            Nome: "Top Five - Invalida",
            Itens:
            [
                new CestaItemRequest("PETR4", 20m),
                new CestaItemRequest("PETR4", 20m),
                new CestaItemRequest("VALE3", 20m),
                new CestaItemRequest("ITUB4", 20m),
                new CestaItemRequest("ABEV3", 20m)
            ]);

        var result = await service.CadastrarOuAlterarCestaAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("QUANTIDADE_ATIVOS_INVALIDA", result.Err!.Codigo);
    }

    [Fact]
    public async Task RebalancearPorDesvioAsync_ValidaThresholdEDelegatesAoServico()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var serviceSemCesta = new AdminService(
            new UnitOfWork(db),
            new RebalanceSpy(),
            NullLogger<AdminService>.Instance);

        var invalid = await serviceSemCesta.RebalancearPorDesvioAsync(new RebalanceamentoDesvioRequest(0m), CancellationToken.None);
        Assert.False(invalid.IsSuccess);
        Assert.Equal("THRESHOLD_INVALIDO", invalid.Err!.Codigo);

        db.CestasRecomendacao.Add(new CestasRecomendacao
        {
            Nome = "Top Five Ativa",
            Ativa = true,
            DataCriacao = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var rebalanceSpy = new RebalanceSpy
        {
            DriftResult = (3, 1)
        };

        var service = new AdminService(
            new UnitOfWork(db),
            rebalanceSpy,
            NullLogger<AdminService>.Instance);

        var result = await service.RebalancearPorDesvioAsync(new RebalanceamentoDesvioRequest(5m), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(5m, rebalanceSpy.LastThresholdPercentual);
        Assert.Equal(3, result.Ok!.TotalClientesAvaliados);
        Assert.Equal(1, result.Ok.TotalClientesRebalanceados);
    }

    private static Cotacoes CreateCotacao(string ticker, DateTime data)
        => new()
        {
            DataPregao = data.Date,
            Ticker = ticker,
            PrecoAbertura = 10m,
            PrecoFechamento = 10m,
            PrecoMaximo = 10m,
            PrecoMinimo = 10m
        };

    private sealed class RebalanceSpy : IRebalanceService
    {
        public int? PreviousCestaId { get; private set; }
        public int? NewCestaId { get; private set; }
        public decimal? LastThresholdPercentual { get; private set; }
        public (int Evaluated, int Rebalanced) DriftResult { get; set; }

        public Task<int> RebalanceByBasketChangeAsync(int previousCestaId, int newCestaId, CancellationToken ct = default)
        {
            PreviousCestaId = previousCestaId;
            NewCestaId = newCestaId;
            return Task.FromResult(1);
        }

        public Task<(int Evaluated, int Rebalanced)> RebalanceByDriftAsync(decimal thresholdPercentual, CancellationToken ct = default)
        {
            LastThresholdPercentual = thresholdPercentual;
            return Task.FromResult(DriftResult);
        }
    }
}
