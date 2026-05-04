using System.Text.Json;
using Synapse.Foundation.Abstractions;

namespace Synapse.Ingestion.South.Adapter;

public class FileSystemStorage : IFileStorage
{
    private readonly string _basePath;

    public FileSystemStorage(string basePath) => _basePath = basePath;

    public async Task SaveAsync<T>(string relativePath, T data, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, relativePath);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(data, options);
        await File.WriteAllTextAsync(fullPath, json, ct);
    }

    public async Task<T?> LoadAsync<T>(string relativePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, relativePath);
        if (!File.Exists(fullPath)) return default;
        var json = await File.ReadAllTextAsync(fullPath, ct);
        return JsonSerializer.Deserialize<T>(json);
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }
}
