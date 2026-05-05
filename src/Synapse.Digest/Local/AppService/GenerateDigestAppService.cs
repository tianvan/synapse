using Synapse.Foundation.Shared;
using Synapse.Digest.Domain;
using Synapse.Digest.Local.Message;
using Synapse.Digest.South.Port.Analyzer;
using Synapse.Digest.South.Port.Output;
using Synapse.Digest.South.Port.Repository;
using ISourceItemRepository = Synapse.Ingestion.South.Port.Repository.ISourceItemRepository;

namespace Synapse.Digest.Local.AppService;

public class GenerateDigestAppService
{
    private readonly ISourceItemRepository _sourceRepo;
    private readonly IAnalyzer _analyzer;
    private readonly IEnumerable<IOutputPort> _outputs;
    private readonly IDigestRepository _digestRepo;

    public GenerateDigestAppService(
        ISourceItemRepository sourceRepo,
        IAnalyzer analyzer,
        IEnumerable<IOutputPort> outputs,
        IDigestRepository digestRepo)
    {
        _sourceRepo = sourceRepo;
        _analyzer = analyzer;
        _outputs = outputs;
        _digestRepo = digestRepo;
    }

    public async Task<GenerateDigestResult> ExecuteAsync(
        GenerateDigestCommand command, CancellationToken ct = default)
    {
        var sources = await _sourceRepo.LoadAsync(command.Date, ct);

        if (sources.Count == 0)
        {
            var emptyDigest = new Digest(command.Date, DateTimeOffset.UtcNow,
                [], "今日无数据", DigestStatus.Published);
            await _digestRepo.SaveAsync(emptyDigest, ct);
            return new GenerateDigestResult(DigestGenerationStatus.Published,
                0, null, Array.Empty<DeliveryStepResult>());
        }

        var analyzedItems = new List<AnalyzedItem>();
        foreach (var source in sources)
        {
            try { analyzedItems.Add(await _analyzer.AnalyzeAsync(source, ct)); }
            catch
            {
                analyzedItems.Add(new AnalyzedItem(source.ExternalId, "未分类",
                    new TechStack(Array.Empty<string>()),
                    new Highlight(source.Description.Length > 120
                        ? source.Description[..120] : source.Description), "", 0));
            }
        }

        var sorted = analyzedItems.OrderByDescending(i => i.Score).ToList();
        var summary = sorted.Count > 0
            ? $"今日共 {sorted.Count} 条技术资讯，最高评分 {sorted.Max(i => i.Score)}/10"
            : "今日无资讯";

        var digest = new Digest(command.Date, DateTimeOffset.UtcNow,
            sorted, summary, DigestStatus.Published);
        await _digestRepo.SaveAsync(digest, ct);

        var deliveryResults = new List<DeliveryStepResult>();
        foreach (var output in _outputs)
        {
            try
            {
                var success = await output.DeliverAsync(digest, ct);
                deliveryResults.Add(new DeliveryStepResult(
                    output.Channel.ToString(), success,
                    success ? null : "Delivery returned false"));
            }
            catch (Exception ex)
            {
                deliveryResults.Add(new DeliveryStepResult(
                    output.Channel.ToString(), false, ex.Message));
            }
        }

        return new GenerateDigestResult(DigestGenerationStatus.Published,
            sorted.Count, null, deliveryResults);
    }
}
