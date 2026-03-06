using ClassLibrary.Domain.Entities;
using MarketDataIngestionService.Interfaces;
using MarketDataIngestionService.Parser;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;

namespace MarketDataIngestionService.Services;

public sealed class IngestJobBackgroundService : BackgroundService, IIngestJobService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IngestJobBackgroundService> _logger;
    private readonly Channel<IngestWorkItem> _channel;

    public IngestJobBackgroundService(IServiceScopeFactory scopeFactory, ILogger<IngestJobBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _channel = Channel.CreateUnbounded<IngestWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async Task<IngestStartResponse> EnqueueAsync(string filePath, string fileName, CancellationToken ct)
    {
        try
        {
            var id = Guid.NewGuid();
            var now = DateTime.UtcNow;

            using (var scope = _scopeFactory.CreateScope())
            {
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var repo = uow.Repository<IngestaoJob>();

                await uow.BeginAsync(ct);
                await repo.AddAsync(new IngestaoJob
                {
                    Id = id,
                    File = fileName,
                    StoredPath = filePath,
                    Status = "QUEUED",
                    CreatedAtUtc = now,
                    Saved = 0,
                    Error = null
                }, ct);
                await uow.CommitAsync(ct);
            }

            _channel.Writer.TryWrite(new IngestWorkItem(id, filePath, fileName));

            return new IngestStartResponse(id, fileName, "QUEUED", now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enfileirar job de ingestao ({File})", fileName);
            throw;
        }
    }

    public async Task<IngestJobStatusResponse?> GetAsync(Guid jobId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Repository<IngestaoJob>();

        var job = await repo.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jobId, ct);

        if (job is null)
        {
            return null;
        }

        return ToDto(job);
    }

    public async Task<IngestOverviewResponse> GetOverviewAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Repository<IngestaoJob>();

        var processingCount = await repo.Query()
            .AsNoTracking()
            .CountAsync(x => x.Status == "QUEUED" || x.Status == "PROCESSING", ct);

        var lastJob = await repo.Query()
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        return new IngestOverviewResponse(
            HasProcessing: processingCount > 0,
            ProcessingCount: processingCount,
            LastJob: lastJob is null ? null : ToDto(lastJob));
    }

    public async Task<IngestHistoryResponse> GetRecentAsync(int take, CancellationToken ct)
    {
        var safeTake = Math.Clamp(take, 1, 50);

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Repository<IngestaoJob>();

        var items = await repo.Query()
            .AsNoTracking()
            .Where(x => x.Status == "COMPLETED")
            .OrderByDescending(x => x.FinishedAtUtc ?? x.CreatedAtUtc)
            .Take(safeTake)
            .Select(x => new IngestHistoryItemResponse(
                x.File,
                x.Saved,
                x.FinishedAtUtc ?? x.CreatedAtUtc))
            .ToListAsync(ct);

        return new IngestHistoryResponse(items);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await MarkProcessingAsync(item.JobId, stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var parser = scope.ServiceProvider.GetRequiredService<CotahistParser>();
                var cotacoesService = scope.ServiceProvider.GetRequiredService<ICotacoesService>();

                await using var stream = File.OpenRead(item.FilePath);
                var records = parser.ParseStream(stream);
                var saved = await cotacoesService.SaveFromCotahistAsync(records, stoppingToken);

                await MarkCompletedAsync(item.JobId, saved, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar job de ingestao {JobId} ({File})", item.JobId, item.File);
                await MarkFailedAsync(item.JobId, ex.Message, stoppingToken);
            }
        }
    }

    private async Task MarkProcessingAsync(Guid jobId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var repo = uow.Repository<IngestaoJob>();

            var job = await repo.FindAsync([jobId], ct);
            if (job is null) return;

            await uow.BeginAsync(ct);
            job.Status = "PROCESSING";
            job.StartedAtUtc = DateTime.UtcNow;
            job.Error = null;
            repo.Update(job);
            await uow.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao marcar job de ingestao como PROCESSING {JobId}", jobId);
        }
    }

    private async Task MarkCompletedAsync(Guid jobId, int saved, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var repo = uow.Repository<IngestaoJob>();

            var job = await repo.FindAsync([jobId], ct);
            if (job is null) return;

            await uow.BeginAsync(ct);
            job.Status = "COMPLETED";
            job.Saved = saved;
            job.FinishedAtUtc = DateTime.UtcNow;
            repo.Update(job);
            await uow.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao marcar job de ingestao como COMPLETED {JobId}", jobId);
        }
    }

    private async Task MarkFailedAsync(Guid jobId, string message, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var repo = uow.Repository<IngestaoJob>();

        var job = await repo.FindAsync([jobId], ct);
        if (job is null) return;

        await uow.BeginAsync(ct);
        job.Status = "FAILED";
        job.Error = message;
        job.FinishedAtUtc = DateTime.UtcNow;
        repo.Update(job);
        await uow.CommitAsync(ct);
    }

    private static IngestJobStatusResponse ToDto(IngestaoJob job)
        => new(job.Id, job.File, job.Status, job.CreatedAtUtc, job.StartedAtUtc, job.FinishedAtUtc, job.Saved, job.Error);

    private sealed record IngestWorkItem(Guid JobId, string FilePath, string File);
}
