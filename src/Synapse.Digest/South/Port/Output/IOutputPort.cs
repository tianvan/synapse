using Synapse.Foundation.Stereotype;

namespace Synapse.Digest.South.Port.Output;

[Port]
public interface IOutputPort
{
    OutputChannel Channel { get; }
    Task<bool> DeliverAsync(Digest digest, CancellationToken ct = default);
}
