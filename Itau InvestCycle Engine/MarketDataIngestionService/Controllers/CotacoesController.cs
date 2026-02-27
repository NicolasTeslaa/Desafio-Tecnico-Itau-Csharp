using Itau.InvestCycleEngine.Contracts.Common;
using MarketDataIngestionService.Interfaces;
using MarketDataIngestionService.Parser;
using Microsoft.AspNetCore.Mvc;

namespace MarketDataIngestionService.Controllers;

public sealed class IngestFileRequest
{
    public IFormFile File { get; set; } = default!;
}

[ApiController]
[Route("api/cotacoes")]
public sealed class CotacoesController : ControllerBase
{
    private readonly ICotacoesService _service;
    private readonly CotahistParser _parser;

    public CotacoesController(ICotacoesService service, CotahistParser parser)
    {
        _service = service;
        _parser = parser;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? ticker = null,
        [FromQuery] DateTime? dataPregao = null,
        CancellationToken ct = default)
    {
        var request = new PagedRequest(page, pageSize);
        var result = await _service.ListAsync(request, ticker, dataPregao, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        if (item is null)
            return NotFound();

        return Ok(item);
    }

    [HttpPost("ingest")]
    [Consumes("multipart/form-data")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> IngestFile(
        [FromForm] IngestFileRequest request,
        CancellationToken ct)
    {
        if (request?.File is null || request.File.Length == 0)
            return BadRequest("File is empty.");

        var file = request.File;

        await using var stream = file.OpenReadStream();
        var records = _parser.ParseStream(stream);

        var saved = await _service.SaveFromCotahistAsync(records, ct);

        return Ok(new { file = file.FileName, saved });
    }
}
