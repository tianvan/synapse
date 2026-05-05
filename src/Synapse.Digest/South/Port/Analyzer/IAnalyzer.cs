using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;
using Synapse.Digest.Domain;

namespace Synapse.Digest.South.Port.Analyzer;

[Port]
public interface IAnalyzer
{
    Task<AnalyzedItem> AnalyzeAsync(SourceItem source, CancellationToken ct = default);
}
