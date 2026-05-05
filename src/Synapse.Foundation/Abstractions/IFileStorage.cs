namespace Synapse.Foundation.Abstractions;

public interface IFileStorage
{
    Task SaveAsync<T>(string relativePath, T data, CancellationToken ct = default);
    Task<T?> LoadAsync<T>(string relativePath, CancellationToken ct = default);
    Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default);
}
