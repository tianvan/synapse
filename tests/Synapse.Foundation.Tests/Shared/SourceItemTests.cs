using FluentAssertions;
using Synapse.Foundation.Shared;

namespace Synapse.Foundation.Tests.Shared;

public class SourceItemTests
{
    [Fact]
    public void Should_create_source_item()
    {
        var item = new SourceItem(
            ExternalId: new ExternalId("github:test/repo"),
            Type: SourceType.GitHubTrending,
            Title: "Test Repo",
            Url: new Uri("https://github.com/test/repo"),
            Description: "A test repository",
            Metadata: new Dictionary<string, string> { ["stars"] = "100" },
            FetchedAt: DateTimeOffset.UtcNow
        );

        item.ExternalId.Value.Should().Be("github:test/repo");
        item.Type.Should().Be(SourceType.GitHubTrending);
        item.Metadata["stars"].Should().Be("100");
    }

    [Fact]
    public void SourceType_enum_values_exist()
    {
        Enum.GetValues<SourceType>().Should().Contain(SourceType.GitHubTrending);
        Enum.GetValues<SourceType>().Should().Contain(SourceType.HackerNews);
    }

    [Fact]
    public void SourceItem_with_expression_creates_modified_copy()
    {
        var original = new SourceItem(
            new ExternalId("github:a/b"), SourceType.GitHubTrending,
            "A", new Uri("https://a.com"), "desc", new(), DateTimeOffset.UtcNow);

        var modified = original with { Title = "B" };

        modified.Title.Should().Be("B");
        modified.ExternalId.Should().Be(original.ExternalId);
        original.Title.Should().Be("A"); // immutable — original unchanged
    }
}
