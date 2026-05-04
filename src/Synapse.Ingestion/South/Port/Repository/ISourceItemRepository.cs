using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;

namespace Synapse.Ingestion.South.Port.Repository;

[Port]
public interface ISourceItemRepository
{
    Task SaveAsync(DateOnly date, IEnumerable<SourceItem> items, CancellationToken ct = default);
    Task<IReadOnlyList<SourceItem>> LoadAsync(DateOnly date, CancellationToken ct = default);
}
