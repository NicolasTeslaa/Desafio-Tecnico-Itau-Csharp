using ClassLibrary.Contracts.DTOs.Clientes;
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
using ScheduledPurchaseEngineService.Repositories;
using ScheduledPurchaseEngineService.Services;

namespace ScheduledPurchaseEngineService.Tests;

public sealed class ClientServiceTests
{
    [Fact]
    public async Task AdesaoProdutoAsync_CriaContaFilhote_CustodiasIniciais_EHistoricoValor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var cesta = new CestasRecomendacao
        {
            Nome = "Top Five Atual",
            Ativa = true,
            DataCriacao = DateTime.UtcNow
        };

        db.CestasRecomendacao.Add(cesta);
        await db.SaveChangesAsync();

        db.ItensCesta.AddRange(
            new ItensCesta { CestaId = cesta.Id, Ticker = "PETR4", Percentual = 20m },
            new ItensCesta { CestaId = cesta.Id, Ticker = "VALE3", Percentual = 20m },
            new ItensCesta { CestaId = cesta.Id, Ticker = "ITUB4", Percentual = 20m },
            new ItensCesta { CestaId = cesta.Id, Ticker = "ABEV3", Percentual = 20m },
            new ItensCesta { CestaId = cesta.Id, Ticker = "RENT3", Percentual = 20m });

        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new ClientService(
            uow,
            new ClientesRepository(uow.Repository<Clientes>()),
            new ClienteValorMensalHistoricoRepository(uow.Repository<ClienteValorMensalHistorico>()),
            NullLogger<ClientService>.Instance);

        var result = await service.AdesaoProdutoAsync(
            new AdesaoClienteRequest("Cliente Novo", "123.456.789-09", "cliente@teste.com", 300m),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Ok!.Ativo);
        Assert.StartsWith("FLH-", result.Ok.ContaGrafica.NumeroConta);

        var cliente = await db.Clientes.SingleAsync();
        var custodias = await db.Custodias
            .Where(x => x.ContasGraficasId == result.Ok.ContaGrafica.Id)
            .OrderBy(x => x.Ticker)
            .ToListAsync();
        var historico = await db.Set<ClienteValorMensalHistorico>().SingleAsync();

        Assert.Equal("12345678909", cliente.CPF);
        Assert.Equal(5, custodias.Count);
        Assert.All(custodias, custodia =>
        {
            Assert.Equal(0, custodia.Quantidade);
            Assert.Equal(0m, custodia.PrecoMedio);
        });
        Assert.Equal(0m, historico.ValorAnterior);
        Assert.Equal(300m, historico.ValorNovo);
    }

    [Fact]
    public async Task AlterarValorMensalAsync_PersisteHistorico_ParaProximaExecucao()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Clientes.Add(new Clientes
        {
            Nome = "Cliente Historico",
            CPF = "12345678909",
            Email = "historico@teste.com",
            ValorMensal = 300m,
            Ativo = true,
            DataAdesao = DateTime.UtcNow.AddMonths(-1)
        });
        await db.SaveChangesAsync();

        var cliente = await db.Clientes.SingleAsync();
        db.Set<ClienteValorMensalHistorico>().Add(new ClienteValorMensalHistorico
        {
            ClienteId = cliente.Id,
            ValorAnterior = 0m,
            ValorNovo = 300m,
            DataAlteracaoUtc = cliente.DataAdesao
        });
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new ClientService(
            uow,
            new ClientesRepository(uow.Repository<Clientes>()),
            new ClienteValorMensalHistoricoRepository(uow.Repository<ClienteValorMensalHistorico>()),
            NullLogger<ClientService>.Instance);

        var result = await service.AlterarValorMensalAsync(
            cliente.Id,
            new AlterarValorMensalRequest(450m),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(300m, result.Ok!.ValorMensalAnterior);
        Assert.Equal(450m, result.Ok.ValorMensalNovo);

        var historicos = await db.Set<ClienteValorMensalHistorico>()
            .Where(x => x.ClienteId == cliente.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, historicos.Count);
        Assert.Equal(300m, historicos[^1].ValorAnterior);
        Assert.Equal(450m, historicos[^1].ValorNovo);
    }

    [Fact]
    public async Task ConsultarCarteiraAsync_RetornaPlEComposicaoConformeRentabilidadeDaCarteira()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var cliente = new Clientes
        {
            Nome = "Cliente Carteira",
            CPF = "12345678909",
            Email = "carteira@teste.com",
            ValorMensal = 300m,
            Ativo = false,
            DataAdesao = DateTime.UtcNow.AddMonths(-2)
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var conta = new ContasGraficas
        {
            ClienteId = cliente.Id,
            NumeroConta = $"FLH-{cliente.Id:D6}",
            Tipo = TipoConta.Filhote,
            DataCriacao = cliente.DataAdesao
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
                DataUltimaAtualizacao = DateTime.UtcNow
            },
            new Custodias
            {
                ContasGraficasId = conta.Id,
                Ticker = "VALE3",
                Quantidade = 5,
                PrecoMedio = 20m,
                DataUltimaAtualizacao = DateTime.UtcNow
            });

        db.Cotacoes.AddRange(
            CreateCotacao("PETR4", DateOnly.FromDateTime(DateTime.UtcNow), 12m),
            CreateCotacao("VALE3", DateOnly.FromDateTime(DateTime.UtcNow), 18m));

        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new ClientService(
            uow,
            new ClientesRepository(uow.Repository<Clientes>()),
            new ClienteValorMensalHistoricoRepository(uow.Repository<ClienteValorMensalHistorico>()),
            NullLogger<ClientService>.Instance);

        var result = await service.ConsultarCarteiraAsync(cliente.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Ok!.Ativos.Count);
        Assert.Equal(200m, result.Ok.Resumo.ValorTotalInvestido);
        Assert.Equal(210m, result.Ok.Resumo.ValorAtualCarteira);
        Assert.Equal(10m, result.Ok.Resumo.PlTotal);
        Assert.Equal(5m, result.Ok.Resumo.RentabilidadePercentual);

        var ativoPrincipal = result.Ok.Ativos.First();
        Assert.Equal("PETR4", ativoPrincipal.Ticker);
        Assert.Equal(57.14m, ativoPrincipal.ComposicaoCarteira);
        Assert.Equal(20m, ativoPrincipal.PlPercentual);
    }

    [Fact]
    public async Task ConsultarRentabilidadeAsync_BuildsHistoricalEvolution_FromDistributionsAndHistoricalQuotes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        var firstRunDate = ResolveRunDate(new DateOnly(previousMonthStart.Year, previousMonthStart.Month, 5));
        var secondRunDate = ResolveRunDate(new DateOnly(previousMonthStart.Year, previousMonthStart.Month, 15));

        var cliente = new Clientes
        {
            Nome = "Cliente Evolucao",
            CPF = "12345678909",
            Email = "evolucao@teste.com",
            ValorMensal = 300m,
            Ativo = true,
            DataAdesao = previousMonthStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var contaFilhote = new ContasGraficas
        {
            ClienteId = cliente.Id,
            NumeroConta = $"FLH-{cliente.Id:D6}",
            Tipo = TipoConta.Filhote,
            DataCriacao = cliente.DataAdesao
        };

        var contaMaster = new ContasGraficas
        {
            ClienteId = cliente.Id,
            NumeroConta = "MST-TESTE",
            Tipo = TipoConta.Master,
            DataCriacao = cliente.DataAdesao
        };

        db.ContasGraficas.AddRange(contaFilhote, contaMaster);
        await db.SaveChangesAsync();

        var custodiaFilhote = new Custodias
        {
            ContasGraficasId = contaFilhote.Id,
            Ticker = "PETR4",
            Quantidade = 20,
            PrecoMedio = 11m,
            DataUltimaAtualizacao = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };

        db.Custodias.Add(custodiaFilhote);
        await db.SaveChangesAsync();

        var primeiraOrdem = new OrdensCompra
        {
            ContaMasterId = contaMaster.Id,
            Ticker = "PETR4F",
            Quantidade = 10,
            QuantidadeDisponivel = 0,
            PrecoUnitario = 10m,
            TipoMercado = TipoMercado.FRACIONARIO,
            DataExecucao = firstRunDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };

        var segundaOrdem = new OrdensCompra
        {
            ContaMasterId = contaMaster.Id,
            Ticker = "PETR4F",
            Quantidade = 10,
            QuantidadeDisponivel = 0,
            PrecoUnitario = 12m,
            TipoMercado = TipoMercado.FRACIONARIO,
            DataExecucao = secondRunDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };

        db.OrdensCompra.AddRange(primeiraOrdem, segundaOrdem);
        await db.SaveChangesAsync();

        db.Distribuicoes.AddRange(
            new Distribuicoes
            {
                OrdemCompraId = primeiraOrdem.Id,
                CustodiaFilhoteId = custodiaFilhote.Id,
                Ticker = "PETR4",
                Quantidade = 10,
                PrecoUnitario = 10m,
                Valor = 100m,
                DataDistribuicao = firstRunDate.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc)
            },
            new Distribuicoes
            {
                OrdemCompraId = segundaOrdem.Id,
                CustodiaFilhoteId = custodiaFilhote.Id,
                Ticker = "PETR4",
                Quantidade = 10,
                PrecoUnitario = 12m,
                Valor = 120m,
                DataDistribuicao = secondRunDate.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc)
            });

        db.Cotacoes.AddRange(
            CreateCotacao("PETR4", firstRunDate, 10m),
            CreateCotacao("PETR4", secondRunDate, 12m),
            CreateCotacao("PETR4", ResolveRunDate(new DateOnly(previousMonthStart.Year, previousMonthStart.Month, 25)), 14m),
            CreateCotacao("PETR4", ResolveRunDate(new DateOnly(currentMonthStart.Year, currentMonthStart.Month, 5)), 15m));

        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new ClientService(
            uow,
            new ClientesRepository(uow.Repository<Clientes>()),
            new ClienteValorMensalHistoricoRepository(uow.Repository<ClienteValorMensalHistorico>()),
            NullLogger<ClientService>.Instance);

        var result = await service.ConsultarRentabilidadeAsync(cliente.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var evolucao = result.Ok!.EvolucaoCarteira.ToDictionary(x => x.Data);
        Assert.Equal(100m, evolucao[firstRunDate].ValorInvestido);
        Assert.Equal(100m, evolucao[firstRunDate].ValorCarteira);
        Assert.Equal(0m, evolucao[firstRunDate].Rentabilidade);

        Assert.Equal(200m, evolucao[secondRunDate].ValorInvestido);
        Assert.Equal(240m, evolucao[secondRunDate].ValorCarteira);
        Assert.Equal(20m, evolucao[secondRunDate].Rentabilidade);
    }

    [Fact]
    public async Task ConsultarRentabilidadeAsync_BuildsHistoricalEvolution_WithRebalanceMovements()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        var firstRunDate = ResolveRunDate(new DateOnly(previousMonthStart.Year, previousMonthStart.Month, 5));
        var secondRunDate = ResolveRunDate(new DateOnly(previousMonthStart.Year, previousMonthStart.Month, 15));
        var thirdRunDate = ResolveRunDate(new DateOnly(previousMonthStart.Year, previousMonthStart.Month, 25));
        var rebalanceDate = ResolveRunDate(new DateOnly(currentMonthStart.Year, currentMonthStart.Month, 5));

        var cliente = new Clientes
        {
            Nome = "Cliente Rebalanceado",
            CPF = "98765432100",
            Email = "rebalanceado@teste.com",
            ValorMensal = 300m,
            Ativo = true,
            DataAdesao = previousMonthStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        var contaFilhote = new ContasGraficas
        {
            ClienteId = cliente.Id,
            NumeroConta = $"FLH-{cliente.Id:D6}",
            Tipo = TipoConta.Filhote,
            DataCriacao = cliente.DataAdesao
        };

        var contaMaster = new ContasGraficas
        {
            ClienteId = cliente.Id,
            NumeroConta = "MST-REBAL",
            Tipo = TipoConta.Master,
            DataCriacao = cliente.DataAdesao
        };

        db.ContasGraficas.AddRange(contaFilhote, contaMaster);
        await db.SaveChangesAsync();

        var custodiaPetr = new Custodias
        {
            ContasGraficasId = contaFilhote.Id,
            Ticker = "PETR4",
            Quantidade = 25,
            PrecoMedio = 12m,
            DataUltimaAtualizacao = rebalanceDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };

        var custodiaVale = new Custodias
        {
            ContasGraficasId = contaFilhote.Id,
            Ticker = "VALE3",
            Quantidade = 3,
            PrecoMedio = 20m,
            DataUltimaAtualizacao = rebalanceDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };

        db.Custodias.AddRange(custodiaPetr, custodiaVale);
        await db.SaveChangesAsync();

        var primeiraOrdem = new OrdensCompra
        {
            ContaMasterId = contaMaster.Id,
            Ticker = "PETR4F",
            Quantidade = 10,
            QuantidadeDisponivel = 0,
            PrecoUnitario = 10m,
            TipoMercado = TipoMercado.FRACIONARIO,
            DataExecucao = firstRunDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };

        var segundaOrdem = new OrdensCompra
        {
            ContaMasterId = contaMaster.Id,
            Ticker = "PETR4F",
            Quantidade = 10,
            QuantidadeDisponivel = 0,
            PrecoUnitario = 12m,
            TipoMercado = TipoMercado.FRACIONARIO,
            DataExecucao = secondRunDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };

        var terceiraOrdem = new OrdensCompra
        {
            ContaMasterId = contaMaster.Id,
            Ticker = "PETR4F",
            Quantidade = 10,
            QuantidadeDisponivel = 0,
            PrecoUnitario = 14m,
            TipoMercado = TipoMercado.FRACIONARIO,
            DataExecucao = thirdRunDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };

        db.OrdensCompra.AddRange(primeiraOrdem, segundaOrdem, terceiraOrdem);
        await db.SaveChangesAsync();

        db.Distribuicoes.AddRange(
            new Distribuicoes
            {
                OrdemCompraId = primeiraOrdem.Id,
                CustodiaFilhoteId = custodiaPetr.Id,
                Ticker = "PETR4",
                Quantidade = 10,
                PrecoUnitario = 10m,
                Valor = 100m,
                DataDistribuicao = firstRunDate.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc)
            },
            new Distribuicoes
            {
                OrdemCompraId = segundaOrdem.Id,
                CustodiaFilhoteId = custodiaPetr.Id,
                Ticker = "PETR4",
                Quantidade = 10,
                PrecoUnitario = 12m,
                Valor = 120m,
                DataDistribuicao = secondRunDate.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc)
            },
            new Distribuicoes
            {
                OrdemCompraId = terceiraOrdem.Id,
                CustodiaFilhoteId = custodiaPetr.Id,
                Ticker = "PETR4",
                Quantidade = 10,
                PrecoUnitario = 14m,
                Valor = 140m,
                DataDistribuicao = thirdRunDate.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc)
            });

        db.Rebalanceamentos.AddRange(
            new Rebalanceamentos
            {
                ClienteId = cliente.Id,
                TickerVendido = "PETR4",
                TickerComprado = "CAIXA",
                QuantidadeVendida = 5,
                PrecoUnitarioVenda = 15m,
                ValorVenda = 75m,
                DataRebalanceamento = rebalanceDate.ToDateTime(new TimeOnly(11, 0), DateTimeKind.Utc)
            },
            new Rebalanceamentos
            {
                ClienteId = cliente.Id,
                TickerVendido = "CAIXA",
                TickerComprado = "VALE3",
                QuantidadeComprada = 3,
                PrecoUnitarioCompra = 20m,
                ValorVenda = 0m,
                DataRebalanceamento = rebalanceDate.ToDateTime(new TimeOnly(11, 5), DateTimeKind.Utc)
            });

        db.Cotacoes.AddRange(
            CreateCotacao("PETR4", firstRunDate, 10m),
            CreateCotacao("PETR4", secondRunDate, 12m),
            CreateCotacao("PETR4", thirdRunDate, 14m),
            CreateCotacao("PETR4", rebalanceDate, 15m),
            CreateCotacao("VALE3", rebalanceDate, 22m));

        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var service = new ClientService(
            uow,
            new ClientesRepository(uow.Repository<Clientes>()),
            new ClienteValorMensalHistoricoRepository(uow.Repository<ClienteValorMensalHistorico>()),
            NullLogger<ClientService>.Instance);

        var result = await service.ConsultarRentabilidadeAsync(cliente.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var evolucao = result.Ok!.EvolucaoCarteira.ToDictionary(x => x.Data);
        Assert.Equal(100m, evolucao[firstRunDate].ValorInvestido);
        Assert.Equal(100m, evolucao[firstRunDate].ValorCarteira);
        Assert.Equal(0m, evolucao[firstRunDate].Rentabilidade);

        Assert.Equal(200m, evolucao[secondRunDate].ValorInvestido);
        Assert.Equal(240m, evolucao[secondRunDate].ValorCarteira);
        Assert.Equal(20m, evolucao[secondRunDate].Rentabilidade);

        Assert.Equal(300m, evolucao[thirdRunDate].ValorInvestido);
        Assert.Equal(420m, evolucao[thirdRunDate].ValorCarteira);
        Assert.Equal(40m, evolucao[thirdRunDate].Rentabilidade);

        Assert.Equal(400m, evolucao[rebalanceDate].ValorInvestido);
        Assert.Equal(441m, evolucao[rebalanceDate].ValorCarteira);
        Assert.Equal(10.25m, evolucao[rebalanceDate].Rentabilidade);
    }

    private static Cotacoes CreateCotacao(string ticker, DateOnly data, decimal fechamento)
        => new()
        {
            DataPregao = data.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Ticker = ticker,
            PrecoAbertura = fechamento,
            PrecoFechamento = fechamento,
            PrecoMaximo = fechamento,
            PrecoMinimo = fechamento
        };

    private static DateOnly ResolveRunDate(DateOnly baseDate)
    {
        var date = baseDate;
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return date;
    }
}
