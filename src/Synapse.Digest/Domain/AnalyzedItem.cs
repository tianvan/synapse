using Synapse.Foundation.Shared;

namespace Synapse.Digest.Domain;

public sealed record AnalyzedItem(
    ExternalId SourceRef,
    string Category,
    TechStack TechStack,
    Highlight Highlight,
    string Description,
    string Suitability,
    int Score
);
