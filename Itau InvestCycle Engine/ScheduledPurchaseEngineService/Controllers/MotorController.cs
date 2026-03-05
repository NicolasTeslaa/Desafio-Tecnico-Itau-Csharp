using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Motor;
using Itau.InvestCycleEngine.Domain.Entities;
using Itau.InvestCycleEngine.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Controllers;

[ApiController]
[Route("api/motor")]
public sealed class MotorController : ControllerBase
{
    private readonly IScheduledPurchaseEngine _engine;
    private readonly IRepository<MotorExecucaoHistorico> _historicoRepo;

    public MotorController(
        IScheduledPurchaseEngine engine,
        IRepository<MotorExecucaoHistorico> historicoRepo)
    {
        _engine = engine;
        _historicoRepo = historicoRepo;
    }

    [HttpPost("executar-compra")]
    [ProducesResponseType(typeof(ExecutarCompraResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExecutarCompra([FromBody] ExecutarCompraRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _engine.ExecuteAsync(request.DataReferencia, ct);

            var ordens = result.Orders
                .GroupBy(x => x.Ticker)
                .Select(g =>
                {
                    var detalhes = g.Select(o => new DetalheOrdemCompraResponse(
                        Tipo: o.Market == TipoMercado.LOTE ? "LOTE" : "FRACIONARIO",
                        Ticker: o.Market == TipoMercado.FRACIONARIO ? $"{o.Ticker}F" : o.Ticker,
                        Quantidade: o.Quantity)).ToList();

                    var quantidadeTotal = g.Sum(x => x.Quantity);
                    var valorTotal = Math.Round(g.Sum(x => x.Quantity * x.UnitPrice), 2);
                    var precoUnitario = quantidadeTotal > 0
                        ? Math.Round(valorTotal / quantidadeTotal, 2)
                        : 0m;

                    return new OrdemCompraResponse(
                        Ticker: g.Key,
                        QuantidadeTotal: quantidadeTotal,
                        Detalhes: detalhes,
                        PrecoUnitario: precoUnitario,
                        ValorTotal: valorTotal);
                })
                .ToList();

            var distribuicoes = result.Distributions
                .Select(d => new DistribuicaoClienteResponse(
                    ClienteId: (int)d.ClientId,
                    Nome: d.Name,
                    ValorAporte: d.ContributionValue,
                    Ativos: d.Assets.Select(a => new AtivoDistribuicaoResponse(a.Ticker, a.Quantity)).ToList()))
                .ToList();

            var residuos = result.Residuals
                .Select(r => new ResiduoCustodiaMasterResponse(r.Ticker, r.Quantity))
                .ToList();

            var response = new ExecutarCompraResponse(
                DataExecucao: result.ExecutedAtUtc.UtcDateTime,
                TotalClientes: result.TotalClients,
                TotalConsolidado: result.TotalConsolidated,
                OrdensCompra: ordens,
                Distribuicoes: distribuicoes,
                ResiduosCustMaster: residuos,
                EventosIrPublicados: result.IrEventsPublished,
                Mensagem: $"Compra programada executada com sucesso para {result.TotalClients} clientes.");

            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("COTACAO_NAO_ENCONTRADA"))
        {
            return NotFound(new ApiError("Cotacao nao encontrada para a data informada.", "COTACAO_NAO_ENCONTRADA"));
        }
        catch (InvalidOperationException ex) when (ex.Message == "CESTA_NAO_ENCONTRADA")
        {
            return NotFound(new ApiError("Nenhuma cesta ativa encontrada.", "CESTA_NAO_ENCONTRADA"));
        }
        catch (InvalidOperationException ex) when (ex.Message == "CESTA_SEM_ITENS")
        {
            return BadRequest(new ApiError("Cesta ativa sem itens configurados.", "QUANTIDADE_ATIVOS_INVALIDA"));
        }
        catch (InvalidOperationException ex) when (ex.Message == "DATA_EXECUCAO_INVALIDA")
        {
            return BadRequest(new ApiError(BuildInvalidExecutionDateMessage(request.DataReferencia), "DATA_EXECUCAO_INVALIDA"));
        }
        catch (InvalidOperationException ex) when (ex.Message == "COMPRA_JA_EXECUTADA")
        {
            return StatusCode(StatusCodes.Status409Conflict, new ApiError("Compra ja foi executada para esta data.", "COMPRA_JA_EXECUTADA"));
        }
        catch (InvalidOperationException ex) when (ex.Message == "KAFKA_INDISPONIVEL")
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiError("Erro ao publicar no topico Kafka.", "KAFKA_INDISPONIVEL"));
        }
    }

    [HttpGet("historico")]
    [ProducesResponseType(typeof(MotorHistoricoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistorico([FromQuery] int take = 5, CancellationToken ct = default)
    {
        var safeTake = Math.Clamp(take, 1, 50);

        var items = await _historicoRepo.Query()
            .AsNoTracking()
            .OrderByDescending(x => x.DataHoraUtc)
            .Take(safeTake)
            .Select(x => new MotorHistoricoItemResponse(
                x.DataReferencia,
                x.TotalClientes,
                x.TotalConsolidado,
                x.DataHoraUtc))
            .ToListAsync(ct);

        return Ok(new MotorHistoricoResponse(items));
    }

    private static string BuildInvalidExecutionDateMessage(DateOnly referenceDate)
    {
        var d5 = ResolveRunDate(referenceDate.Year, referenceDate.Month, 5);
        var d15 = ResolveRunDate(referenceDate.Year, referenceDate.Month, 15);
        var d25 = ResolveRunDate(referenceDate.Year, referenceDate.Month, 25);

        return $"Data de execucao invalida para {referenceDate:yyyy-MM-dd}. Datas validas no mes: {d5:yyyy-MM-dd}, {d15:yyyy-MM-dd}, {d25:yyyy-MM-dd}.";
    }

    private static DateOnly ResolveRunDate(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return date;
    }
}
