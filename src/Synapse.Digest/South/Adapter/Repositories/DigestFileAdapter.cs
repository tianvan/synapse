using System.Text.Json;
using Synapse.Foundation.Stereotype;
using Synapse.Digest.Domain;
using Synapse.Digest.South.Port.Repository;

namespace Synapse.Digest.South.Adapter.Repositories;

[Adapter]
public class DigestFileAdapter : IDigestRepository
{
    private readonly string _basePath;

    public DigestFileAdapter(string basePath) => _basePath = basePath;

    public async Task SaveAsync(Digest digest, CancellationToken ct = default)
    {
        var dir = Path.Combine(_basePath, "data", "digests");
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"{digest.Id:yyyy-MM-dd}.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(digest, options);
        await File.WriteAllTextAsync(filePath, json, ct);
    }

    public async Task<Digest?> GetAsync(DateOnly date, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, "data", "digests", $"{date:yyyy-MM-dd}.json");
        if (!File.Exists(filePath)) return null;
        var json = await File.ReadAllTextAsync(filePath, ct);
        return JsonSerializer.Deserialize<Digest>(json);
    }
}
