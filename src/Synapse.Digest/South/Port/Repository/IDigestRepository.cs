using Synapse.Foundation.Stereotype;

namespace Synapse.Digest.South.Port.Repository;

[Port]
public interface IDigestRepository
{
    Task SaveAsync(Digest.Domain.Digest digest, CancellationToken ct = default);
    Task<Digest.Domain.Digest?> GetAsync(DateOnly date, CancellationToken ct = default);
}
