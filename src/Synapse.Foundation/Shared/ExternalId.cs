namespace Synapse.Foundation.Shared;

public sealed record ExternalId
{
    public string Value { get; }

    public ExternalId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public override string ToString() => Value;
}
