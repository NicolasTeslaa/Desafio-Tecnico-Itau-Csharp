using Itau.InvestCycleEngine.Contracts.Common;
using MarketDataIngestionService.Controllers;
using MarketDataIngestionService.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketDataIngestionService.Tests;

public sealed class CotacoesControllerTests
{
    [Fact]
    public async Task IngestFile_ReturnsBadRequest_WhenFileIsEmpty()
    {
        var controller = new CotacoesController(
            new StubCotacoesService(),
            new FakeIngestJobService(),
            NullLogger<CotacoesController>.Instance);

        var request = new IngestFileRequest
        {
            File = new FormFile(Stream.Null, 0, 0, "file", "COTAHIST_D20260305.TXT")
        };

        var result = await controller.IngestFile(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("File is empty.", badRequest.Value);
    }

    [Fact]
    public async Task IngestFile_SavesFileUnderProjectCotacoes_AndReturnsOk()
    {
        var fakeService = new FakeIngestJobService();
        var controller = new CotacoesController(
            new StubCotacoesService(),
            fakeService,
            NullLogger<CotacoesController>.Instance);

        var tempRoot = Path.Combine(Path.GetTempPath(), $"marketdata-tests-{Guid.NewGuid():N}");
        var cotacoesDir = Path.Combine(tempRoot, "cotacoes");
        Directory.CreateDirectory(cotacoesDir);
        File.WriteAllText(Path.Combine(tempRoot, "Itau.InvestCycleEngine.slnx"), string.Empty);

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempRoot);

            await using var content = new MemoryStream("conteudo cotahist"u8.ToArray());
            var file = new FormFile(content, 0, content.Length, "file", "COTAHIST_D20260305.TXT");
            var request = new IngestFileRequest { File = file };

            var result = await controller.IngestFile(request, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<IngestStartResponse>(ok.Value);
            var expectedPath = Path.Combine(cotacoesDir, "COTAHIST_D20260305.TXT");

            Assert.Equal("COTAHIST_D20260305.TXT", response.File);
            Assert.Equal(expectedPath, fakeService.LastFilePath);
            Assert.True(File.Exists(expectedPath));
            Assert.Equal("conteudo cotahist", await File.ReadAllTextAsync(expectedPath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CotacoesController_MapsListLookupAndJobEndpoints()
    {
        var jobId = Guid.NewGuid();
        var service = new StubCotacoesService
        {
            ListHandler = (request, ticker, dataPregao, _) => Task.FromResult(new PagedResponse<ClassLibrary.Contracts.DTOs.CotacaoIngestDto>(
                [new ClassLibrary.Contracts.DTOs.CotacaoIngestDto(1, new DateTime(2026, 3, 5), ticker ?? "PETR4", 10m, 11m, 12m, 9m)],
                request.Page,
                request.PageSize,
                1)),
            GetByIdHandler = (id, _) => Task.FromResult<ClassLibrary.Contracts.DTOs.CotacaoIngestDto?>(id == 1
                ? new ClassLibrary.Contracts.DTOs.CotacaoIngestDto(1, new DateTime(2026, 3, 5), "PETR4", 10m, 11m, 12m, 9m)
                : null),
            TickersHandler = (query, limit, _) => Task.FromResult<IReadOnlyList<string>>(["PETR4", "VALE3"])
        };

        var jobs = new FakeIngestJobService
        {
            StatusResponse = new IngestJobStatusResponse(jobId, "COTAHIST.TXT", "COMPLETED", DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, 2, null),
            OverviewResponse = new IngestOverviewResponse(true, 1, new IngestJobStatusResponse(jobId, "COTAHIST.TXT", "PROCESSING", DateTime.UtcNow, DateTime.UtcNow, null, 0, null)),
            HistoryResponse = new IngestHistoryResponse([new IngestHistoryItemResponse("COTAHIST.TXT", 2, DateTime.UtcNow)])
        };

        var controller = new CotacoesController(service, jobs, NullLogger<CotacoesController>.Instance);

        Assert.IsType<OkObjectResult>(await controller.List(page: 2, pageSize: 10, ticker: "PETR4", dataPregao: new DateTime(2026, 3, 5), ct: CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.GetById(1, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.GetById(2, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.ListDistinctTickers(query: "PE", limit: 10, ct: CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.GetIngestStatus(jobId, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.GetIngestStatus(Guid.NewGuid(), CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.GetIngestOverview(CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.GetIngestHistory(5, CancellationToken.None));
    }

    [Fact]
    public async Task IngestFile_SanitizesInvalidFileNameCharacters()
    {
        var fakeService = new FakeIngestJobService();
        var controller = new CotacoesController(
            new StubCotacoesService(),
            fakeService,
            NullLogger<CotacoesController>.Instance);

        var tempRoot = Path.Combine(Path.GetTempPath(), $"marketdata-tests-{Guid.NewGuid():N}");
        var cotacoesDir = Path.Combine(tempRoot, "cotacoes");
        Directory.CreateDirectory(cotacoesDir);
        File.WriteAllText(Path.Combine(tempRoot, "Itau.InvestCycleEngine.slnx"), string.Empty);

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempRoot);

            await using var content = new MemoryStream("conteudo"u8.ToArray());
            var file = new FormFile(content, 0, content.Length, "file", "COTA:TEST?.TXT");
            var request = new IngestFileRequest { File = file };

            await controller.IngestFile(request, CancellationToken.None);

            Assert.EndsWith("COTA_TEST_.TXT", fakeService.LastFilePath);
            Assert.True(File.Exists(Path.Combine(cotacoesDir, "COTA_TEST_.TXT")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class FakeIngestJobService : IIngestJobService
    {
        public string? LastFilePath { get; private set; }
        public IngestJobStatusResponse? StatusResponse { get; set; }
        public IngestOverviewResponse OverviewResponse { get; set; } = new(false, 0, null);
        public IngestHistoryResponse HistoryResponse { get; set; } = new([]);

        public Task<IngestStartResponse> EnqueueAsync(string filePath, string fileName, CancellationToken ct)
        {
            LastFilePath = filePath;
            return Task.FromResult(new IngestStartResponse(Guid.NewGuid(), fileName, "QUEUED", DateTime.UtcNow));
        }

        public Task<IngestJobStatusResponse?> GetAsync(Guid jobId, CancellationToken ct)
            => Task.FromResult(StatusResponse?.JobId == jobId ? StatusResponse : null);

        public Task<IngestOverviewResponse> GetOverviewAsync(CancellationToken ct)
            => Task.FromResult(OverviewResponse);

        public Task<IngestHistoryResponse> GetRecentAsync(int take, CancellationToken ct)
            => Task.FromResult(HistoryResponse);
    }

    private sealed class StubCotacoesService : ICotacoesService
    {
        public Func<IEnumerable<ClassLibrary.Contracts.DTOs.CotacaoIngestDto>, CancellationToken, Task<int>> SaveHandler { get; set; } =
            (_, _) => Task.FromResult(0);
        public Func<IEnumerable<ClassLibrary.Contracts.DTOs.CotahistPriceRecord>, CancellationToken, Task<int>> SaveFromCotahistHandler { get; set; } =
            (_, _) => Task.FromResult(0);
        public Func<int, CancellationToken, Task<ClassLibrary.Contracts.DTOs.CotacaoIngestDto?>> GetByIdHandler { get; set; } =
            (_, _) => Task.FromResult<ClassLibrary.Contracts.DTOs.CotacaoIngestDto?>(null);
        public Func<PagedRequest, string?, DateTime?, CancellationToken, Task<PagedResponse<ClassLibrary.Contracts.DTOs.CotacaoIngestDto>>> ListHandler { get; set; } =
            (request, _, _, _) => Task.FromResult(new PagedResponse<ClassLibrary.Contracts.DTOs.CotacaoIngestDto>([], request.Page, request.PageSize, 0));
        public Func<string?, int, CancellationToken, Task<IReadOnlyList<string>>> TickersHandler { get; set; } =
            (_, _, _) => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<int> SaveAsync(IEnumerable<ClassLibrary.Contracts.DTOs.CotacaoIngestDto> dtos, CancellationToken ct)
            => SaveHandler(dtos, ct);

        public Task<int> SaveFromCotahistAsync(IEnumerable<ClassLibrary.Contracts.DTOs.CotahistPriceRecord> records, CancellationToken ct)
            => SaveFromCotahistHandler(records, ct);

        public Task<ClassLibrary.Contracts.DTOs.CotacaoIngestDto?> GetByIdAsync(int id, CancellationToken ct)
            => GetByIdHandler(id, ct);

        public Task<PagedResponse<ClassLibrary.Contracts.DTOs.CotacaoIngestDto>> ListAsync(PagedRequest request, string? ticker, DateTime? dataPregao, CancellationToken ct)
            => ListHandler(request, ticker, dataPregao, ct);

        public Task<IReadOnlyList<string>> ListDistinctTickersAsync(string? query, int limit, CancellationToken ct)
            => TickersHandler(query, limit, ct);
    }
}
