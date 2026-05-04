using FluentAssertions;
using Synapse.Foundation.Shared;
using Synapse.Ingestion.South.Adapter.Repositories;

namespace Synapse.Ingestion.Tests.South.Adapter;

public class SourceItemFileAdapterTests : IDisposable
{
    private readonly string _tempDir;

    public SourceItemFileAdapterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"synapse-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public async Task Save_and_load_roundtrip()
    {
        var adapter = new SourceItemFileAdapter(_tempDir);
        var date = new DateOnly(2026, 5, 5);
        var items = new[]
        {
            new SourceItem(
                new ExternalId("github:test/repo"),
                SourceType.GitHubTrending,
                "Test Repo",
                new Uri("https://github.com/test/repo"),
                "A test repo",
                new Dictionary<string, string> { ["stars"] = "50" },
                DateTimeOffset.UtcNow
            )
        };

        await adapter.SaveAsync(date, items);
        var loaded = await adapter.LoadAsync(date);

        loaded.Should().HaveCount(1);
        loaded[0].ExternalId.Value.Should().Be("github:test/repo");
        loaded[0].Title.Should().Be("Test Repo");
    }

    [Fact]
    public async Task Load_should_return_empty_when_no_file_exists()
    {
        var adapter = new SourceItemFileAdapter(_tempDir);
        var loaded = await adapter.LoadAsync(new DateOnly(2099, 1, 1));
        loaded.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_should_deduplicate_by_external_id()
    {
        var adapter = new SourceItemFileAdapter(_tempDir);
        var date = new DateOnly(2026, 5, 5);
        var first = new[]
        {
            new SourceItem(new ExternalId("github:a/b"), SourceType.GitHubTrending,
                "A", new Uri("https://github.com/a/b"), "", new(), DateTimeOffset.UtcNow)
        };
        var second = new[]
        {
            new SourceItem(new ExternalId("github:a/b"), SourceType.GitHubTrending,
                "A Updated", new Uri("https://github.com/a/b"), "", new(), DateTimeOffset.UtcNow),
            new SourceItem(new ExternalId("github:c/d"), SourceType.GitHubTrending,
                "C", new Uri("https://github.com/c/d"), "", new(), DateTimeOffset.UtcNow)
        };

        await adapter.SaveAsync(date, first);
        await adapter.SaveAsync(date, second);
        var loaded = await adapter.LoadAsync(date);

        loaded.Should().HaveCount(2);
        loaded.Should().ContainSingle(x =>
            x.ExternalId.Value == "github:a/b" && x.Title == "A Updated");
        loaded.Should().ContainSingle(x =>
            x.ExternalId.Value == "github:c/d");
    }
}
