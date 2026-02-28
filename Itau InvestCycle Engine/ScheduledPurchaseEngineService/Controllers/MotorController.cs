using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Motor;
using Itau.InvestCycleEngine.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Controllers;

[ApiController]
[Route("api/motor")]
public sealed class MotorController : ControllerBase
{
    private readonly IScheduledPurchaseEngine _engine;

    public MotorController(IScheduledPurchaseEngine engine)
    {
        _engine = engine;
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
    }
}
