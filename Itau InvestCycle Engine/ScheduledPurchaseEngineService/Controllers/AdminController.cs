using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Admin;
using Microsoft.AspNetCore.Mvc;
using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminService _service;

    public AdminController(IAdminService service) => _service = service;

    [HttpPost("cesta")]
    [ProducesResponseType(typeof(CadastrarOuAlterarCestaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CadastrarOuAlterarCesta([FromBody] CadastrarOuAlterarCestaRequest request, CancellationToken ct)
    {
        Result<CadastrarOuAlterarCestaResponse, ApiError> result;
        try
        {
            result = await _service.CadastrarOuAlterarCestaAsync(request, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == "KAFKA_INDISPONIVEL")
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ApiError("Erro ao publicar no topico Kafka.", "KAFKA_INDISPONIVEL"));
        }

        if (!result.IsSuccess)
            return ToErrorResponse(result.Err!);

        return StatusCode(StatusCodes.Status201Created, result.Ok!);
    }

    [HttpPut("cesta/{cestaId:int}")]
    [ProducesResponseType(typeof(CadastrarOuAlterarCestaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditarCesta([FromRoute] int cestaId, [FromBody] CadastrarOuAlterarCestaRequest request, CancellationToken ct)
    {
        var result = await _service.EditarCestaAsync(cestaId, request, ct);

        if (!result.IsSuccess)
            return ToErrorResponse(result.Err!);

        return Ok(result.Ok!);
    }

    [HttpGet("cesta/atual")]
    [ProducesResponseType(typeof(CestaAtualResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConsultarCestaAtual(CancellationToken ct)
    {
        var result = await _service.ConsultarCestaAtualAsync(ct);

        if (!result.IsSuccess)
            return ToErrorResponse(result.Err!);

        return Ok(result.Ok!);
    }

    [HttpGet("cesta/historico")]
    [ProducesResponseType(typeof(HistoricoCestasResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> HistoricoCestas(CancellationToken ct)
    {
        var response = await _service.HistoricoCestasAsync(ct);
        return Ok(response);
    }

    [HttpGet("cesta/tickers")]
    [ProducesResponseType(typeof(TickersDisponiveisResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarTickersDisponiveis([FromQuery] string? query = null, [FromQuery] int limit = 500, CancellationToken ct = default)
    {
        var response = await _service.ListarTickersDisponiveisAsync(query, limit, ct);
        return Ok(response);
    }

    [HttpDelete("cesta/{cestaId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExcluirCesta([FromRoute] int cestaId, CancellationToken ct)
    {
        var result = await _service.ExcluirCestaAsync(cestaId, ct);

        if (!result.IsSuccess)
            return ToErrorResponse(result.Err!);

        return NoContent();
    }

    [HttpGet("conta-master/custodia")]
    [ProducesResponseType(typeof(ContaMasterCustodiaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConsultarCustodiaMaster(CancellationToken ct)
    {
        var result = await _service.ConsultarCustodiaMasterAsync(ct);

        if (!result.IsSuccess)
            return ToErrorResponse(result.Err!);

        return Ok(result.Ok!);
    }

    [HttpPost("rebalancear/desvio")]
    [ProducesResponseType(typeof(RebalanceamentoDesvioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RebalancearPorDesvio([FromBody] RebalanceamentoDesvioRequest request, CancellationToken ct)
    {
        Result<RebalanceamentoDesvioResponse, ApiError> result;
        try
        {
            result = await _service.RebalancearPorDesvioAsync(request, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == "KAFKA_INDISPONIVEL")
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ApiError("Erro ao publicar no topico Kafka.", "KAFKA_INDISPONIVEL"));
        }

        if (!result.IsSuccess)
            return ToErrorResponse(result.Err!);

        return Ok(result.Ok!);
    }

    private IActionResult ToErrorResponse(ApiError error)
    {
        if (error.Codigo is "CESTA_NAO_ENCONTRADA" or "CONTA_MASTER_NAO_ENCONTRADA")
            return NotFound(error);
        
        return BadRequest(error);
    }
}
