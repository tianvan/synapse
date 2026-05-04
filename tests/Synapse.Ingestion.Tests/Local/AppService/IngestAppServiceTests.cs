using FluentAssertions;
using Synapse.Foundation.Shared;
using Synapse.Ingestion.Local.AppService;
using Synapse.Ingestion.Local.Message;
using Synapse.Ingestion.South.Adapter.Repositories;
using Synapse.Ingestion.South.Port.SourceReader;

namespace Synapse.Ingestion.Tests.Local.AppService;

public class IngestAppServiceTests
{
    [Fact]
    public async Task Should_fetch_from_all_readers_and_save()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"synapse-ingest-{Guid.NewGuid()}");
        var repo = new SourceItemFileAdapter(tempDir);
        var readers = new ISourceReader[]
        {
            new FakeSourceReader(SourceType.GitHubTrending,
                new SourceItem(new ExternalId("github:a/b"), SourceType.GitHubTrending,
                    "A/B", new Uri("https://github.com/a/b"), "Test",
                    new(), DateTimeOffset.UtcNow)),
            new FakeSourceReader(SourceType.HackerNews,
                new SourceItem(new ExternalId("hn:123"), SourceType.HackerNews,
                    "HN Item", new Uri("https://news.ycombinator.com/item?id=123"), "Test",
                    new(), DateTimeOffset.UtcNow))
        };

        var service = new IngestAppService(readers, repo);
        var command = new IngestCommand(SourceFilter: "all",
            Date: new DateOnly(2026, 5, 5));
        var result = await service.ExecuteAsync(command);

        result.Status.Should().Be(IngestStatus.Ok);
        result.TotalFetched.Should().Be(2);
        result.Steps.Should().HaveCount(2);
        result.Steps.All(s => s.Status == IngestStatus.Ok).Should().BeTrue();

        var loaded = await repo.LoadAsync(new DateOnly(2026, 5, 5));
        loaded.Should().HaveCount(2);

        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task Should_continue_on_one_reader_failure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"synapse-ingest-{Guid.NewGuid()}");
        var repo = new SourceItemFileAdapter(tempDir);
        var readers = new ISourceReader[]
        {
            new FailingSourceReader(SourceType.GitHubTrending),
            new FakeSourceReader(SourceType.HackerNews,
                new SourceItem(new ExternalId("hn:123"), SourceType.HackerNews,
                    "HN Item", new Uri("https://news.ycombinator.com/item?id=123"), "Test",
                    new(), DateTimeOffset.UtcNow))
        };

        var service = new IngestAppService(readers, repo);
        var command = new IngestCommand(SourceFilter: "all",
            Date: new DateOnly(2026, 5, 5));
        var result = await service.ExecuteAsync(command);

        result.TotalFetched.Should().Be(1);
        result.Steps.Should().ContainSingle(s => s.Status == IngestStatus.Error);
        result.Steps.Should().ContainSingle(s => s.Status == IngestStatus.Ok);

        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task Should_return_error_when_all_readers_fail()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"synapse-ingest-{Guid.NewGuid()}");
        var repo = new SourceItemFileAdapter(tempDir);
        var readers = new ISourceReader[]
        {
            new FailingSourceReader(SourceType.GitHubTrending),
            new FailingSourceReader(SourceType.HackerNews)
        };

        var service = new IngestAppService(readers, repo);
        var result = await service.ExecuteAsync(
            new IngestCommand(SourceFilter: "all", Date: new DateOnly(2026, 5, 5)));

        result.Status.Should().Be(IngestStatus.Error);
        result.ErrorMessage.Should().NotBeNull();

        try { Directory.Delete(tempDir, true); } catch { }
    }
}

public class FakeSourceReader : ISourceReader
{
    public SourceType Type { get; }
    private readonly SourceItem[] _items;
    public FakeSourceReader(SourceType type, params SourceItem[] items)
    { Type = type; _items = items; }

    public Task<IReadOnlyList<SourceItem>> FetchAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SourceItem>>(_items);
}

public class FailingSourceReader : ISourceReader
{
    public SourceType Type { get; }
    public FailingSourceReader(SourceType type) => Type = type;

    public Task<IReadOnlyList<SourceItem>> FetchAsync(CancellationToken ct = default)
        => throw new HttpRequestException("Connection failed");
}
