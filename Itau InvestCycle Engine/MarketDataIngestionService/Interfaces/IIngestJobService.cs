namespace MarketDataIngestionService.Interfaces;

public sealed record IngestStartResponse(Guid JobId, string File, string Status, DateTime CreatedAtUtc);

public sealed record IngestJobStatusResponse(
    Guid JobId,
    string File,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? FinishedAtUtc,
    int Saved,
    string? Error);

public sealed record IngestOverviewResponse(
    bool HasProcessing,
    int ProcessingCount,
    IngestJobStatusResponse? LastJob);

public sealed record IngestHistoryItemResponse(
    string File,
    int Saved,
    DateTime DataHoraUtc);

public sealed record IngestHistoryResponse(IReadOnlyList<IngestHistoryItemResponse> Ingestoes);

public interface IIngestJobService
{
    Task<IngestStartResponse> EnqueueAsync(string filePath, string fileName, CancellationToken ct);
    Task<IngestJobStatusResponse?> GetAsync(Guid jobId, CancellationToken ct);
    Task<IngestOverviewResponse> GetOverviewAsync(CancellationToken ct);
    Task<IngestHistoryResponse> GetRecentAsync(int take, CancellationToken ct);
}
