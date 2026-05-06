using FluentAssertions;
using Synapse.Foundation.Shared;
using Synapse.Digest.South.Adapter.Analyzers;

namespace Synapse.Digest.Tests.South.Adapter;

public class OpenAIAnalyzerAdapterTests
{
    [Fact]
    public void BuildPrompt_should_replace_all_placeholders()
    {
        var source = new SourceItem(
            new ExternalId("github:a/b"),
            SourceType.GitHubTrending,
            "My Project",
            new Uri("https://github.com/a/b"),
            "A great project description",
            new() { ["stars"] = "3000", ["language"] = "Rust" },
            DateTimeOffset.UtcNow
        );

        var template = "项目: {{Title}}\n描述: {{Description}}\n{{Stars}}{{Score}}语言: {{Language}}";
        var result = OpenAIAnalyzerAdapter.BuildPrompt(source, template);

        result.Should().Contain("项目: My Project");
        result.Should().Contain("描述: A great project description");
        result.Should().Contain("Stars: 3000");
        result.Should().Contain("语言: Rust");
        result.Should().NotContain("{{Title}}");
        result.Should().NotContain("{{Stars}}");
    }

    [Fact]
    public void BuildPrompt_should_handle_missing_metadata()
    {
        var source = new SourceItem(
            new ExternalId("hn:123"),
            SourceType.HackerNews,
            "HN Item",
            new Uri("https://example.com"),
            "HN Item",
            new(),
            DateTimeOffset.UtcNow
        );

        var template = "{{Title}}|{{Language}}|{{Stars}}|{{Score}}";
        var result = OpenAIAnalyzerAdapter.BuildPrompt(source, template);

        result.Should().Be("HN Item|||");
    }

    [Fact]
    public void BuildPrompt_should_prefix_hn_score()
    {
        var source = new SourceItem(
            new ExternalId("hn:456"),
            SourceType.HackerNews,
            "HN Story",
            new Uri("https://example.com"),
            "HN Story",
            new() { ["score"] = "250" },
            DateTimeOffset.UtcNow
        );

        var result = OpenAIAnalyzerAdapter.BuildPrompt(source, "{{Score}}");
        result.Should().Contain("HN Score: 250");
    }

    [Fact]
    public void ParseResponse_should_extract_all_fields_including_description()
    {
        var json = @"{
            ""category"": ""tool"",
            ""techStack"": [""Rust"", ""WASM""],
            ""highlight"": ""构建速度提升10倍"",
            ""description"": ""基于 Rust 的下一代前端构建工具，利用 WASM 实现跨平台一致体验"",
            ""suitability"": ""中大型前端项目的构建优化"",
            ""score"": 8
        }";

        var source = new SourceItem(new ExternalId("github:a/b"), SourceType.GitHubTrending,
            "a/b", new Uri("https://github.com/a/b"), "desc", new(), DateTimeOffset.UtcNow);

        var result = OpenAIAnalyzerAdapter.ParseResponse(json, source);

        result.Category.Should().Be("tool");
        result.TechStack.Tags.Should().Equal(["Rust", "WASM"]);
        result.Highlight.Text.Should().Be("构建速度提升10倍");
        result.Description.Should().Be("基于 Rust 的下一代前端构建工具，利用 WASM 实现跨平台一致体验");
        result.Suitability.Should().Be("中大型前端项目的构建优化");
        result.Score.Should().Be(8);
    }

    [Fact]
    public void Degrade_should_truncate_description_to_150_chars()
    {
        var longDesc = new string('x', 200);
        var source = new SourceItem(new ExternalId("github:a/b"), SourceType.GitHubTrending,
            "a/b", new Uri("https://github.com/a/b"), longDesc, new(), DateTimeOffset.UtcNow);

        var result = OpenAIAnalyzerAdapter.Degrade(source);

        result.Category.Should().Be("未分类");
        result.Description.Should().Be(new string('x', 150));
        result.Score.Should().Be(0);
    }

    [Fact]
    public void Degrade_should_keep_short_description_unchanged()
    {
        var source = new SourceItem(new ExternalId("github:a/b"), SourceType.GitHubTrending,
            "a/b", new Uri("https://github.com/a/b"), "Short desc", new(), DateTimeOffset.UtcNow);

        var result = OpenAIAnalyzerAdapter.Degrade(source);

        result.Description.Should().Be("Short desc");
        result.Highlight.Text.Should().Be("Short desc");
    }
}
