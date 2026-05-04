namespace Synapse.Ingestion.Local.Message;

public sealed record IngestCommand(string SourceFilter, DateOnly Date);
