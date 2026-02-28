using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Clientes;
using Microsoft.AspNetCore.Mvc;
using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Controllers;

[ApiController]
[Route("api/clientes")]
public sealed class ClientController : ControllerBase
{
    private readonly IClentService _service;

    public ClientController(IClentService service)
    {
        _service = service;
    }

    [HttpPost("adesao")]
    [ProducesResponseType(typeof(AdesaoClienteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Adesao([FromBody] AdesaoClienteRequest request, CancellationToken ct)
    {
        var result = await _service.AdesaoProdutoAsync(request, ct);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Err!);
        }

        return StatusCode(StatusCodes.Status201Created, result.Ok!);
    }

    [HttpPost("{clienteId:int}/saida")]
    [ProducesResponseType(typeof(SaidaClienteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Saida([FromRoute] int clienteId, CancellationToken ct)
    {
        var result = await _service.SairDoProdutoAsync(clienteId, ct);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Err!);
        }

        return Ok(result.Ok!);
    }

    [HttpDelete("{clienteId:int}")]
    [ProducesResponseType(typeof(ExcluirClienteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excluir([FromRoute] int clienteId, CancellationToken ct)
    {
        var result = await _service.ExcluirClienteAsync(clienteId, ct);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Err!);
        }

        return Ok(result.Ok!);
    }

    [HttpPut("{clienteId:int}/valor-mensal")]
    [ProducesResponseType(typeof(AlterarValorMensalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AlterarValorMensal([FromRoute] int clienteId, [FromBody] AlterarValorMensalRequest request, CancellationToken ct)
    {
        var result = await _service.AlterarValorMensalAsync(clienteId, request, ct);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Err!);
        }

        return Ok(result.Ok!);
    }

    [HttpGet("{clienteId:int}/carteira")]
    [ProducesResponseType(typeof(ConsultarCarteiraResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConsultarCarteira([FromRoute] int clienteId, CancellationToken ct)
    {
        var result = await _service.ConsultarCarteiraAsync(clienteId, ct);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Err!);
        }

        return Ok(result.Ok!);
    }

    [HttpGet("{clienteId:int}/rentabilidade")]
    [ProducesResponseType(typeof(ConsultarRentabilidadeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConsultarRentabilidade([FromRoute] int clienteId, CancellationToken ct)
    {
        var result = await _service.ConsultarRentabilidadeAsync(clienteId, ct);
        if (!result.IsSuccess)
        {
            return ToErrorResponse(result.Err!);
        }

        return Ok(result.Ok!);
    }

    private IActionResult ToErrorResponse(ApiError error)
    {
        if (error.Codigo == "CLIENTE_NAO_ENCONTRADO")
        {
            return NotFound(error);
        }

        return BadRequest(error);
    }
}
