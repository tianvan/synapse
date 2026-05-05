# Synapse 知识库管道 — 设计规格

## 概述

从 GitHub Trending 和 Hacker News 抓取热点项目及讨论，通过 采集 → 分析 → 组织 管道处理后，以结构化日报形式推送到企业微信群聊。由 GitHub Actions 调度，零部署成本运行。

## 场景与约束

- **受众**：团队内部技术资讯分享
- **分析深度**：结构化处理（技术栈、亮点、适用场景、分类整理）
- **推送渠道**：多渠道灵活输出，初期接入企业微信群机器人 Webhook
- **数据源**：GitHub Trending + Hacker News，架构预留扩展接口
- **节奏**：采集每 4-6 小时多次触发，日报生成每日 1 次
- **调度**：GitHub Actions schedule cron，无需常驻进程
- **持久化**：文件系统，按日期分目录存储 JSON（raw）和 Markdown（digest）

## 技术栈

| 层面 | 选型 |
|---|---|
| 语言 | C# / .NET 10 |
| 调度 | GitHub Actions `schedule` + `workflow_dispatch` |
| HTTP | `HttpClientFactory` |
| AI 分析 | OpenAI Chat Completions API |
| 模板 | Scriban |
| DI/配置 | `Microsoft.Extensions.DependencyInjection` / `Microsoft.Extensions.Configuration` |
| 持久化 | 文件系统（JSON + Markdown） |

## 架构风格

**菱形对称架构 (Rhombic Symmetric Architecture)**，合并 **限界上下文分离**。

每个限界上下文内：
- **Local（应用程序层）**：`AppService/` 包含用例编排，`Message/` 为 Published Language 消息契约
- **Domain**：核心实体、值对象、领域异常
- **South Gateway（ACL 模式，Anti-Corruption Layer）**：`South/Port/` 定义抽象端口，`South/Adapter/` 实现端口

North Remote 的职责由 `Synapse.Cli` 独立承担，调用各上下文的 `Local/AppService`。

### 限界上下文

```
Ingestion Context (采集)                Digest Context (日报)
  每 4-6h 触发                           每日 1 次触发
  产出: data/raw/{date}/{source}.json     产出: data/digests/{date}.md + 推送

         raw JSON files = 上下文间契约
```

采集上下文可独立高频运行，日报上下文每日一次汇总分析和推送。两个上下文通过文件系统契约集成，不直接内存引用。

## 项目结构

```
src/
├── Synapse.Foundation/
│   ├── Stereotype/
│   │   ├── PortAttribute.cs
│   │   ├── AdapterAttribute.cs
│   │   ├── DomainServiceAttribute.cs
│   │   └── AggregateAttribute.cs
│   ├── Exception/
│   │   ├── ApplicationException.cs
│   │   └── DomainException.cs
│   └── Abstractions/
│       ├── Result.cs
│       └── IFileStorage.cs

├── Synapse.Ingestion/
│   ├── Domain/
│   │   ├── SourceItem.cs
│   │   ├── SourceType.cs
│   │   ├── ExternalId.cs
│   │   └── Exception/
│   │       └── SourceFetchException.cs
│   ├── Local/
│   │   ├── AppService/
│   │   │   └── IngestAppService.cs
│   │   └── Message/
│   │       ├── IngestCommand.cs
│   │       └── IngestResult.cs
│   └── South/
│       ├── Port/
│       │   ├── SourceReader/
│       │   │   └── ISourceReader.cs
│       │   └── Repository/
│       │       └── ISourceItemRepository.cs
│       └── Adapter/
│           ├── Sources/
│           │   ├── GitHubTrendingAdapter.cs
│           │   └── HackerNewsAdapter.cs
│           └── Repositories/
│               └── SourceItemFileAdapter.cs

├── Synapse.Digest/
│   ├── Domain/
│   │   ├── Digest.cs
│   │   ├── AnalyzedItem.cs
│   │   ├── DigestStatus.cs
│   │   ├── Highlight.cs
│   │   ├── TechStack.cs
│   │   └── Exception/
│   │       └── DigestGenerationException.cs
│   ├── Local/
│   │   ├── AppService/
│   │   │   └── GenerateDigestAppService.cs
│   │   └── Message/
│   │       ├── GenerateDigestCommand.cs
│   │       └── GenerateDigestResult.cs
│   └── South/
│       ├── Port/
│       │   ├── Analyzer/
│       │   │   └── IAnalyzer.cs
│       │   ├── Output/
│       │   │   └── IOutputPort.cs
│       │   └── Repository/
│       │       └── IDigestRepository.cs
│       └── Adapter/
│           ├── Analyzers/
│           │   └── OpenAIAnalyzerAdapter.cs
│           ├── Outputs/
│           │   └── WeComAdapter.cs
│           └── Repositories/
│               └── DigestFileAdapter.cs

└── Synapse.Cli/
    ├── Program.cs
    └── appsettings.json
```

### 架构闭合关系

```
                    Synapse.Cli  (North Remote Gateway = OHS)
                         │
          ┌──────────────┼──────────────┐
          ▼              ▼              ▼
    ┌──────────┐  ┌──────────┐  ┌──────────┐
    │  Local   │  │  Local   │  │  (future │
    │AppService│  │AppService│  │ contexts)│
    ├──────────┤  ├──────────┤  │          │
    │  Domain  │  │  Domain  │  │          │
    ├──────────┤  ├──────────┤  │          │
    │  South   │  │  South   │  │          │
    │ Port+Adp │  │ Port+Adp │  │          │
    └──────────┘  └──────────┘  └──────────┘
    Synapse.       Synapse.
    Ingestion      Digest
```

### 项目依赖关系

```
Synapse.Cli → Synapse.Ingestion, Synapse.Digest, Synapse.Foundation
Synapse.Ingestion → Synapse.Foundation
Synapse.Digest → Synapse.Foundation
```

Domain 层不依赖任何外部项目。

## 核心数据模型

### Ingestion Context

```csharp
// Domain/ExternalId.cs
public class ExternalId : ValueObject
{
    public string Value { get; }
    // 格式: "{source}:{identifier}"
}

// Domain/SourceItem.cs
public class SourceItem
{
    public ExternalId ExternalId { get; }            // 去重键，跨来源唯一
    public SourceType Type { get; }                  // GitHubTrending / HackerNews
    public string Title { get; }
    public Uri Url { get; }
    public string Description { get; }
    public Dictionary<string, string> Metadata { get; }  // stars, language, points...
    public DateTimeOffset FetchedAt { get; }
}

// Domain/SourceType.cs
public enum SourceType { GitHubTrending, HackerNews }
```

### ExternalId 生成规则

| SourceType | 规则 | 示例 |
|---|---|---|
| GitHubTrending | `github:{owner}/{repo}` | `github:rust-lang/rust` |
| HackerNews | `hn:{storyId}` | `hn:37854123` |

前缀由 `SourceType` 决定，保证不同来源的 ID 不会碰撞。生成逻辑在各自 South Adapter 内部，Domain 层当作不透明标识符使用。

### Digest Context

```csharp
// Domain/Digest.cs [Aggregate]
public class Digest
{
    public DateOnly Id { get; }                 // 自然键：日期
    public DateTimeOffset GeneratedAt { get; }
    public List<AnalyzedItem> Items { get; }    // 按 Score 降序
    public string Summary { get; }              // 当日概览，AI 生成
    public DigestStatus Status { get; }         // Pending / Published / Failed
}

// Domain/AnalyzedItem.cs
public class AnalyzedItem
{
    public ExternalId SourceRef { get; }        // 关联原始 SourceItem
    public string Category { get; }             // framework / tool / library / article
    public TechStack TechStack { get; }         // AI 动态提取，不做枚举预定义
    public Highlight Highlight { get; }         // 一句话：为什么值得关注
    public string Suitability { get; }          // 适用场景
    public int Score { get; }                   // 综合关注度 1-10
}

// Domain/Highlight.cs
public class Highlight : ValueObject
{
    public string Text { get; }
    // 封装领域概念 "亮点摘要"，即使只有一个字段也独立建模
    // 未来可扩展 KeyPoints: string[]
}

// Domain/TechStack.cs
public class TechStack : ValueObject
{
    public IReadOnlyList<string> Tags { get; }
    // AI 从描述中自由提取技术标签，不做预定义枚举约束
    // 示例: ["rust", "k8s", "wasm"]
    // 归一化（如 "kubernetes" → "k8s"）可在 Adapter 层处理
}

// Domain/DigestStatus.cs
public enum DigestStatus { Pending, Published, Failed }
```

## 领域端口契约

### Ingestion Context — South Ports

```csharp
// South/Port/SourceReader/ISourceReader.cs
[Port]
public interface ISourceReader
{
    SourceType Type { get; }
    Task<IReadOnlyList<SourceItem>> FetchAsync(CancellationToken ct);
}

// South/Port/Repository/ISourceItemRepository.cs
[Port]
public interface ISourceItemRepository
{
    Task SaveAsync(DateOnly date, IEnumerable<SourceItem> items, CancellationToken ct);
    Task<IReadOnlyList<SourceItem>> LoadAsync(DateOnly date, CancellationToken ct);
}
```

### Digest Context — South Ports

```csharp
// South/Port/Analyzer/IAnalyzer.cs
[Port]
public interface IAnalyzer
{
    Task<AnalyzedItem> AnalyzeAsync(SourceItem source, CancellationToken ct);
}

// South/Port/Output/IOutputPort.cs
[Port]
public interface IOutputPort
{
    OutputChannel Channel { get; }
    Task<bool> DeliverAsync(Digest digest, CancellationToken ct);
}

// South/Port/Repository/IDigestRepository.cs
[Port]
public interface IDigestRepository
{
    Task SaveAsync(Digest digest, CancellationToken ct);
    Task<Digest?> GetAsync(DateOnly date, CancellationToken ct);
}
```

## 管道流程

### IngestUseCase

```
遍历 ISourceReader[]
  → 每个 Source 抓取
  → 按 ExternalId 去重
  → ISourceItemRepository.Save(rawData)
  → 失败不阻塞，记录 Warning，继续下一个 Source
```

### GenerateDigestUseCase

```
ISourceItemRepository.Load(date) → 读取当天所有 raw
  → 遍历 SourceItem 调用 IAnalyzer.AnalyzeAsync()
    单项独立，可并行执行
    AI 调用失败 → 降级为未分析条目 (Score=0, 保留标题+链接)
  → 按 Category 归组，Score 降序排列
  → AI 生成当日 Summary
  → 组装 Digest 聚合根
  → IDigestRepository.Save(digest)
  → 遍历 IOutputPort[]，逐个推送
    单渠道失败不阻塞其他
```

### 异常策略

| 场景 | 处理 |
|---|---|
| 单个 Source 抓取失败 | Warning，继续下一个 |
| AI 分析单条失败 | 降级条目，Score=0，保留标题和链接 |
| 单个渠道推送失败 | Error，继续下一个渠道 |
| 全部 Source 失败 | 管道 Failed，不生成 Digest，返回错误 |
| API Key 缺失 (LLM/WeCom) | 启动即 Fail Fast，不浪费 Source 调用 |

## 调度 Workflow

```yaml
# .github/workflows/ingest.yml — 每 6 小时
on:
  schedule: [{cron: "0 */6 * * *"}]
  workflow_dispatch:
jobs:
  ingest:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - run: dotnet run --project src/Synapse.Cli -- ingest --source all

# .github/workflows/daily-digest.yml — 每日 8:00 UTC
on:
  schedule: [{cron: "0 8 * * *"}]
  workflow_dispatch:
jobs:
  digest:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - run: dotnet run --project src/Synapse.Cli -- generate-digest
      - run: |
          git config user.name "github-actions[bot]"
          git add data/
          git diff --staged --quiet || git commit -m "chore: archive $(date +%F)"
          git push
```

## 数据文件契约

```
data/
├── raw/
│   └── {yyyy-MM-dd}/
│       ├── github-trending.json        # SourceItem[]
│       └── hacker-news.json            # SourceItem[]
└── digests/
    └── {yyyy-MM-dd}.md                 # Scriban 渲染的 Markdown 日报
```

## 待定

- OpenAI 模型选型（gpt-4o-mini 用于成本优先）
- 企业微信 Webhook URL 配置方式（GitHub Secret）
- 日报模板具体格式（后续迭代调整）
