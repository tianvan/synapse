# AI知识库 项目愿景 v1.0

## 核心链路
每天自动抓取 GitHub Trending → 分析 Agent 逐个解读 → JSON 中间层 → 整理 Agent 输出 AI 简报 + 推送通信 app

---

## 要做什么

### 1. 抓取
- 每天自动抓取 GitHub Trending Daily（全语言），约 25 条
- 按 repo topic 标签筛选 AI 相关仓库
- 抓取粒度：repo 基本信息（名称、描述、语言、star 数、topic 列表）

### 2. 分析 Agent
对每个 repo 读取 README（截断至 4000 token），输出三项：
- **一句话描述**（≤30 字）：这个仓库做什么
- **核心技术思路**（2-3 句话）：用了什么方法、什么架构
- **创新点标签**（1-3 个关键词，自由输出，下游 Agent 负责语义去重）

Token 预算：每个 repo 单次调用，输出 ≤500 token。日均成本估算 $2-3。

### 3. 中间 JSON（给整理 Agent 消费）
每条知识条目一个 JSON 对象，字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `repo_id` | string | `owner/repo`，主键，去重用 |
| `url` | string | GitHub 仓库地址 |
| `first_seen` | date | 首次出现在 trending 的日期 |
| `last_seen` | date | 最近一次出现在 trending 的日期 |
| `trending_count` | int | 累计出现在 trending 的次数 |
| `one_liner` | string | 一句话描述（≤30 字） |
| `tech_approach` | string | 核心技术思路（2-3 句） |
| `innovation_tags` | string[] | 创新点标签，自由输出 |
| `language` | string | 主要编程语言 |
| `stars` | int | 抓取时的 star 数 |
| `last_analyzed_at` | datetime | 最近一次分析时间戳 |

重分析策略：repo 再次出现时，若 star 变化超过 **50% 或 500 星**（取较小者，以 `max(old_stars, 100)` 为分母），触发重新分析并覆盖旧字段。否则仅更新 `last_seen` 和 `trending_count`。

存储：本地 JSON 文件，按日期分片，外加一份索引文件。

### 4. 整理 Agent
- 消费当日 + 历史 JSON，输出 **AI 简报**（Markdown 格式，面向人阅读）
- 支持推送到通信 app（目标 app 待定）
- 负责创新点标签的语义去重与归类

---

## 不做什么
- 不克隆源码、不分析代码实现细节（只看 README）
- 不分析 Issues / PR / Commit history
- 不做竞品对比分析
- 不提供实时 trending 监控（daily 批处理就够了）
- 不做网页前端展示（输出以文件 + 推送为主）
- 不保证 100% AI 相关筛选准确率（topic 标签有漏有误，接受误判）

---

## 边界与验收

### 输入边界
- 数据源仅限 GitHub Trending Daily 页面
- 仅处理 topic 标签含 AI/ML/LLM 相关关键词的仓库
- README 超过 4000 token 的部分直接截断不读

### 输出边界
- 每日最多 25 条分析条目
- 简报长度控制在 2000 字以内
- 中间 JSON 保留最近 90 天，超期归档

### 验收标准
1. 抓取成功率达到 95%（允许 GitHub 反爬偶发失败）
2. 分析 Agent 的一句话描述可通过人工抽查：10 条中 ≥8 条准确
3. 梳理 Agent 输出的简报无事实错误（不胡说 repo 功能）
4. 同一 repo 多次出现时正确去重更新，不产生重复条目

---

## 怎么验证
1. **抓取**：每日运行后检查 JSON 条目数，连续 7 天记录成功率
2. **分析质量**：每日从 25 条中随机抽 3 条，人工对比 repo README 判断描述是否准确
3. **去重**：手动构造一个已存在 repo 再次出现的场景，检查是否正确更新而非新增
4. **简报质量**：人工阅读每日简报，检查是否有明显事实错误
5. **成本**：记录每日 API 调用次数和 token 消耗，确认日均成本在预算范围内
