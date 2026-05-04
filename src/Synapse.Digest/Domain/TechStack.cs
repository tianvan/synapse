namespace Synapse.Digest.Domain;

public sealed record TechStack(IReadOnlyList<string> Tags)
{
    public bool Equals(TechStack? other) =>
        other is not null && Tags.SequenceEqual(other.Tags);

    public override int GetHashCode()
    {
        var hc = new HashCode();
        foreach (var tag in Tags)
            hc.Add(tag);
        return hc.ToHashCode();
    }
}
