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

public interface IIngestJobService
{
    Task<IngestStartResponse> EnqueueAsync(string filePath, string fileName, CancellationToken ct);
    Task<IngestJobStatusResponse?> GetAsync(Guid jobId, CancellationToken ct);
    Task<IngestOverviewResponse> GetOverviewAsync(CancellationToken ct);
}
