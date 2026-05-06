# Content Enrichment & Prompt Optimization Design

**Date**: 2026-05-06
**Status**: Approved

## Problem Statement

1. **采集信息不够丰富** — GitHub trending 只采集基本字段，HN 描述直接复制标题，缺乏实质性描述
2. **分析结果只有标题** — `AnalyzedItem` 只有一个 `Highlight`（一句话），缺少详细描述
3. **适用场景需点击展开** — 藏在 `<details>` 折叠框里，不直观
4. **AI 味太重** — prompt 硬编码、英文 system prompt、没有风格约束

## Constraints

- 采集阶段不使用 AI，保持源头信息纯粹性
- AI 生成的描述 100-150 字（中等篇幅）
- Prompt 采用 IOptions 模式，System + User 双模板可配置

---

## Design

### 1. Domain Model

**SourceItem** (no changes — existing fields used better):
- `Description` — GitHub: API 返回的 repo description; HN self-post: `text` 字段; HN 普通链接: title
- `Metadata` — GitHub: 新增 `topics`

**AnalyzedItem** (new `Description` field):
```csharp
public sealed record AnalyzedItem(
    ExternalId SourceRef,
    string Category,
    TechStack TechStack,
    Highlight Highlight,
    string Description,    // NEW: AI-generated 100-150 char Chinese description
    string Suitability,
    int Score
);
```

**OpenAIOptions** (new prompt fields):
```csharp
public class OpenAIOptions
{
    public const string Section = "OpenAI";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.deepseek.com/";
    public string Model { get; set; } = "deepseek-v4-pro";
    public string SystemPrompt { get; set; } = "...";       // NEW
    public string UserPromptTemplate { get; set; } = "...";  // NEW
}
```

### 2. Ingestion Enrichment

**GitHubTrendingAdapter** — add GitHub REST API call after HTML scrape:
- `GET /repos/{owner}/{name}` → extract `description` (repo full description), `topics` (array → comma-separated in Metadata)
- Unauthenticated, 60 req/h, ~25 repos/day — safe
- API failure → fallback to HTML-scraped description

**HackerNewsAdapter** — use `text` field when available:
- Item type is story AND `text` field exists (self-post) → `Description = item.Text` (truncated to 500 chars)
- Regular article link → `Description = item.Title` (unchanged)

### 3. Prompt Templates

**Placeholders** (`{{Name}}` syntax, replaced via `string.Replace`):

| Placeholder | Source |
|-------------|--------|
| `{{Title}}` | `source.Title` |
| `{{Description}}` | `source.Description` |
| `{{Language}}` | `source.Metadata["language"]` |
| `{{Stars}}` | `source.Metadata["stars"]` (prefixed with "Stars: ") |
| `{{Score}}` | `source.Metadata["score"]` (prefixed with "HN Score: ") |

**Default SystemPrompt**:
```
你是一位资深技术编辑，擅长用精炼且有人情味的语言点评技术项目。
你的分析准确、务实，不说套话，不堆砌术语。
输出合法 JSON，不要 markdown。
```

**Default UserPromptTemplate**:
```
分析以下技术项目，输出 JSON：
{
  "category": "framework|tool|library|article|other",
  "techStack": ["具体技术名"],
  "highlight": "一句话中文亮点，为什么值得关注（20-40字）",
  "description": "项目做什么、核心价值在哪（100-150字），口语化中文，像在给同事介绍",
  "suitability": "适合什么场景使用",
  "score": 1-10
}

要求：
- 禁用"赋能""抓手""拉通""值得关注的是""此外""综上所述"等套话
- techStack 要具体，避免只说"AI"，应具体到"强化学习""LLM""RAG"等
- description 用长短句交替，可以插入口语化过渡词（"其实""你发现没""说来有趣"）
- 评分严格：7 分以上必须有明显技术突破或独特设计思路
- 项目名、技术名词保持原文，不要翻译

项目: {{Title}}
描述: {{Description}}
{{Stars}}{{Score}}
语言: {{Language}}
```

### 4. Analyzer Changes (OpenAIAnalyzerAdapter)

- `BuildPrompt()` — replace hardcoded `$$"""` with `_options.UserPromptTemplate` + `string.Replace`
- System message — use `_options.SystemPrompt`
- `ParseResponse()` — extract `description` field from AI response JSON
- `Degrade()` — `Description` = `source.Description` truncated to 150 chars

### 5. Display Layer

**item-card.html**:
- Remove `<details class="card-suitability">` wrapper
- Add `<p class="card-description">` before meta (shows `Description`)
- Change suitability to `<p class="card-suitability">` (always visible)

**podium-card.html**:
- Add description and suitability paragraphs (currently absent)

**WeComAdapter**:
- Add `{item.Description}` line after highlight in message template

**style.scss**:
- Add `.card-description` and `.podium-description` styles (13px, $muted, 1.6 line-height)

### 6. Data Flow

```
Ingestion                            Analysis                      Display
─────────                            ────────                      ───────
GitHub API ──→ SourceItem ──→ OpenAIAnalyzer ──→ AnalyzedItem ──→ Pages/WeCom
                 .Description          .Description (AI生成)       直接展示
                 .Metadata             .Highlight                  无折叠
HN API ──────→                        .Suitability
```

### 7. Files Changed

| Layer | File | Change |
|-------|------|--------|
| Domain | `src/Synapse.Digest/Domain/AnalyzedItem.cs` | Add `Description` field |
| Domain | `src/Synapse.Digest/South/Adapter/Analyzers/OpenAIOptions.cs` | Add `SystemPrompt`, `UserPromptTemplate` |
| Adapter | `src/Synapse.Digest/South/Adapter/Analyzers/OpenAIAnalyzerAdapter.cs` | Template substitution, parse new field |
| Adapter | `src/Synapse.Ingestion/South/Adapter/Sources/GitHubTrendingAdapter.cs` | Call GitHub API for description/topics |
| Adapter | `src/Synapse.Ingestion/South/Adapter/Sources/HackerNewsAdapter.cs` | Use `text` field for self-posts |
| Adapter | `src/Synapse.Digest/South/Adapter/Outputs/WeComAdapter.cs` | Include description in output |
| UI | `pages/_includes/item-card.html` | Remove details, add description, inline suitability |
| UI | `pages/_includes/podium-card.html` | Add description and suitability |
| UI | `pages/assets/css/style.scss` | New card description styles |

### 8. Testing

- **GitHubTrendingAdapterTests**: API response maps correctly to Description + Metadata["topics"]; fallback on API failure
- **HackerNewsAdapterTests**: Self-post `text` → Description; regular link → title as Description
- **OpenAIAnalyzerAdapterTests**: Template placeholder substitution; `description` parsed from response; degrade uses source.Description
- **GenerateDigestAppServiceTests**: AnalyzedItem.Description flows to Digest output
