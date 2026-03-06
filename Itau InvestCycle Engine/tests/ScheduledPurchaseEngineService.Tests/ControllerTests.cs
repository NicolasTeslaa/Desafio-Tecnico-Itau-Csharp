using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Admin;
using ClassLibrary.Contracts.DTOs.Clientes;
using ClassLibrary.Contracts.DTOs.Motor;
using Itau.InvestCycleEngine.Domain.Entities;
using Itau.InvestCycleEngine.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Controllers;
using ScheduledPurchaseEngineService.Data;
using ScheduledPurchaseEngineService.Interfaces;
using ScheduledPurchaseEngineService.Repositories;

namespace ScheduledPurchaseEngineService.Tests;

public sealed class ControllerTests
{
    [Fact]
    public async Task AdminController_ReturnsExpectedHttpResults()
    {
        var service = new StubAdminService
        {
            CadastrarHandler = (_, _) => Task.FromResult(Result<CadastrarOuAlterarCestaResponse, ApiError>.Success(
                new CadastrarOuAlterarCestaResponse(1, "Top Five", true, DateTime.UtcNow, [new CestaItemResponse("PETR4", 20m)], null, false, [], [], "ok"))),
            CestaAtualHandler = _ => Task.FromResult(Result<CestaAtualResponse, ApiError>.Failure(new ApiError("nao encontrada", "CESTA_NAO_ENCONTRADA"))),
            ExcluirHandler = (_, _) => Task.FromResult(Result<bool, ApiError>.Success(true)),
            TickersHandler = (_, _, _) => Task.FromResult(new TickersDisponiveisResponse(["PETR4"])),
            HistoricoHandler = _ => Task.FromResult(new HistoricoCestasResponse([])),
            CustodiaHandler = _ => Task.FromResult(Result<ContaMasterCustodiaResponse, ApiError>.Failure(new ApiError("nao encontrada", "CONTA_MASTER_NAO_ENCONTRADA"))),
            RebalancearHandler = (_, _) => throw new InvalidOperationException("KAFKA_INDISPONIVEL")
        };

        var controller = new AdminController(service);
        var request = new CadastrarOuAlterarCestaRequest("Top Five", [new CestaItemRequest("PETR4", 20m), new CestaItemRequest("VALE3", 20m), new CestaItemRequest("ITUB4", 20m), new CestaItemRequest("ABEV3", 20m), new CestaItemRequest("RENT3", 20m)]);

        Assert.Equal(201, Assert.IsType<ObjectResult>(await controller.CadastrarOuAlterarCesta(request, CancellationToken.None)).StatusCode);
        Assert.IsType<NotFoundObjectResult>(await controller.ConsultarCestaAtual(CancellationToken.None));
        Assert.IsType<NoContentResult>(await controller.ExcluirCesta(1, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.ListarTickersDisponiveis(ct: CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.HistoricoCestas(CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.ConsultarCustodiaMaster(CancellationToken.None));
        Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.RebalancearPorDesvio(new RebalanceamentoDesvioRequest(5m), CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task AdminController_MapsRemainingSuccessAndValidationBranches()
    {
        var service = new StubAdminService
        {
            CadastrarHandler = (_, _) => Task.FromResult(Result<CadastrarOuAlterarCestaResponse, ApiError>.Failure(new ApiError("invalida", "VALIDACAO"))),
            EditarHandler = (_, _, _) => Task.FromResult(Result<CadastrarOuAlterarCestaResponse, ApiError>.Success(
                new CadastrarOuAlterarCestaResponse(2, "Top Five", true, DateTime.UtcNow, [new CestaItemResponse("PETR4", 20m)], null, true, [], [], "ok"))),
            CestaAtualHandler = _ => Task.FromResult(Result<CestaAtualResponse, ApiError>.Success(
                new CestaAtualResponse(2, "Top Five", true, DateTime.UtcNow, [new CestaAtualItemResponse("PETR4", 20m, 30m)]))),
            CustodiaHandler = _ => Task.FromResult(Result<ContaMasterCustodiaResponse, ApiError>.Success(
                new ContaMasterCustodiaResponse(new ContaMasterInfoResponse(1, "MST-1", "MASTER"), [], 0m))),
            RebalancearHandler = (_, _) => Task.FromResult(Result<RebalanceamentoDesvioResponse, ApiError>.Success(
                new RebalanceamentoDesvioResponse(10, 2, 5m, "ok")))
        };

        var controller = new AdminController(service);
        var request = new CadastrarOuAlterarCestaRequest("Top Five", [new CestaItemRequest("PETR4", 20m)]);

        Assert.IsType<BadRequestObjectResult>(await controller.CadastrarOuAlterarCesta(request, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.EditarCesta(2, request, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.ConsultarCestaAtual(CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.ConsultarCustodiaMaster(CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.RebalancearPorDesvio(new RebalanceamentoDesvioRequest(5m), CancellationToken.None));
    }

    [Fact]
    public async Task ClientController_ReturnsExpectedHttpResults()
    {
        var service = new StubClientService
        {
            ListarHandler = (_, _) => Task.FromResult(Result<ListarClientesResponse, ApiError>.Success(new ListarClientesResponse([]))),
            AdesaoHandler = (_, _) => Task.FromResult(Result<AdesaoClienteResponse, ApiError>.Success(
                new AdesaoClienteResponse(1, "Cliente", "12345678909", "c@teste.com", 300m, true, DateTime.UtcNow, new ContaGraficaResponse(1, "FLH-1", "FILHOTE", DateTime.UtcNow)))),
            SaidaHandler = (_, _) => Task.FromResult(Result<SaidaClienteResponse, ApiError>.Success(new SaidaClienteResponse(1, "Cliente", false, DateTime.UtcNow, "ok"))),
            ExcluirHandler = (_, _) => Task.FromResult(Result<ExcluirClienteResponse, ApiError>.Failure(new ApiError("nao encontrado", "CLIENTE_NAO_ENCONTRADO"))),
            AlterarHandler = (_, _, _) => Task.FromResult(Result<AlterarValorMensalResponse, ApiError>.Success(new AlterarValorMensalResponse(1, 300m, 450m, DateTime.UtcNow, "ok"))),
            CarteiraHandler = (_, _) => Task.FromResult(Result<ConsultarCarteiraResponse, ApiError>.Success(new ConsultarCarteiraResponse(1, "Cliente", "FLH-1", DateTime.UtcNow, new ResumoCarteiraResponse(100m, 110m, 10m, 10m), []))),
            RentabilidadeHandler = (_, _) => Task.FromResult(Result<ConsultarRentabilidadeResponse, ApiError>.Success(new ConsultarRentabilidadeResponse(1, "Cliente", DateTime.UtcNow, new RentabilidadeResumoResponse(100m, 110m, 10m, 10m), [], [])))
        };

        var controller = new ClientController(service);

        Assert.IsType<OkObjectResult>(await controller.Listar(ct: CancellationToken.None));
        Assert.Equal(201, Assert.IsType<ObjectResult>(await controller.Adesao(new AdesaoClienteRequest("Cliente", "12345678909", "c@teste.com", 300m), CancellationToken.None)).StatusCode);
        Assert.IsType<OkObjectResult>(await controller.Saida(1, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.Excluir(1, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.AlterarValorMensal(1, new AlterarValorMensalRequest(450m), CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.ConsultarCarteira(1, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.ConsultarRentabilidade(1, CancellationToken.None));
    }

    [Fact]
    public async Task ClientController_MapsBadRequestAndNotFoundBranches()
    {
        var service = new StubClientService
        {
            ListarHandler = (_, _) => Task.FromResult(Result<ListarClientesResponse, ApiError>.Failure(new ApiError("invalido", "VALIDACAO"))),
            AdesaoHandler = (_, _) => Task.FromResult(Result<AdesaoClienteResponse, ApiError>.Failure(new ApiError("invalido", "VALIDACAO"))),
            SaidaHandler = (_, _) => Task.FromResult(Result<SaidaClienteResponse, ApiError>.Failure(new ApiError("nao encontrado", "CLIENTE_NAO_ENCONTRADO"))),
            ExcluirHandler = (_, _) => Task.FromResult(Result<ExcluirClienteResponse, ApiError>.Failure(new ApiError("nao encontrado", "CLIENTE_NAO_ENCONTRADO"))),
            AlterarHandler = (_, _, _) => Task.FromResult(Result<AlterarValorMensalResponse, ApiError>.Failure(new ApiError("nao encontrado", "CLIENTE_NAO_ENCONTRADO"))),
            CarteiraHandler = (_, _) => Task.FromResult(Result<ConsultarCarteiraResponse, ApiError>.Failure(new ApiError("nao encontrado", "CLIENTE_NAO_ENCONTRADO"))),
            RentabilidadeHandler = (_, _) => Task.FromResult(Result<ConsultarRentabilidadeResponse, ApiError>.Failure(new ApiError("nao encontrado", "CLIENTE_NAO_ENCONTRADO")))
        };

        var controller = new ClientController(service);

        Assert.IsType<BadRequestObjectResult>(await controller.Listar(ct: CancellationToken.None));
        Assert.IsType<BadRequestObjectResult>(await controller.Adesao(new AdesaoClienteRequest("Cliente", "12345678909", "c@teste.com", 300m), CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.Saida(1, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.Excluir(1, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.AlterarValorMensal(1, new AlterarValorMensalRequest(450m), CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.ConsultarCarteira(1, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.ConsultarRentabilidade(1, CancellationToken.None));
    }

    [Fact]
    public async Task MotorController_MapsSuccessAndErrorBranches()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var engine = new StubScheduledPurchaseEngine
        {
            ExecuteHandler = (_, _) => Task.FromResult(new ScheduledPurchaseResult(
                DateTimeOffset.UtcNow,
                new DateOnly(2026, 3, 5),
                1,
                300m,
                [new OrderSummary("PETR4", 10, 10m, TipoMercado.FRACIONARIO)],
                [new ClientDistributionSummary(1, "Cliente", 100m, [new AssetQty("PETR4", 10)])],
                [new ResidualSummary("PETR4", 2)],
                1))
        };

        db.Set<MotorExecucaoHistorico>().Add(new MotorExecucaoHistorico
        {
            Id = 1,
            DataReferencia = new DateTime(2026, 3, 5),
            TotalClientes = 1,
            TotalConsolidado = 300m,
            DataHoraUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = new MotorController(engine, new Repository<MotorExecucaoHistorico>(db));

        var ok = Assert.IsType<OkObjectResult>(await controller.ExecutarCompra(new ExecutarCompraRequest(new DateOnly(2026, 3, 5)), CancellationToken.None));
        var payload = Assert.IsType<ExecutarCompraResponse>(ok.Value);
        Assert.Equal(1, payload.TotalClientes);
        Assert.Equal("PETR4F", payload.OrdensCompra[0].Detalhes[0].Ticker);

        Assert.IsType<OkObjectResult>(await controller.GetHistorico(100, CancellationToken.None));

        engine.ExecuteHandler = (_, _) => throw new InvalidOperationException("DATA_EXECUCAO_INVALIDA");
        Assert.IsType<BadRequestObjectResult>(await controller.ExecutarCompra(new ExecutarCompraRequest(new DateOnly(2026, 3, 6)), CancellationToken.None));

        engine.ExecuteHandler = (_, _) => throw new InvalidOperationException("COMPRA_JA_EXECUTADA");
        Assert.Equal(409, Assert.IsType<ObjectResult>(await controller.ExecutarCompra(new ExecutarCompraRequest(new DateOnly(2026, 3, 5)), CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task MotorController_MapsRemainingExceptionBranches()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var engine = new StubScheduledPurchaseEngine();
        var controller = new MotorController(engine, new Repository<MotorExecucaoHistorico>(db));
        var request = new ExecutarCompraRequest(new DateOnly(2026, 3, 25));

        engine.ExecuteHandler = (_, _) => throw new InvalidOperationException("COTACAO_NAO_ENCONTRADA");
        Assert.IsType<NotFoundObjectResult>(await controller.ExecutarCompra(request, CancellationToken.None));

        engine.ExecuteHandler = (_, _) => throw new InvalidOperationException("CESTA_NAO_ENCONTRADA");
        Assert.IsType<NotFoundObjectResult>(await controller.ExecutarCompra(request, CancellationToken.None));

        engine.ExecuteHandler = (_, _) => throw new InvalidOperationException("CESTA_SEM_ITENS");
        Assert.IsType<BadRequestObjectResult>(await controller.ExecutarCompra(request, CancellationToken.None));

        engine.ExecuteHandler = (_, _) => throw new InvalidOperationException("KAFKA_INDISPONIVEL");
        Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.ExecutarCompra(request, CancellationToken.None)).StatusCode);
    }

    private sealed class StubAdminService : IAdminService
    {
        public Func<CadastrarOuAlterarCestaRequest, CancellationToken, Task<Result<CadastrarOuAlterarCestaResponse, ApiError>>> CadastrarHandler { get; set; } = default!;
        public Func<int, CadastrarOuAlterarCestaRequest, CancellationToken, Task<Result<CadastrarOuAlterarCestaResponse, ApiError>>> EditarHandler { get; set; } = (_, _, _) => Task.FromResult(Result<CadastrarOuAlterarCestaResponse, ApiError>.Failure(new ApiError("", "")));
        public Func<int, CancellationToken, Task<Result<bool, ApiError>>> ExcluirHandler { get; set; } = default!;
        public Func<string?, int, CancellationToken, Task<TickersDisponiveisResponse>> TickersHandler { get; set; } = default!;
        public Func<CancellationToken, Task<Result<CestaAtualResponse, ApiError>>> CestaAtualHandler { get; set; } = default!;
        public Func<CancellationToken, Task<HistoricoCestasResponse>> HistoricoHandler { get; set; } = default!;
        public Func<CancellationToken, Task<Result<ContaMasterCustodiaResponse, ApiError>>> CustodiaHandler { get; set; } = default!;
        public Func<RebalanceamentoDesvioRequest, CancellationToken, Task<Result<RebalanceamentoDesvioResponse, ApiError>>> RebalancearHandler { get; set; } = default!;

        public Task<Result<CadastrarOuAlterarCestaResponse, ApiError>> CadastrarOuAlterarCestaAsync(CadastrarOuAlterarCestaRequest request, CancellationToken ct) => CadastrarHandler(request, ct);
        public Task<Result<CadastrarOuAlterarCestaResponse, ApiError>> EditarCestaAsync(int cestaId, CadastrarOuAlterarCestaRequest request, CancellationToken ct) => EditarHandler(cestaId, request, ct);
        public Task<Result<bool, ApiError>> ExcluirCestaAsync(int cestaId, CancellationToken ct) => ExcluirHandler(cestaId, ct);
        public Task<TickersDisponiveisResponse> ListarTickersDisponiveisAsync(string? query, int limit, CancellationToken ct) => TickersHandler(query, limit, ct);
        public Task<Result<CestaAtualResponse, ApiError>> ConsultarCestaAtualAsync(CancellationToken ct) => CestaAtualHandler(ct);
        public Task<HistoricoCestasResponse> HistoricoCestasAsync(CancellationToken ct) => HistoricoHandler(ct);
        public Task<Result<ContaMasterCustodiaResponse, ApiError>> ConsultarCustodiaMasterAsync(CancellationToken ct) => CustodiaHandler(ct);
        public Task<Result<RebalanceamentoDesvioResponse, ApiError>> RebalancearPorDesvioAsync(RebalanceamentoDesvioRequest request, CancellationToken ct) => RebalancearHandler(request, ct);
    }

    private sealed class StubClientService : IClentService
    {
        public Func<AdesaoClienteRequest, CancellationToken, Task<Result<AdesaoClienteResponse, ApiError>>> AdesaoHandler { get; set; } = default!;
        public Func<int, CancellationToken, Task<Result<ExcluirClienteResponse, ApiError>>> ExcluirHandler { get; set; } = default!;
        public Func<int, CancellationToken, Task<Result<SaidaClienteResponse, ApiError>>> SaidaHandler { get; set; } = default!;
        public Func<int, AlterarValorMensalRequest, CancellationToken, Task<Result<AlterarValorMensalResponse, ApiError>>> AlterarHandler { get; set; } = default!;
        public Func<int, CancellationToken, Task<Result<ConsultarCarteiraResponse, ApiError>>> CarteiraHandler { get; set; } = default!;
        public Func<int, CancellationToken, Task<Result<ConsultarRentabilidadeResponse, ApiError>>> RentabilidadeHandler { get; set; } = default!;
        public Func<bool?, CancellationToken, Task<Result<ListarClientesResponse, ApiError>>> ListarHandler { get; set; } = default!;

        public Task<Result<AdesaoClienteResponse, ApiError>> AdesaoProdutoAsync(AdesaoClienteRequest request, CancellationToken ct) => AdesaoHandler(request, ct);
        public Task<Result<ExcluirClienteResponse, ApiError>> ExcluirClienteAsync(int clienteId, CancellationToken ct) => ExcluirHandler(clienteId, ct);
        public Task<Result<SaidaClienteResponse, ApiError>> SairDoProdutoAsync(int clienteId, CancellationToken ct) => SaidaHandler(clienteId, ct);
        public Task<Result<AlterarValorMensalResponse, ApiError>> AlterarValorMensalAsync(int clienteId, AlterarValorMensalRequest request, CancellationToken ct) => AlterarHandler(clienteId, request, ct);
        public Task<Result<ConsultarCarteiraResponse, ApiError>> ConsultarCarteiraAsync(int clienteId, CancellationToken ct) => CarteiraHandler(clienteId, ct);
        public Task<Result<ConsultarRentabilidadeResponse, ApiError>> ConsultarRentabilidadeAsync(int clienteId, CancellationToken ct) => RentabilidadeHandler(clienteId, ct);
        public Task<Result<ListarClientesResponse, ApiError>> ListarClientesAsync(bool? ativo, CancellationToken ct) => ListarHandler(ativo, ct);
    }

    private sealed class StubScheduledPurchaseEngine : IScheduledPurchaseEngine
    {
        public Func<DateOnly, CancellationToken, Task<ScheduledPurchaseResult>> ExecuteHandler { get; set; } = default!;

        public Task<ScheduledPurchaseResult> ExecuteAsync(DateOnly referenceDate, CancellationToken ct = default)
            => ExecuteHandler(referenceDate, ct);
    }
}
