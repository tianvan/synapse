namespace Synapse.Digest.Local.Message;

public sealed record GenerateDigestResult(
    DigestGenerationStatus Status,
    int TotalItems,
    string? ErrorMessage,
    IReadOnlyList<DeliveryStepResult> DeliveryResults
);

public sealed record DeliveryStepResult(string Channel, bool Success, string? Error);

public enum DigestGenerationStatus { Published, Failed }
