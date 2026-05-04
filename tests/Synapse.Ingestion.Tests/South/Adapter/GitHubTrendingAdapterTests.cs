using FluentAssertions;
using Synapse.Foundation.Shared;
using Synapse.Ingestion.South.Adapter.Sources;

namespace Synapse.Ingestion.Tests.South.Adapter;

public class GitHubTrendingAdapterTests
{
    [Fact]
    public void SourceType_should_be_GitHubTrending()
    {
        var adapter = new GitHubTrendingAdapter();
        adapter.Type.Should().Be(SourceType.GitHubTrending);
    }

    [Fact]
    public void ExternalId_format_is_github_prefix()
    {
        var id = new ExternalId("github:dotnet/runtime");
        id.Value.Should().Be("github:dotnet/runtime");
    }
}
