using FluentAssertions;
using Synapse.Foundation.Shared;
using Synapse.Digest.Domain;
using Synapse.Digest.Local.AppService;
using Synapse.Digest.Local.Message;
using Synapse.Digest.South.Port.Analyzer;
using Synapse.Digest.South.Port.Output;
using Synapse.Digest.South.Port.Repository;
using Synapse.Digest.South.Adapter.Repositories;
using ISourceItemRepository = Synapse.Ingestion.South.Port.Repository.ISourceItemRepository;

namespace Synapse.Digest.Tests.Local.AppService;

public class GenerateDigestAppServiceTests
{
    [Fact]
    public async Task Should_load_sources_analyze_and_save_digest()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"synapse-digest-app-{Guid.NewGuid()}");

        var sourceRepo = new Synapse.Ingestion.South.Adapter.Repositories.SourceItemFileAdapter(tempDir);
        var sources = new[]
        {
            new SourceItem(new ExternalId("github:a/b"), SourceType.GitHubTrending,
                "Test Repo", new Uri("https://github.com/a/b"), "desc",
                new() { ["stars"] = "100" }, DateTimeOffset.UtcNow),
            new SourceItem(new ExternalId("hn:123"), SourceType.HackerNews,
                "HN Item", new Uri("https://example.com"), "desc",
                new() { ["score"] = "50" }, DateTimeOffset.UtcNow)
        };
        await sourceRepo.SaveAsync(new DateOnly(2026, 5, 5), sources);

        var digestRepo = new DigestFileAdapter(tempDir);
        var analyzer = new FakeAnalyzer();
        var outputs = new[] { new FakeOutputPort() };

        var service = new GenerateDigestAppService(sourceRepo, analyzer, outputs, digestRepo);
        var result = await service.ExecuteAsync(new GenerateDigestCommand(new DateOnly(2026, 5, 5)));

        result.Status.Should().Be(DigestGenerationStatus.Published);
        result.TotalItems.Should().Be(2);
        result.DeliveryResults[0].Success.Should().BeTrue();

        var saved = await digestRepo.GetAsync(new DateOnly(2026, 5, 5));
        saved!.Items.Should().HaveCount(2);

        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task Should_degrade_on_analyzer_failure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"synapse-degrade-{Guid.NewGuid()}");
        var sourceRepo = new Synapse.Ingestion.South.Adapter.Repositories.SourceItemFileAdapter(tempDir);
        await sourceRepo.SaveAsync(new DateOnly(2026, 5, 5), new[]
        {
            new SourceItem(new ExternalId("github:a/b"), SourceType.GitHubTrending,
                "Test", new Uri("https://github.com/a/b"), "A test repo", new(), DateTimeOffset.UtcNow)
        });

        var digestRepo = new DigestFileAdapter(tempDir);
        var service = new GenerateDigestAppService(sourceRepo, new FailingAnalyzer(),
            Array.Empty<IOutputPort>(), digestRepo);

        var result = await service.ExecuteAsync(new GenerateDigestCommand(new DateOnly(2026, 5, 5)));

        result.Status.Should().Be(DigestGenerationStatus.Published);
        result.TotalItems.Should().Be(1);

        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task Should_return_empty_when_no_sources()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"synapse-empty-{Guid.NewGuid()}");
        var sourceRepo = new Synapse.Ingestion.South.Adapter.Repositories.SourceItemFileAdapter(tempDir);
        var digestRepo = new DigestFileAdapter(tempDir);
        var service = new GenerateDigestAppService(sourceRepo, new FakeAnalyzer(),
            Array.Empty<IOutputPort>(), digestRepo);

        var result = await service.ExecuteAsync(new GenerateDigestCommand(new DateOnly(2099, 1, 1)));
        result.TotalItems.Should().Be(0);

        try { Directory.Delete(tempDir, true); } catch { }
    }
}

public class FakeAnalyzer : IAnalyzer
{
    public Task<AnalyzedItem> AnalyzeAsync(SourceItem source, CancellationToken ct = default)
        => Task.FromResult(new AnalyzedItem(source.ExternalId, "tool",
            new TechStack(["go"]), new Highlight("Worth checking out"), "", "general", 7));
}

public class FailingAnalyzer : IAnalyzer
{
    public Task<AnalyzedItem> AnalyzeAsync(SourceItem source, CancellationToken ct = default)
        => throw new InvalidOperationException("API unavailable");
}

public class FakeOutputPort : IOutputPort
{
    public OutputChannel Channel => OutputChannel.WeCom;
    public Task<bool> DeliverAsync(Digest digest, CancellationToken ct = default)
        => Task.FromResult(true);
}
