using FluentAssertions;
using Synapse.Digest.Domain;
using DigestRecord = global::Synapse.Digest.Digest;

namespace Synapse.Digest.Tests.Domain;

public class DomainModelTests
{
    [Fact]
    public void Highlight_equal_by_value()
    {
        new Highlight("A").Should().Be(new Highlight("A"));
        new Highlight("A").Should().NotBe(new Highlight("B"));
    }

    [Fact]
    public void TechStack_equal_by_value()
    {
        new TechStack(["a", "b"]).Should().Be(new TechStack(["a", "b"]));
        new TechStack(["a"]).Should().NotBe(new TechStack(["b"]));
    }

    [Fact]
    public void AnalyzedItem_is_immutable_record()
    {
        var item = new AnalyzedItem(
            SourceRef: new Foundation.Shared.ExternalId("github:a/b"),
            Category: "tool",
            TechStack: new TechStack(["rust"]),
            Highlight: new Highlight("Faster builds"),
            Suitability: "production ready",
            Score: 8
        );

        item.Category.Should().Be("tool");
        item.Score.Should().Be(8);

        var modified = item with { Score = 9 };
        modified.Score.Should().Be(9);
        item.Score.Should().Be(8); // immutable — original unchanged
    }

    [Fact]
    public void Digest_default_status_is_pending()
    {
        var digest = new DigestRecord(
            new DateOnly(2026, 5, 5),
            DateTimeOffset.UtcNow,
            [],
            "",
            DigestStatus.Pending
        );
        digest.Status.Should().Be(DigestStatus.Pending);
    }
}
