using Itau.InvestCycleEngine.Contracts.Common;
using MarketDataIngestionService.Interfaces;
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
    private readonly IIngestJobService _ingestJobService;
    private readonly ILogger<CotacoesController> _logger;

    public CotacoesController(ICotacoesService service, IIngestJobService ingestJobService, ILogger<CotacoesController> logger)
    {
        _service = service;
        _ingestJobService = ingestJobService;
        _logger = logger;
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
        var uploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var jobFileName = $"{Guid.NewGuid():N}_{Sanitize(file.FileName)}";
        var filePath = Path.Combine(uploadsDir, jobFileName);

        await using (var stream = file.OpenReadStream())
        await using (var target = System.IO.File.Create(filePath))
        {
            await stream.CopyToAsync(target, ct);
        }

        var response = await _ingestJobService.EnqueueAsync(filePath, file.FileName, ct);
        _logger.LogInformation("Novo job de ingestao enfileirado: {JobId} para arquivo {File}", response.JobId, response.File);

        return Accepted(response);
    }

    [HttpGet("ingest/{jobId:guid}")]
    [ProducesResponseType(typeof(IngestJobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIngestStatus(Guid jobId, CancellationToken ct)
    {
        var job = await _ingestJobService.GetAsync(jobId, ct);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(job);
    }

    [HttpGet("ingest/overview")]
    [ProducesResponseType(typeof(IngestOverviewResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIngestOverview(CancellationToken ct)
    {
        var overview = await _ingestJobService.GetOverviewAsync(ct);
        return Ok(overview);
    }

    private static string Sanitize(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
    }
}
