using FluentAssertions;
using Synapse.Digest.Domain;
using Synapse.Digest.South.Adapter.Repositories;
using Synapse.Foundation.Shared;

namespace Synapse.Digest.Tests.South.Adapter;

public class DigestFileAdapterTests : IDisposable
{
    private readonly string _tempDir;

    public DigestFileAdapterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"synapse-digest-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public async Task Save_and_load_roundtrip()
    {
        var adapter = new DigestFileAdapter(_tempDir);
        var digest = new Digest(
            Id: new DateOnly(2026, 5, 5),
            GeneratedAt: DateTimeOffset.UtcNow,
            Items: [new AnalyzedItem(
                new ExternalId("github:test/repo"), "tool",
                new TechStack(["rust"]), new Highlight("Very fast"),
                "cli tools", 8)],
            Summary: "Today's top projects",
            Status: DigestStatus.Published
        );

        await adapter.SaveAsync(digest);
        var loaded = await adapter.GetAsync(new DateOnly(2026, 5, 5));

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(new DateOnly(2026, 5, 5));
        loaded.Summary.Should().Be("Today's top projects");
        loaded.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Get_should_return_null_when_no_file()
    {
        var adapter = new DigestFileAdapter(_tempDir);
        var result = await adapter.GetAsync(new DateOnly(2099, 1, 1));
        result.Should().BeNull();
    }
}
