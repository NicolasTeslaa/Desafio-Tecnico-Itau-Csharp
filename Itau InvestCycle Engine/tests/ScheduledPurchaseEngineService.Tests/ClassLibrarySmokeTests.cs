using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Admin;
using ClassLibrary.Contracts.DTOs.Clientes;
using ClassLibrary.Contracts.DTOs.Motor;
using ClassLibrary.Domain.Entities;
using ClassLibrary.Domain.Entities.Cestas;
using ClassLibrary.Domain.Entities.Clientes;
using ClassLibrary.Domain.Entities.CompraDistribuicao;
using ClassLibrary.Domain.Entities.RebalanceamentoIR;
using Itau.InvestCycleEngine.Common.Auditing;
using Itau.InvestCycleEngine.Common.BaseTypes;
using Itau.InvestCycleEngine.Contracts.Common;
using Itau.InvestCycleEngine.Domain.Entities;
using Itau.InvestCycleEngine.Domain.Enums;
using Itau.InvestCycleEngine.Domain.ValueObjects;

namespace ScheduledPurchaseEngineService.Tests;

public sealed class ClassLibrarySmokeTests
{
    [Fact]
    public void ContractsAndResults_CanBeConstructed()
    {
        var pagedRequest = new PagedRequest(2, 10);
        var pagedResponse = new PagedResponse<string>(["PETR4"], 2, 10, 1);
        var apiError = new ApiError("erro", "CODIGO");
        var assetQty = new AssetQty("PETR4", 10);
        var distribution = new ClientDistributionSummary(1, "Cliente", 100m, [assetQty]);
        var order = new OrderSummary("PETR4", 10, 12m, TipoMercado.LOTE);
        var residual = new ResidualSummary("PETR4", 2);
        var priceRecord = new CotahistPriceRecord("PETR4", new DateOnly(2026, 3, 5), 10m, 11m, 9m, 10.5m, 1000m);
        var ingestDto = new CotacaoIngestDto(1, new DateTime(2026, 3, 5), "PETR4", 10m, 11m, 12m, 9m);
        var purchaseResult = new ScheduledPurchaseResult(DateTimeOffset.UtcNow, new DateOnly(2026, 3, 5), 1, 100m, [order], [distribution], [residual], 1);
        var success = Result<string, ApiError>.Success("ok");
        var failure = Result<string, ApiError>.Failure(apiError);

        Assert.Equal(2, pagedRequest.Page);
        Assert.Single(pagedResponse.Items);
        Assert.Equal("PETR4", assetQty.Ticker);
        Assert.Single(distribution.Assets);
        Assert.Equal(TipoMercado.LOTE, order.Market);
        Assert.Equal("PETR4", residual.Ticker);
        Assert.Equal("PETR4", priceRecord.Symbol);
        Assert.Equal("PETR4", ingestDto.Ticker);
        Assert.Equal(1, purchaseResult.TotalClients);
        Assert.True(success.IsSuccess);
        Assert.False(failure.IsSuccess);
    }

    [Fact]
    public void AdminClientAndMotorContracts_CanBeConstructed()
    {
        var adminRequest = new CadastrarOuAlterarCestaRequest("Top Five", [new CestaItemRequest("PETR4", 20m)]);
        var adminResponse = new CadastrarOuAlterarCestaResponse(
            1,
            "Top Five",
            true,
            DateTime.UtcNow,
            [new CestaItemResponse("PETR4", 20m)],
            new CestaAnteriorDesativadaResponse(0, "Anterior", DateTime.UtcNow),
            true,
            ["BBDC4"],
            ["RENT3"],
            "ok",
            [new AtivoPercentualAlteradoResponse("PETR4", 30m, 20m)]);
        var cestaAtual = new CestaAtualResponse(1, "Top Five", true, DateTime.UtcNow, [new CestaAtualItemResponse("PETR4", 20m, 35m)]);
        var historico = new HistoricoCestasResponse([new CestaHistoricoItemResponse(1, "Top Five", true, DateTime.UtcNow, null, [new CestaItemResponse("PETR4", 20m)])]);
        var tickers = new TickersDisponiveisResponse(["PETR4"]);
        var contaMaster = new ContaMasterCustodiaResponse(
            new ContaMasterInfoResponse(1, "MST-1", "MASTER"),
            [new CustodiaMasterItemResponse("PETR4", 2, 10m, 20m, "Residuo")],
            20m);
        var rebalanceamento = new RebalanceamentoDesvioResponse(10, 2, 5m, "ok");

        var adesao = new AdesaoClienteResponse(1, "Cliente", "12345678909", "c@teste.com", 300m, true, DateTime.UtcNow, new ContaGraficaResponse(1, "FLH-1", "FILHOTE", DateTime.UtcNow));
        var saida = new SaidaClienteResponse(1, "Cliente", false, DateTime.UtcNow, "ok");
        var alteracao = new AlterarValorMensalResponse(1, 300m, 450m, DateTime.UtcNow, "ok");
        var carteira = new ConsultarCarteiraResponse(1, "Cliente", "FLH-1", DateTime.UtcNow, new ResumoCarteiraResponse(100m, 110m, 10m, 10m), [new CarteiraAtivoResponse("PETR4", 10, 10m, 11m, 110m, 10m, 10m, 100m)]);
        var rentabilidade = new ConsultarRentabilidadeResponse(1, "Cliente", DateTime.UtcNow, new RentabilidadeResumoResponse(100m, 110m, 10m, 10m), [new HistoricoAporteResponse(new DateOnly(2026, 3, 5), 100m, "1/3")], [new EvolucaoCarteiraResponse(new DateOnly(2026, 3, 5), 110m, 100m, 10m)]);
        var exclusao = new ExcluirClienteResponse(1, "ok");
        var clientes = new ListarClientesResponse([new ClienteListaItemResponse(1, "Cliente", "12345678909", "c@teste.com", 300m, true, DateTime.UtcNow, "FLH-1")]);

        var executarCompra = new ExecutarCompraResponse(
            DateTime.UtcNow,
            1,
            300m,
            [new OrdemCompraResponse("PETR4", 10, [new DetalheOrdemCompraResponse("LOTE", "PETR4", 10)], 10m, 100m)],
            [new DistribuicaoClienteResponse(1, "Cliente", 100m, [new AtivoDistribuicaoResponse("PETR4", 10)])],
            [new ResiduoCustodiaMasterResponse("PETR4", 2)],
            1,
            "ok");
        var historicoMotor = new MotorHistoricoResponse([new MotorHistoricoItemResponse(DateTime.UtcNow, 1, 100m, DateTime.UtcNow)]);

        Assert.Equal("Top Five", adminRequest.Nome);
        Assert.True(adminResponse.RebalanceamentoDisparado);
        Assert.Single(cestaAtual.Itens);
        Assert.Single(historico.Cestas);
        Assert.Single(tickers.Tickers);
        Assert.Equal(20m, contaMaster.ValorTotalResiduo);
        Assert.Equal(5m, rebalanceamento.ThresholdPercentual);
        Assert.True(adesao.Ativo);
        Assert.False(saida.Ativo);
        Assert.Equal(450m, alteracao.ValorMensalNovo);
        Assert.Single(carteira.Ativos);
        Assert.Single(rentabilidade.HistoricoAportes);
        Assert.Equal("ok", exclusao.Mensagem);
        Assert.Single(clientes.Clientes);
        Assert.Single(executarCompra.OrdensCompra);
        Assert.Single(historicoMotor.Compras);
    }

    [Fact]
    public void EntitiesAndBaseTypes_ExposeExpectedBehavior()
    {
        var id = Guid.NewGuid();
        var asset = new Asset(id, " petr4 ", AssetType.Stock, CurrencyCode.BRL);
        var account = new InvestmentAccount(Guid.NewGuid(), Guid.NewGuid(), "Itau", "ACC-1");
        var schedule = new PlanSchedule(FrequencyType.Weekly, 0, new TimeSpan(10, 0, 0), DayOfWeek.Monday);
        var money = new Money(100m, CurrencyCode.BRL);
        var plan = new ProgrammedPurchasePlan(Guid.NewGuid(), account.Id, asset.Id, money, schedule, DateTime.UtcNow.AddDays(1));
        var execution = new PlanExecution(Guid.NewGuid(), plan.Id, DateTime.UtcNow, ExecutionStatus.Executed);
        var cotacao = new Cotacoes { Id = 1, Ticker = "PETR4", DataPregao = new DateTime(2026, 3, 5), PrecoFechamento = 10m, PrecoAbertura = 9m, PrecoMaximo = 11m, PrecoMinimo = 8m };
        var ingestao = new IngestaoJob { Id = Guid.NewGuid(), File = "COTAHIST.TXT", StoredPath = "cotacoes/COTAHIST.TXT", Status = "QUEUED", CreatedAtUtc = DateTime.UtcNow };
        var motor = new MotorExecucao { Id = 1, DataReferencia = new DateTime(2026, 3, 5), Status = "PENDING", DataInicioUtc = DateTime.UtcNow };
        var motorHistorico = new MotorExecucaoHistorico { Id = 1, DataReferencia = new DateTime(2026, 3, 5), TotalClientes = 1, TotalConsolidado = 100m, DataHoraUtc = DateTime.UtcNow };
        var cesta = new CestasRecomendacao { Id = 1, Nome = "Top Five", Ativa = true, DataCriacao = DateTime.UtcNow };
        var itemCesta = new ItensCesta { Id = 1, CestaId = 1, Ticker = "PETR4", Percentual = 20m };
        var cliente = new Clientes { Id = 1, Nome = "Cliente", CPF = "12345678909", Email = "c@teste.com", ValorMensal = 300m, Ativo = true, DataAdesao = DateTime.UtcNow };
        var historicoValor = new ClienteValorMensalHistorico { Id = 1, ClienteId = 1, ValorAnterior = 300m, ValorNovo = 450m, DataAlteracaoUtc = DateTime.UtcNow };
        var contaGrafica = new ContasGraficas { Id = 1, ClienteId = 1, NumeroConta = "FLH-1", Tipo = TipoConta.Filhote, DataCriacao = DateTime.UtcNow };
        var custodia = new Custodias { Id = 1, ContasGraficasId = 1, Ticker = "PETR4", Quantidade = 10, PrecoMedio = 10m, DataUltimaAtualizacao = DateTime.UtcNow };
        var ordem = new OrdensCompra { Id = 1, ContaMasterId = 1, Ticker = "PETR4", Quantidade = 10, QuantidadeDisponivel = 2, PrecoUnitario = 10m, TipoMercado = TipoMercado.LOTE, DataExecucao = DateTime.UtcNow };
        var distribuicao = new Distribuicoes { Id = 1, OrdemCompraId = 1, CustodiaFilhoteId = 1, Ticker = "PETR4", Quantidade = 10, PrecoUnitario = 10m, Valor = 100m, DataDistribuicao = DateTime.UtcNow };
        var eventoIr = new EventosIR { Id = 1, ClienteId = 1, Tipo = TipoIR.DEDO_DURO, ValorBase = 100m, ValorIR = 0.01m, PublicadoKafka = false, DataEvento = DateTime.UtcNow };
        var rebalanceamento = new Rebalanceamentos { Id = 1, ClienteId = 1, TickerVendido = "PETR4", TickerComprado = "VALE3", QuantidadeVendida = 5, PrecoUnitarioVenda = 10m, QuantidadeComprada = 4, PrecoUnitarioCompra = 20m, ValorVenda = 50m, DataRebalanceamento = DateTime.UtcNow };

        var comparableA = new ComparableEntity(7);
        var comparableB = new ComparableEntity(7);
        var comparableC = new ComparableEntity(8);
        var auditable = new ComparableAuditableEntity(9);
        var beforeTouch = auditable.UpdatedAtUtc;

        plan.Pause();
        plan.Resume(DateTime.UtcNow.AddDays(2));
        plan.Cancel();
        plan.SetNextRun(DateTime.UtcNow.AddDays(3));
        auditable.Touch();

        Assert.Equal("PETR4", asset.Symbol);
        Assert.Equal(100m, money.Amount);
        Assert.Equal(CurrencyCode.BRL, money.Currency);
        Assert.Equal(1, schedule.Interval);
        Assert.Equal(ExecutionStatus.Executed, execution.Status);
        Assert.Equal(10m, cotacao.PrecoFechamento);
        Assert.Equal("QUEUED", ingestao.Status);
        Assert.Equal("PENDING", motor.Status);
        Assert.Equal(100m, motorHistorico.TotalConsolidado);
        Assert.True(cesta.Ativa);
        Assert.Equal(20m, itemCesta.Percentual);
        Assert.True(cliente.Ativo);
        Assert.Equal(450m, historicoValor.ValorNovo);
        Assert.Equal(TipoConta.Filhote, contaGrafica.Tipo);
        Assert.Equal(10, custodia.Quantidade);
        Assert.Equal(2, ordem.QuantidadeDisponivel);
        Assert.Equal(100m, distribuicao.Valor);
        Assert.Equal(TipoIR.DEDO_DURO, eventoIr.Tipo);
        Assert.Equal("VALE3", rebalanceamento.TickerComprado);
        Assert.True(comparableA.Equals(comparableB));
        Assert.False(comparableA.Equals(comparableC));
        Assert.NotEqual(0, comparableA.GetHashCode());
        Assert.True(auditable.UpdatedAtUtc >= beforeTouch);
    }

    private sealed class ComparableEntity : Entity<int>
    {
        public ComparableEntity(int id)
        {
            Id = id;
        }
    }

    private sealed class ComparableAuditableEntity : AuditableEntity<int>
    {
        public ComparableAuditableEntity(int id)
        {
            Id = id;
        }
    }
}
