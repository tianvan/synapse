# Synapse — AI 知识库

每日管线：抓取 GitHub Trending → Claude API 分析 README → 结构化 JSON 存储 → 生成 AI 简报 → 推送到通讯 app。完整愿景：@specs/project-vision.md

## 架构不变量

以下决策已定稿，未经更新 spec 不得修改。

- **数据源**：GitHub Trending Daily（全语言），每天约 25 条。按 repo topic 标签筛选，关键词覆盖 AI/ML/LLM 相关领域。
- **分析 Agent**：Claude API，每个 repo 单次调用。README 截断至 4000 token 输入。输出三项：一句话描述（≤30 字）、核心技术思路（2-3 句）、创新点标签（1-3 个自由关键词）。输出 ≤500 token。
- **中间 JSON schema**：10 个字段，定义见 `specs/project-vision.md` §3。`repo_id`（`owner/repo`）为主键去重。按日期分片存储：`data/YYYY-MM-DD.json`，外加 `data/index.json` 索引文件。
- **重分析触发条件**：star 变化超过 `max(500, old_stars × 0.5)`，分母取 `max(old_stars, 100)`。触发时覆盖旧分析字段；未触发时仅更新 `last_seen` 和 `trending_count`。
- **整理 Agent**：消费累积的 JSON，输出 Markdown 简报 ≤2000 字。负责创新点标签的语义去重与归类。推送至通讯 app（具体目标待定）。
- **数据保留**：中间 JSON 保留 90 天，超期归档。
- **成本预算**：日均 $2-3，对应 25 个 repo × 单次 API 调用。

## 技术约定

- Python 3.11+。所有公开函数必须加类型标注。
- Pydantic v2 模型作为 JSON schema 的单一数据源。
- `httpx` 做 HTTP、`structlog` 做日志、`pathlib.Path` 做路径操作。
- API key 通过环境变量注入，严禁硬编码。必需：`ANTHROPIC_API_KEY`。
- 测试框架：`pytest`。抓取模块的测试必须 mock 网络请求，覆盖 95% 成功率验收标准。

## 工作流

- 分支命名：`feat/`、`fix/`、`chore/`、`docs/`
- 遵循 Conventional Commits 规范
- 提交前：运行测试套件，并验证 JSON schema 与 spec 一致
- 新增数据源、修改 JSON schema、或调整重分析阈值时，同步更新本文件
