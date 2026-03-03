namespace ClassLibrary.Domain.Entities;

public sealed class IngestaoJob
{
    public Guid Id { get; set; }
    public string File { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string Status { get; set; } = "QUEUED";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public int Saved { get; set; }
    public string? Error { get; set; }
}

