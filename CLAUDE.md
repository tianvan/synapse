# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

```bash
dotnet build Synapse.slnx
dotnet test Synapse.slnx                                         # all tests
dotnet test --filter "FullyQualifiedName~IngestAppServiceTests"  # single test class
```

Run CLI commands (from repo root):
```bash
dotnet run --project src/Synapse.Cli -- ingest --source all [--date yyyy-MM-dd]
dotnet run --project src/Synapse.Cli -- generate-digest [--date yyyy-MM-dd]
```

## Architecture

**Diamond Symmetric Architecture (菱形对称架构)** with two bounded contexts driven by GitHub Actions.

Each bounded context follows this internal structure:
- `Domain/` — entities, value objects, domain exceptions. All types are `sealed record`.
- `Local/AppService/` — use case orchestration. No stereotype annotation.
- `Local/Message/` — command/result DTOs (records).
- `South/Port/` — interfaces (`[Port]`) defining what the domain needs from outside.
- `South/Adapter/` — implementations (`[Adapter]`) of ports (HTTP, file system, external APIs).

`Synapse.Cli` is the **North Remote Gateway** — it calls `Local/AppService` in each context.

```
Synapse.Cli (North Remote)
  ├─→ Synapse.Ingestion (采集: GitHub Trending + Hacker News)
  └─→ Synapse.Digest   (日报: AI analyze → organize → deliver)
```

**Shared kernel:** `Synapse.Foundation` — `ExternalId`, `SourceItem`, `SourceType` (enum: `GitHubTrending`, `HackerNews`), stereotype attributes (`[Port]`, `[Adapter]`, `[DomainService]`, `[Aggregate]`), `Result<T>`, base exceptions.

Contexts communicate via file system contract: `data/raw/{yyyy-MM-dd}/{source}.json`.

DI is manual `ServiceCollection` in `Program.cs` — no `Host.CreateDefaultBuilder`. `HttpClient` registered via `services.AddHttpClient()` and injected into adapters.

## Configuration

`IOptions<T>` pattern. Sensible defaults in options class property initializers (e.g. `OpenAIOptions`), overridable via `appsettings.json` or environment variables. Never hardcode operational parameters in constructors or method bodies.

Env vars use `__` separator for hierarchy: `OpenAI__ApiKey`, `OpenAI__BaseUrl`, `WeCom__WebhookUrl`. Set via GitHub Secrets with matching names.

GitHub Secrets required for `daily-digest.yml`: `OpenAI__ApiKey`, `OpenAI__BaseUrl`, `OpenAI__Model`, `WeCom__WebhookUrl`.

## CI/CD

Three GitHub Actions workflows:
- `ingest.yml` — fetches sources every 6h, archives data via git push
- `daily-digest.yml` — generates and delivers digest daily at 8:00 UTC
- `pr-checks.yml` — runs tests on PR, enables auto-merge (squash) on pass

Workflows that push data need `permissions: contents: write`. `pr-checks.yml` auto-merge uses `gh pr merge --auto --squash` and needs `GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}`.

## GitHub Pages

Jekyll site source in `pages/`. Digest data is deployed to `gh-pages` branch via `deploy-pages.yml`:
- Auto-triggered after `daily-digest.yml` completes
- Manually triggerable via workflow_dispatch
- `pages/_data/` is CI-generated (gitignored in master)

Site URL: `https://tianvan.github.io/synapse` (after first deploy, configure in repo Settings → Pages)

## Key Conventions

- **`sealed record` for all value objects and entities** — records give value equality, immutability, `with`.
- **`[DomainService]` only on Domain-layer services**, never on `Local/AppService` classes.
- **`DateOnly` for date values**, not `DateTime`.
- **`slnx` format** (not `.sln`) for .NET 10 solution.
- **File-scoped namespaces** throughout.
- **`ILogger<T>`** for logging, not `Console.WriteLine`. Log level in `appsettings.json`.

### Test Conventions

- **xUnit** with **FluentAssertions** (`.Should()` extension methods).
- **No mocking library** — test doubles are inline classes at the bottom of the test file (e.g. `FakeSourceReader`, `FailingSourceReader`).
- Test method naming: `snake_case` with `Should_` prefix (e.g. `Should_fetch_from_all_readers_and_save`).
- Tests that create temp files use `Path.GetTempPath()` + cleanup in `try/catch`.
