# GitHub Pages 展示 Synapse 日报 — 设计规格

## 概述

将 Synapse 生成的 AI 策展日报（`data/digests/{date}.json`）通过 GitHub Pages 以静态网站形式展示，提供排行榜风格的浏览体验。

## 需求摘要

- **展示内容**：仅日报 Digest（AI 分析后的结构化内容）
- **更新方式**：每日自动部署 + 手动触发
- **布局风格**：按评分降序排列，高分项高亮，暖色纸质感主题
- **技术方案**：Jekyll 静态站点，部署到 `gh-pages` 分支

## 文件结构

```
synapse/
├── pages/                          ← 新增：Jekyll 站点源文件
│   ├── _config.yml                 ← Jekyll 配置
│   ├── index.html                  ← 首页（今日日报）
│   ├── _layouts/
│   │   └── default.html            ← 暖色纸质感布局
│   ├── _includes/
│   │   └── item-card.html          ← 单条资讯卡片组件
│   ├── assets/
│   │   └── css/
│   │       └── style.scss          ← 自定义样式
│   └── _data/
│       └── digest.json             ← CI 拷贝产物（不提交到 master）
├── .github/workflows/
│   ├── daily-digest.yml            ← 已有，末尾新增 deploy-pages job
│   └── deploy-pages.yml            ← 新增：部署到 gh-pages
└── data/digests/                   ← 已有
```

## 数据流

```
daily-digest.yml (定时 / 手动)
  ├─ 生成 digest → data/digests/{date}.json
  └─ 触发 deploy-pages.yml
       ├─ Checkout master
       ├─ 拷贝最新 digest JSON → pages/_data/digest.json
       ├─ 扫描 data/digests/ 生成 _data/history.json
       └─ peaceiris/actions-gh-pages 部署到 gh-pages 分支
            └─ GitHub Pages 检测变更 → 自动 Jekyll 构建 → 上线
```

## 数据模型

`digest.json` 字段到页面的映射：

| JSON 字段 | 页面展示 | 说明 |
|---|---|---|
| `Items[].Score` | 排序依据 + 星级 | 9-10 分高亮（金色） |
| `Items[].Highlight.Text` | 标题/摘要 | 卡片主要内容 |
| `Items[].SourceRef.Value` | 来源链接 | `github:owner/repo` → GitHub；`hn:id` → HN |
| `Items[].Category` | 分类标签 | tool / article / framework / library / other |
| `Items[].TechStack.Tags` | 技术标签 | 小标签展示 |
| `Items[].Suitability` | 适用场景 | 展开/折叠或 tooltip |

`history.json` 结构：

```json
[
  { "date": "2026-05-05" },
  { "date": "2026-05-04" }
]
```

用于渲染历史日期选择器。

## CI/CD

### deploy-pages.yml（新增）

```yaml
name: Deploy to GitHub Pages
on:
  workflow_dispatch:   # 手动触发
  workflow_call:        # 被 daily-digest.yml 调用

jobs:
  deploy:
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@v4
      - name: Prepare data
        run: |
          cp data/digests/$(date +%Y-%m-%d).json pages/_data/digest.json
          # 生成历史列表
          ls data/digests/ | sed 's/.json//' | sort -r | jq -R '[.,{date:.}]' > pages/_data/history.json
      - name: Deploy
        uses: peaceiris/actions-gh-pages@v4
        with:
          github_token: ${{ secrets.GITHUB_TOKEN }}
          publish_dir: ./pages
          publish_branch: gh-pages
```

### daily-digest.yml 修改

在现有 jobs 末尾新增：

```yaml
  deploy-pages:
    needs: generate-digest
    uses: ./.github/workflows/deploy-pages.yml
    permissions:
      contents: write
```

### GitHub Settings 配置

| 设置项 | 值 |
|---|---|
| Settings → Pages → Source | Deploy from a branch |
| Branch | `gh-pages` / `/ (root)` |
| Workflow permissions | Read and write permissions |

## 视觉设计

- **主题**：暖色纸质感（暖白底色 #fffbeb，琥珀色点缀 #b45309）
- **布局**：单栏，按评分降序排列
- **Top 10**：金色高亮，带星级标记
- **卡片**：包含来源链接、分类标签、技术栈标签、摘要、评分
- **响应式**：移动端友好

> 注意：具体视觉设计在实现阶段由 frontend-design skill 负责打磨。

## 非功能需求

- **性能**：纯静态页面，无需 JS 框架，Lighthouse 评分 > 95
- **SEO**：Jekyll 生成静态 HTML，搜索引擎友好
- **安全**：无用户输入、无后端，零攻击面
- **可维护性**：Jekyll 模板修改无需重建 CI 管道

## 不做什么

- 不展示原始采集数据（`data/raw/`）
- 不做搜索功能（静态站点限制）
- 不做评论系统
- 不做 RSS 订阅
- 不做暗色模式切换（用户选了暖色主题）
