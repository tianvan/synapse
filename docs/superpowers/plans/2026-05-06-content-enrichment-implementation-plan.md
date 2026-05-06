# Content Enrichment & Prompt Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enrich source descriptions at ingestion, add AI-generated description field to analysis, inline suitability display, and make prompts configurable via IOptions.

**Architecture:** Changes span three layers — ingestion adapters fetch richer source data (GitHub API for repo descriptions/topics, HN `text` field for self-posts), the analyzer uses configurable prompt templates with `{{placeholder}}` substitution and outputs a new `Description` field, and the display layer removes `<details>` folding and surfaces descriptions directly on cards.

**Tech Stack:** C# 10, .NET 10, xUnit + FluentAssertions, plain `string.Replace` for templates (no template engine)

---

### Task 1: Domain model changes

**Files:**
- Modify: `src/Synapse.Digest/Domain/AnalyzedItem.cs`
- Modify: `src/Synapse.Digest/South/Adapter/Analyzers/OpenAIOptions.cs`
- Modify: `tests/Synapse.Digest.Tests/Domain/DomainModelTests.cs`

- [ ] **Step 1: Add Description field to AnalyzedItem**

In `src/Synapse.Digest/Domain/AnalyzedItem.cs`:

```csharp
namespace Synapse.Digest.Domain;

public sealed record AnalyzedItem(
    ExternalId SourceRef,
    string Category,
    TechStack TechStack,
    Highlight Highlight,
    string Description,
    string Suitability,
    int Score
);
```

- [ ] **Step 2: Add SystemPrompt and UserPromptTemplate to OpenAIOptions**

In `src/Synapse.Digest/South/Adapter/Analyzers/OpenAIOptions.cs`:

```csharp
namespace Synapse.Digest.South.Adapter.Analyzers;

public class OpenAIOptions
{
    public const string Section = "OpenAI";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.deepseek.com/";
    public string Model { get; set; } = "deepseek-v4-pro";

    public string SystemPrompt { get; set; } =
        "你是一位资深技术编辑，擅长用精炼且有人情味的语言点评技术项目。\n" +
        "你的分析准确、务实，不说套话，不堆砌术语。\n" +
        "输出合法 JSON，不要 markdown。";

    public string UserPromptTemplate { get; set; } =
        "分析以下技术项目，输出 JSON：\n" +
        "{\n" +
        "  \"category\": \"framework|tool|library|article|other\",\n" +
        "  \"techStack\": [\"具体技术名\"],\n" +
        "  \"highlight\": \"一句话中文亮点，为什么值得关注（20-40字）\",\n" +
        "  \"description\": \"项目做什么、核心价值在哪（100-150字），口语化中文，像在给同事介绍\",\n" +
        "  \"suitability\": \"适合什么场景使用\",\n" +
        "  \"score\": 1-10\n" +
        "}\n\n" +
        "要求：\n" +
        "- 禁用\"赋能\"\"抓手\"\"拉通\"\"值得关注的是\"\"此外\"\"综上所述\"等套话\n" +
        "- techStack 要具体，避免只说\"AI\"，应具体到\"强化学习\"\"LLM\"\"RAG\"等\n" +
        "- description 用长短句交替，可以插入口语化过渡词（\"其实\"\"你发现没\"\"说来有趣\"）\n" +
        "- 评分严格：7 分以上必须有明显技术突破或独特设计思路\n" +
        "- 项目名、技术名词保持原文，不要翻译\n\n" +
        "项目: {{Title}}\n" +
        "描述: {{Description}}\n" +
        "{{Stars}}{{Score}}语言: {{Language}}";
}
```

- [ ] **Step 3: Update DomainModelTests to include Description**

In `tests/Synapse.Digest.Tests/Domain/DomainModelTests.cs`, replace the `AnalyzedItem_is_immutable_record` test:

```csharp
[Fact]
public void AnalyzedItem_is_immutable_record()
{
    var item = new AnalyzedItem(
        SourceRef: new Foundation.Shared.ExternalId("github:a/b"),
        Category: "tool",
        TechStack: new TechStack(["rust"]),
        Highlight: new Highlight("Faster builds"),
        Description: "A next-generation build system written in Rust, promising 10x speed improvements",
        Suitability: "production ready",
        Score: 8
    );

    item.Category.Should().Be("tool");
    item.Score.Should().Be(8);
    item.Description.Should().Be("A next-generation build system written in Rust, promising 10x speed improvements");

    var modified = item with { Score = 9 };
    modified.Score.Should().Be(9);
    item.Score.Should().Be(8);
}
```

- [ ] **Step 4: Build and run domain tests**

```bash
dotnet build src/Synapse.Digest
dotnet test tests/Synapse.Digest.Tests --filter "FullyQualifiedName~DomainModelTests"
```

Expected: Build succeeds, 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Synapse.Digest/Domain/AnalyzedItem.cs src/Synapse.Digest/South/Adapter/Analyzers/OpenAIOptions.cs tests/Synapse.Digest.Tests/Domain/DomainModelTests.cs
git commit -m "feat: add Description to AnalyzedItem and prompt fields to OpenAIOptions"
```

---

### Task 2: Update OpenAIAnalyzerAdapter

**Files:**
- Modify: `src/Synapse.Digest/South/Adapter/Analyzers/OpenAIAnalyzerAdapter.cs`
- Modify: `src/Synapse.Digest/Synapse.Digest.csproj`

- [ ] **Step 1: Make BuildPrompt, ParseResponse, Degrade internal and add InternalsVisibleTo**

In `src/Synapse.Digest/Synapse.Digest.csproj`, add after the `</PropertyGroup>` closing tag:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="Synapse.Digest.Tests" />
</ItemGroup>
```

In `OpenAIAnalyzerAdapter.cs`, change `private static` → `internal static` on `BuildPrompt`, `ParseResponse`, and `Degrade`.

- [ ] **Step 2: Replace BuildPrompt with template-based version**

Replace the entire `BuildPrompt` method:

```csharp
internal static string BuildPrompt(SourceItem source, string template)
{
    var stars = source.Metadata.TryGetValue("stars", out var s) ? s : null;
    var score = source.Metadata.TryGetValue("score", out var sc) ? sc : null;
    var language = source.Metadata.TryGetValue("language", out var l) ? l : null;

    return template
        .Replace("{{Title}}", source.Title)
        .Replace("{{Description}}", source.Description)
        .Replace("{{Language}}", language ?? "")
        .Replace("{{Stars}}", stars is not null ? $"Stars: {stars}  " : "")
        .Replace("{{Score}}", score is not null ? $"HN Score: {score}  " : "");
}
```

- [ ] **Step 3: Update AnalyzeAsync to use configurable prompts**

In `AnalyzeAsync`, change the `messages` array:

```csharp
var prompt = BuildPrompt(source, _options.UserPromptTemplate);
var requestBody = new
{
    model = _options.Model,
    messages = new[]
    {
        new { role = "system", content = _options.SystemPrompt },
        new { role = "user", content = prompt }
    },
    temperature = 0.3
};
```

- [ ] **Step 4: Update ParseResponse to extract description**

In `ParseResponse`, add description extraction after the highlight line:

```csharp
var description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";

return new AnalyzedItem(source.ExternalId, category, new TechStack(tags),
    new Highlight(highlight), description, suitability, Math.Clamp(score, 1, 10));
```

- [ ] **Step 5: Update Degrade to include Description**

Replace the Degrade method:

```csharp
internal static AnalyzedItem Degrade(SourceItem source) =>
    new(
        source.ExternalId,
        Category: "未分类",
        TechStack: new TechStack(Array.Empty<string>()),
        Highlight: new Highlight(source.Description.Length > 120
            ? source.Description[..120] : source.Description),
        Description: source.Description.Length > 150
            ? source.Description[..150] : source.Description,
        Suitability: "",
        Score: 0
    );
```

- [ ] **Step 6: Build**

```bash
dotnet build Synapse.slnx
```

Expected: Build succeeds.

- [ ] **Step 7: Commit**

```bash
git add src/Synapse.Digest/South/Adapter/Analyzers/OpenAIAnalyzerAdapter.cs src/Synapse.Digest/Synapse.Digest.csproj
git commit -m "feat: use configurable prompt templates and parse Description in analyzer"
```

---

### Task 3: Fix downstream consumers and add analyzer tests

**Files:**
- Modify: `tests/Synapse.Digest.Tests/Local/AppService/GenerateDigestAppServiceTests.cs`
- Create: `tests/Synapse.Digest.Tests/South/Adapter/OpenAIAnalyzerAdapterTests.cs`

- [ ] **Step 1: Update FakeAnalyzer to include Description**

In `GenerateDigestAppServiceTests.cs`, update `FakeAnalyzer`:

```csharp
public class FakeAnalyzer : IAnalyzer
{
    public Task<AnalyzedItem> AnalyzeAsync(SourceItem source, CancellationToken ct = default)
        => Task.FromResult(new AnalyzedItem(source.ExternalId, "tool",
            new TechStack(["go"]), new Highlight("Worth checking out"),
            "An elegant Go library that simplifies concurrent task orchestration with minimal boilerplate",
            "general", 7));
}
```

- [ ] **Step 2: Create OpenAIAnalyzerAdapterTests**

Create `tests/Synapse.Digest.Tests/South/Adapter/OpenAIAnalyzerAdapterTests.cs`:

```csharp
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

        result.Should().Be("HN Item||||");
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
}
```

- [ ] **Step 3: Run all tests**

```bash
dotnet test Synapse.slnx
```

Expected: All tests pass, including the 5 new analyzer tests and the 3 updated app service tests.

- [ ] **Step 4: Commit**

```bash
git add tests/Synapse.Digest.Tests/Local/AppService/GenerateDigestAppServiceTests.cs tests/Synapse.Digest.Tests/South/Adapter/OpenAIAnalyzerAdapterTests.cs
git commit -m "test: add OpenAIAnalyzerAdapter tests and update FakeAnalyzer"
```

---

### Task 4: Enrich GitHubTrendingAdapter with GitHub API

**Files:**
- Modify: `src/Synapse.Ingestion/South/Adapter/Sources/GitHubTrendingAdapter.cs`

- [ ] **Step 1: Add GitHub API enrichment to FetchAsync**

After creating each `SourceItem` from HTML scrape, call the GitHub REST API. Add a helper method `EnrichFromApiAsync` and call it. Add `using System.Text.Json;`.

Replace the `FetchAsync` method and add the enrichment helper:

```csharp
public async Task<IReadOnlyList<SourceItem>> FetchAsync(CancellationToken ct = default)
{
    var html = await _httpClient.GetStringAsync(
        "https://github.com/trending?since=daily", ct);

    var items = new List<SourceItem>();
    var articlePattern = @"<article\s+class=""Box-row"">(.+?)</article>";
    var matches = Regex.Matches(html, articlePattern, RegexOptions.Singleline);

    foreach (Match match in matches)
    {
        var block = match.Groups[1].Value;
        var repoMatch = Regex.Match(block, @"href=""/(""?([^/""\s]+?)/([^/""\s]+?))""");
        if (!repoMatch.Success) continue;

        var owner = repoMatch.Groups[2].Value.Trim();
        var name = repoMatch.Groups[3].Value.Trim();

        if (owner.Contains('?') || owner.Contains('%') || owner.Contains('&')
            || owner is "login" or "sponsors" or "settings" or "features" or "orgs"
            || name.Contains('?'))
            continue;

        var descMatch = Regex.Match(block,
            @"<p\s+class=""col-9[^""]*"">\s*(.+?)\s*</p>", RegexOptions.Singleline);
        var langMatch = Regex.Match(block,
            @"itemprop=""programmingLanguage"">\s*(.+?)\s*</span>");
        var starsMatch = Regex.Match(block, @"(\d[\d,]*)\s+stars");

        var metadata = new Dictionary<string, string>
        {
            ["owner"] = owner,
            ["repo"] = name
        };
        if (langMatch.Success) metadata["language"] = langMatch.Groups[1].Value.Trim();
        if (starsMatch.Success) metadata["stars"] = starsMatch.Groups[1].Value.Trim();

        var htmlDescription = descMatch.Success ? descMatch.Groups[1].Value.Trim() : "";

        var item = new SourceItem(
            new ExternalId($"github:{owner}/{name}"),
            SourceType.GitHubTrending,
            $"{owner}/{name}",
            new Uri($"https://github.com/{owner}/{name}"),
            htmlDescription,
            metadata,
            DateTimeOffset.UtcNow
        );

        items.Add(await EnrichFromApiAsync(item, owner, name, ct));
    }

    return items;
}

private async Task<SourceItem> EnrichFromApiAsync(
    SourceItem item, string owner, string name, CancellationToken ct)
{
    try
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{owner}/{name}");
        request.Headers.Add("User-Agent", "Synapse/1.0");
        request.Headers.Add("Accept", "application/vnd.github.v3+json");

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return item;

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;

        var apiDescription = root.TryGetProperty("description", out var desc)
            && desc.ValueKind == JsonValueKind.String ? desc.GetString() : null;
        var topics = new List<string>();
        if (root.TryGetProperty("topics", out var ts) && ts.ValueKind == JsonValueKind.Array)
            foreach (var t in ts.EnumerateArray()) topics.Add(t.GetString()!);

        var description = !string.IsNullOrWhiteSpace(apiDescription)
            ? apiDescription : item.Description;
        var metadata = new Dictionary<string, string>(item.Metadata);
        if (topics.Count > 0) metadata["topics"] = string.Join(", ", topics);

        return item with { Description = description, Metadata = metadata };
    }
    catch
    {
        return item;
    }
}
```

- [ ] **Step 2: Build and run ingestion tests**

```bash
dotnet build src/Synapse.Ingestion
dotnet test tests/Synapse.Ingestion.Tests
```

Expected: Build succeeds, existing tests pass.

- [ ] **Step 3: Commit**

```bash
git add src/Synapse.Ingestion/South/Adapter/Sources/GitHubTrendingAdapter.cs
git commit -m "feat: enrich GitHub trending with API description and topics"
```

---

### Task 5: Enrich HackerNewsAdapter with text field

**Files:**
- Modify: `src/Synapse.Ingestion/South/Adapter/Sources/HackerNewsAdapter.cs`

- [ ] **Step 1: Add type and text fields to HnItem, use text for description**

Add `Type` and `Text` properties to the `HnItem` record, and use `Text` as description when available:

```csharp
private sealed record HnItem(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("score")] int? Score,
    [property: JsonPropertyName("by")] string? By,
    [property: JsonPropertyName("descendants")] int? Descendants,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("text")] string? Text
);
```

Then in the `FetchAsync` foreach loop body, replace the description assignment:

```csharp
var description = item.Title ?? "";
if (item is { Type: "story", Text: not null } && !string.IsNullOrWhiteSpace(item.Text))
{
    description = item.Text.Length > 500 ? item.Text[..500] : item.Text;
}

items.Add(new SourceItem(
    new ExternalId($"hn:{id}"),
    SourceType.HackerNews,
    item.Title,
    url,
    description,
    metadata,
    DateTimeOffset.UtcNow
));
```

- [ ] **Step 2: Build and run ingestion tests**

```bash
dotnet build src/Synapse.Ingestion
dotnet test tests/Synapse.Ingestion.Tests
```

Expected: Build succeeds, existing tests pass.

- [ ] **Step 3: Commit**

```bash
git add src/Synapse.Ingestion/South/Adapter/Sources/HackerNewsAdapter.cs
git commit -m "feat: use HN text field as description for self-posts"
```

---

### Task 6: Update WeComAdapter to include description

**Files:**
- Modify: `src/Synapse.Digest/South/Adapter/Outputs/WeComAdapter.cs`

- [ ] **Step 1: Add description line to message template**

In `BuildItemChunks`, update the message format:

```csharp
private static IEnumerable<string> BuildItemChunks(Digest digest)
{
    var chunk = new StringBuilder();
    foreach (var item in digest.Items)
    {
        var desc = item.Description.Length > 80
            ? item.Description[..80] + "..." : item.Description;
        var line = $"\n> **{item.Score}/10** {item.Highlight.Text}\n" +
                   $"> {desc}\n" +
                   $"> 分类: {item.Category} | 技术: {string.Join(", ", item.TechStack.Tags)}\n";
        if (chunk.Length + line.Length > 3800)
        {
            yield return chunk.ToString();
            chunk.Clear();
        }
        chunk.Append(line);
    }
    if (chunk.Length > 0) yield return chunk.ToString();
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/Synapse.Digest
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Synapse.Digest/South/Adapter/Outputs/WeComAdapter.cs
git commit -m "feat: include description in WeCom digest messages"
```

---

### Task 7: UI changes — show description and inline suitability

**Files:**
- Modify: `pages/_includes/item-card.html`
- Modify: `pages/_includes/podium-card.html`
- Modify: `pages/assets/css/style.scss`

- [ ] **Step 1: Update item-card.html**

Replace the `details` block and add a description paragraph. Change the `<details class="card-suitability">...</details>` block to:

```html
    <p class="card-description">{{ include.item.Description }}</p>

    {% if include.item.Suitability %}
    <p class="card-suitability">🎯 {{ include.item.Suitability }}</p>
    {% endif %}
```

The full file becomes:

```html
{% assign score = include.item.Score %}
{% if score >= 9 %}
  {% assign tier_class = "tier-gold" %}
{% elsif score >= 7 %}
  {% assign tier_class = "tier-silver" %}
{% else %}
  {% assign tier_class = "" %}
{% endif %}

<article class="item-card {{ tier_class }}" data-score="{{ score }}">
  <div class="card-rank">
    <span class="rank-number">#{{ include.rank }}</span>
    <span class="rank-score">{{ score }}</span>
  </div>

  <div class="card-body">
    {% assign source = include.item.SourceRef.Value %}
    {% if source contains "github:" %}
      {% assign url = source | remove_first: "github:" | split: "/" %}
      {% assign url_final = "https://github.com/" | append: url[0] | append: "/" | append: url[1] %}
    {% elsif source contains "hn:" %}
      {% assign hn_id = source | remove_first: "hn:" %}
      {% assign url_final = "https://news.ycombinator.com/item?id=" | append: hn_id %}
    {% else %}
      {% assign url_final = "#" %}
    {% endif %}

    <h2 class="card-title">
      <a href="{{ url_final }}" target="_blank" rel="noopener noreferrer">{{ include.item.Highlight.Text }}</a>
    </h2>

    <p class="card-description">{{ include.item.Description }}</p>

    <div class="card-meta">
      <span class="category-tag {{ include.item.Category }}">{{ include.item.Category }}</span>
      {% for tag in include.item.TechStack.Tags %}
        <span class="tech-tag">{{ tag }}</span>
      {% endfor %}
    </div>

    {% if include.item.Suitability %}
    <p class="card-suitability">🎯 {{ include.item.Suitability }}</p>
    {% endif %}
  </div>
</article>
```

- [ ] **Step 2: Update podium-card.html**

Add description and suitability:

```html
{% assign score = include.item.Score %}
{% assign source = include.item.SourceRef.Value %}
{% if source contains "github:" %}
  {% assign url = source | remove_first: "github:" | split: "/" %}
  {% assign url_final = "https://github.com/" | append: url[0] | append: "/" | append: url[1] %}
{% elsif source contains "hn:" %}
  {% assign hn_id = source | remove_first: "hn:" %}
  {% assign url_final = "https://news.ycombinator.com/item?id=" | append: hn_id %}
{% else %}
  {% assign url_final = "#" %}
{% endif %}

<article class="podium-card">
  <p class="podium-rank">
    {% if include.rank == 1 %}🥇 TOP 1
    {% elsif include.rank == 2 %}🥈 TOP 2
    {% else %}🥉 TOP 3
    {% endif %}
  </p>

  <h2 class="podium-title">
    <a href="{{ url_final }}" target="_blank" rel="noopener noreferrer">{{ include.item.Highlight.Text }}</a>
  </h2>

  <p class="podium-description">{{ include.item.Description }}</p>

  <div class="podium-meta">
    <span class="category-tag {{ include.item.Category }}">{{ include.item.Category }}</span>
    {% for tag in include.item.TechStack.Tags limit: 3 %}
      <span class="tech-tag">{{ tag }}</span>
    {% endfor %}
  </div>

  {% if include.item.Suitability %}
  <p class="podium-suitability">🎯 {{ include.item.Suitability }}</p>
  {% endif %}

  <span class="podium-score">{{ score }}</span>
</article>
```

- [ ] **Step 3: Add CSS styles**

In `pages/assets/css/style.scss`, after the `.card-suitability` block (around line 399), replace the entire `.card-suitability` section:

```scss
// ---- Description ----
.card-description {
  margin-top: 6px;
  font-size: 13px;
  color: $muted;
  line-height: 1.6;
}

.podium-description {
  margin-top: 6px;
  font-size: 12px;
  color: $muted;
  line-height: 1.55;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

// ---- Suitability ----
.card-suitability {
  margin-top: 6px;
  font-size: 12px;
  color: $muted;
  line-height: 1.55;
  padding-left: 6px;
  border-left: 2px solid rgba($gold, 0.25);
}

.podium-suitability {
  margin-top: 6px;
  font-size: 11px;
  color: $muted;
  line-height: 1.5;
}
```

- [ ] **Step 4: Commit**

```bash
git add pages/_includes/item-card.html pages/_includes/podium-card.html pages/assets/css/style.scss
git commit -m "feat: show description and inline suitability on cards"
```

---

### Task 8: Run full test suite

- [ ] **Step 1: Build and run all tests**

```bash
dotnet build Synapse.slnx
dotnet test Synapse.slnx
```

Expected: Full solution builds, all tests pass.

- [ ] **Step 2: Verify no warnings**

```bash
dotnet build Synapse.slnx --warnaserror
```

Expected: Zero warnings.

---

### Summary of Commits

| # | Commit Message |
|---|---------------|
| 1 | `feat: add Description to AnalyzedItem and prompt fields to OpenAIOptions` |
| 2 | `feat: use configurable prompt templates and parse Description in analyzer` |
| 3 | `test: add OpenAIAnalyzerAdapter tests and update FakeAnalyzer` |
| 4 | `feat: enrich GitHub trending with API description and topics` |
| 5 | `feat: use HN text field as description for self-posts` |
| 6 | `feat: include description in WeCom digest messages` |
| 7 | `feat: show description and inline suitability on cards` |
| 8 | Final verification — run full test suite |
