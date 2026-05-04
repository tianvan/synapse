using FluentAssertions;
using Synapse.Foundation.Shared;
using Synapse.Ingestion.South.Adapter.Sources;

namespace Synapse.Ingestion.Tests.South.Adapter;

public class HackerNewsAdapterTests
{
    [Fact]
    public void SourceType_should_be_HackerNews()
    {
        var adapter = new HackerNewsAdapter();
        adapter.Type.Should().Be(SourceType.HackerNews);
    }

    [Fact]
    public void ExternalId_format_is_hn_prefix()
    {
        var id = new ExternalId("hn:37854123");
        id.Value.Should().Be("hn:37854123");
    }
}
