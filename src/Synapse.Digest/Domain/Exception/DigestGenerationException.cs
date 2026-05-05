namespace Synapse.Digest.Domain.Exception;

public class DigestGenerationException(string message)
    : Foundation.Exception.DomainException(message);
