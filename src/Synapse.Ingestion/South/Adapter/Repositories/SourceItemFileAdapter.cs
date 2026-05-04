using System.Text.Json;
using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;
using Synapse.Ingestion.South.Port.Repository;

namespace Synapse.Ingestion.South.Adapter.Repositories;

[Adapter]
public class SourceItemFileAdapter : ISourceItemRepository
{
    private readonly string _basePath;

    public SourceItemFileAdapter(string basePath) => _basePath = basePath;

    public async Task SaveAsync(DateOnly date, IEnumerable<SourceItem> items,
        CancellationToken ct = default)
    {
        var dir = Path.Combine(_basePath, "data", "raw", date.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dir);

        foreach (var group in items.GroupBy(i => i.Type))
        {
            var fileName = $"{group.Key.ToString().ToLowerInvariant()}.json";
            var filePath = Path.Combine(dir, fileName);

            List<SourceItem> existing = new();
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath, ct);
                existing = JsonSerializer.Deserialize<List<SourceItem>>(json) ?? new();
            }

            var merged = existing
                .Where(e => !group.Any(g => g.ExternalId == e.ExternalId))
                .Concat(group)
                .ToList();

            var options = new JsonSerializerOptions { WriteIndented = true };
            var mergedJson = JsonSerializer.Serialize(merged, options);
            await File.WriteAllTextAsync(filePath, mergedJson, ct);
        }
    }

    public async Task<IReadOnlyList<SourceItem>> LoadAsync(DateOnly date,
        CancellationToken ct = default)
    {
        var dir = Path.Combine(_basePath, "data", "raw", date.ToString("yyyy-MM-dd"));
        if (!Directory.Exists(dir)) return Array.Empty<SourceItem>();

        var results = new List<SourceItem>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file, ct);
            var items = JsonSerializer.Deserialize<List<SourceItem>>(json);
            if (items is not null) results.AddRange(items);
        }
        return results;
    }
}
