using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;

namespace Synapse.Ingestion.South.Port.SourceReader;

[Port]
public interface ISourceReader
{
    SourceType Type { get; }
    Task<IReadOnlyList<SourceItem>> FetchAsync(CancellationToken ct = default);
}
