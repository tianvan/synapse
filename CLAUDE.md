# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

```bash
dotnet build Synapse.slnx
dotnet test Synapse.slnx                        # all tests
dotnet test --filter "FullyQualifiedName~ClassName"  # single test class
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

**Shared kernel:** `Synapse.Foundation` — `ExternalId`, `SourceItem`, `SourceType`, stereotype attributes, `Result<T>`, base exceptions.

Contexts communicate via file system contract: `data/raw/{yyyy-MM-dd}/{source}.json`.

## Configuration

`IOptions<T>` pattern. Defaults in `appsettings.json`, overridable by environment variables.

Env vars use `__` separator for hierarchy: `OpenAI__ApiKey`, `OpenAI__BaseUrl`, `WeCom__WebhookUrl`. Set via GitHub Secrets with matching names.

## CI/CD

Three GitHub Actions workflows:
- `ingest.yml` — fetches sources every 6h, archives data via git push
- `daily-digest.yml` — generates and delivers digest daily at 8:00 UTC
- `pr-checks.yml` — runs tests on PR, enables auto-merge (squash) on pass

Workflows that push data need `permissions: contents: write`.

## Key Conventions

- **No hardcoded defaults in C#** — use `appsettings.json` + `IOptions<T>`.
- **`sealed record` for all value objects and entities** — records give value equality, immutability, `with`.
- **`[DomainService]` only on Domain-layer services**, never on `Local/AppService` classes.
- **`slnx` format** (not `.sln`) for .NET 10 solution.
- **`ILogger<T>`** for logging, not `Console.WriteLine`. Log level in `appsettings.json`.
