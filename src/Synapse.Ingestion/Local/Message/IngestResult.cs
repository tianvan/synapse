namespace Synapse.Ingestion.Local.Message;

public sealed record IngestResult(
    int TotalFetched,
    IngestStatus Status,
    string? ErrorMessage,
    IReadOnlyList<IngestStepResult> Steps
);

public sealed record IngestStepResult(
    string SourceName,
    IngestStatus Status,
    int ItemCount,
    string? Error
);

public enum IngestStatus { Ok, Error }
