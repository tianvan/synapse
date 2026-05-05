# Synapse — AI 策展技术资讯日报

[![Daily Digest](https://github.com/tianvan/synapse/actions/workflows/daily-digest.yml/badge.svg)](https://github.com/tianvan/synapse/actions/workflows/daily-digest.yml)
[![Ingest Sources](https://github.com/tianvan/synapse/actions/workflows/ingest.yml/badge.svg)](https://github.com/tianvan/synapse/actions/workflows/ingest.yml)

每天自动采集 GitHub Trending 和 Hacker News 热门内容，经由 AI 分析、评分、分类，生成一份结构化技术日报——并通过 GitHub Pages 展示。

📡 **日报浏览：** [tianvan.github.io/synapse](https://tianvan.github.io/synapse/)

## 工作原理

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  采集 (6h)   │────▶│ AI 分析 (8:00)│────▶│ GitHub Pages │
│  GitHub      │     │  OpenAI 评分  │     │  暖色纸质感  │
│  HackerNews  │     │  分类 + 摘要  │     │  排行榜展示  │
└──────────────┘     └──────┬───────┘     └──────────────┘
                            │
                            ▼
                     ┌──────────────┐
                     │  企业微信推送  │
                     └──────────────┘
```

### 管道三步走

1. **采集 (ingest)** — `GitHubTrendingAdapter` 和 `HackerNewsAdapter` 每小时从 API / HTML 页面抓取热门内容，存入 `data/raw/{date}/{source}.json`
2. **生成日报 (digest)** — `OpenAIAnalyzerAdapter` 对当日原始数据评分（1-10）、分类（tool / article / framework / library）、提取技术栈标签、撰写摘要和适用场景说明，输出 `data/digests/{date}.json`
3. **分发** — 日报通过企业微信 Webhook 推送，同时 Jekyll 站点自动部署到 GitHub Pages

## 使用方法

### 本地运行

**前置条件：** .NET 10 SDK

```bash
# 采集内容（默认当天）
dotnet run --project src/Synapse.Cli -- ingest --source all

# 指定日期
dotnet run --project src/Synapse.Cli -- ingest --source all --date 2026-05-05

# 生成日报（需要 OpenAI API）
dotnet run --project src/Synapse.Cli -- generate-digest

# 运行测试
dotnet test Synapse.slnx
```

### 配置

复制 `appsettings.json` 或通过环境变量配置：

| 配置项 | 环境变量 | 说明 |
|---|---|---|
| `OpenAI:ApiKey` | `OpenAI__ApiKey` | OpenAI API 密钥 |
| `OpenAI:BaseUrl` | `OpenAI__BaseUrl` | API 地址（兼容第三方） |
| `OpenAI:Model` | `OpenAI__Model` | 模型名称 |
| `WeCom:WebhookUrl` | `WeCom__WebhookUrl` | 企业微信机器人地址 |

GitHub Actions 通过 Settings → Secrets 注入以上变量。

### CI/CD

| 工作流 | 频率 | 说明 |
|---|---|---|
| `ingest.yml` | 每 6 小时 | 抓取 GitHub Trending + HackerNews，数据 commit 回仓库 |
| `daily-digest.yml` | 每天 8:00 UTC | 生成日报 → 企业微信推送 → 自动部署到 GitHub Pages |
| `deploy-pages.yml` | 手动 / 被调用 | 将 digest JSON 部署为静态站点 |
| `pr-checks.yml` | PR 触发 | 跑测试，通过后自动 squash merge |

手动部署 Pages：Actions → Deploy to GitHub Pages → Run workflow

## 架构

**菱形对称架构（Diamond Symmetric Architecture）** — 两个限界上下文，共享内核：

```
Synapse.Cli (North Remote Gateway)
    │
    ├─▶ Synapse.Ingestion     采集上下文
    │   ├─ Domain/            实体、值对象
    │   ├─ Local/AppService/  用例编排
    │   ├─ South/Port/        接口定义
    │   └─ South/Adapter/     实现（HTTP抓取、文件存储）
    │
    └─▶ Synapse.Digest        日报上下文
        ├─ Domain/            实体、值对象
        ├─ Local/AppService/  用例编排
        ├─ South/Port/        接口定义
        └─ South/Adapter/     实现（OpenAI分析、企业微信、Pages部署）

Synapse.Foundation (共享内核)
    └─ ExternalId, SourceItem, SourceType, Stereotype属性, Result<T>
```

### 命名约定

| 后缀 | 用途 |
|---|---|
| `[Port]` | 南向端口接口（`IAnalyzer`, `ISourceReader`） |
| `[Adapter]` | 南向适配器实现（`GitHubTrendingAdapter`, `OpenAIAnalyzerAdapter`） |
| `[DomainService]` | 领域服务（仅 Domain 层） |
| `[Aggregate]` | 聚合根（`Digest`, `SourceItem`） |

### 数据流

```
North Remote (CLI)
    │
    │  Command DTO (record)
    ▼
Local/AppService  ────────────────────────
    │                                     │
    │  Port interface                     │
    ▼                                     │
South/Adapter ──▶ 外部系统 (HTTP / FS)    │
                                            │
上下文间通信：文件系统契约                      │
data/raw/{date}/{source}.json  ← 采集写入，日报读取
```

## 技术栈

| 层 | 技术 |
|---|---|
| 运行时 | .NET 10, C# 13 |
| AI 分析 | OpenAI API（兼容协议） |
| CI/CD | GitHub Actions |
| 静态站点 | Jekyll + GitHub Pages |
| 测试 | xUnit + FluentAssertions |
| DI | `Microsoft.Extensions.DependencyInjection`（手动 ServiceCollection） |
| 配置 | `IOptions<T>` + 环境变量 |

## 项目结构

```
synapse/
├── src/
│   ├── Synapse.Cli/            CLI 入口 + DI 组装
│   ├── Synapse.Digest/         日报限界上下文
│   ├── Synapse.Ingestion/      采集限界上下文
│   └── Synapse.Foundation/     共享内核
├── tests/
│   ├── Synapse.Digest.Tests/
│   ├── Synapse.Foundation.Tests/
│   └── Synapse.Ingestion.Tests/
├── pages/                      Jekyll 站点源文件
│   ├── _config.yml
│   ├── _layouts/default.html
│   ├── _includes/item-card.html
│   ├── index.html
│   └── assets/css/style.scss
├── data/
│   ├── raw/{date}/             原始采集数据
│   └── digests/{date}.json     AI 分析日报
└── .github/workflows/          CI/CD 管道
    ├── ingest.yml
    ├── daily-digest.yml
    ├── deploy-pages.yml
    └── pr-checks.yml
```

## 许可证

MIT
