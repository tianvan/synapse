using Synapse.Foundation.Stereotype;
using Synapse.Ingestion.Local.Message;
using Synapse.Ingestion.South.Port.Repository;
using Synapse.Ingestion.South.Port.SourceReader;

namespace Synapse.Ingestion.Local.AppService;

[DomainService]
public class IngestAppService
{
    private readonly IEnumerable<ISourceReader> _readers;
    private readonly ISourceItemRepository _repository;

    public IngestAppService(
        IEnumerable<ISourceReader> readers,
        ISourceItemRepository repository)
    {
        _readers = readers;
        _repository = repository;
    }

    public async Task<IngestResult> ExecuteAsync(
        IngestCommand command, CancellationToken ct = default)
    {
        var steps = new List<IngestStepResult>();
        var allItems = new List<Foundation.Shared.SourceItem>();

        foreach (var reader in _readers)
        {
            try
            {
                var items = await reader.FetchAsync(ct);
                allItems.AddRange(items);
                steps.Add(new IngestStepResult(
                    reader.Type.ToString(), IngestStatus.Ok, items.Count, null));
            }
            catch (Exception ex)
            {
                steps.Add(new IngestStepResult(
                    reader.Type.ToString(), IngestStatus.Error, 0, ex.Message));
            }
        }

        if (allItems.Count > 0)
            await _repository.SaveAsync(command.Date, allItems, ct);

        var hasAnySuccess = steps.Any(s => s.Status == IngestStatus.Ok);

        return new IngestResult(
            TotalFetched: allItems.Count,
            Status: hasAnySuccess ? IngestStatus.Ok : IngestStatus.Error,
            ErrorMessage: hasAnySuccess ? null : "All sources failed to fetch",
            Steps: steps
        );
    }
}
