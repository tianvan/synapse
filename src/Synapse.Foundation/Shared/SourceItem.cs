namespace Synapse.Foundation.Shared;

public sealed record SourceItem(
    ExternalId ExternalId,
    SourceType Type,
    string Title,
    Uri Url,
    string Description,
    Dictionary<string, string> Metadata,
    DateTimeOffset FetchedAt
);
