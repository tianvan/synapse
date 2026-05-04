using Synapse.Foundation.Stereotype;

namespace Synapse.Digest.Domain;

[Aggregate]
public sealed record Digest(
    DateOnly Id,
    DateTimeOffset GeneratedAt,
    List<AnalyzedItem> Items,
    string Summary,
    DigestStatus Status
);
