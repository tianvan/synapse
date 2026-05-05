# Synapse 知识库管道 — 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建从 GitHub Trending / Hacker News 抓取热点项目，经 AI 分析后生成结构化日报并推送到企业微信的管道系统。

**Architecture:** 菱形对称架构 + 两个限界上下文（Ingestion、Digest），GitHub Actions 调度，文件系统持久化，CLI 作为 North Remote Gateway。

**Tech Stack:** C# / .NET 10、xUnit + FluentAssertions、HttpClientFactory、Scriban、OpenAI API、企业微信 Webhook

---

## File Structure Map

```
src/
├── Synapse.Foundation/           # 共享内核
│   ├── Foundation.csproj
│   ├── Stereotype/               # PortAttribute, AdapterAttribute, ...
│   ├── Exception/                # ApplicationException, DomainException
│   ├── Shared/                   # ExternalId, SourceItem, SourceType
│   └── Abstractions/             # Result<T>, IFileStorage
│
├── Synapse.Ingestion/            # 采集限界上下文
│   ├── Ingestion.csproj          # → Synapse.Foundation
│   ├── Domain/                   # (empty — shared types in Foundation)
│   ├── Local/
│   │   ├── AppService/           # IngestAppService
│   │   └── Message/              # IngestCommand, IngestResult
│   └── South/
│       ├── Port/                 # ISourceReader, ISourceItemRepository
│       └── Adapter/              # GitHubTrendingAdapter, HackerNewsAdapter, SourceItemFileAdapter
│
├── Synapse.Digest/               # 日报限界上下文
│   ├── Digest.csproj             # → Synapse.Foundation
│   ├── Domain/                   # Highlight, TechStack, AnalyzedItem, Digest, DigestStatus
│   ├── Local/
│   │   ├── AppService/           # GenerateDigestAppService
│   │   └── Message/              # GenerateDigestCommand, GenerateDigestResult
│   └── South/
│       ├── Port/                 # IAnalyzer, IOutputPort, IDigestRepository
│       └── Adapter/              # OpenAIAnalyzerAdapter, WeComAdapter, DigestFileAdapter
│
├── Synapse.Cli/                  # North Remote Gateway
│   ├── Cli.csproj                # → Synapse.Ingestion, Synapse.Digest, Synapse.Foundation
│   ├── Program.cs
│   └── appsettings.json
│
└── tests/
    ├── Synapse.Foundation.Tests/
    ├── Synapse.Ingestion.Tests/
    └── Synapse.Digest.Tests/
```

**Shared Kernel 决策：** `ExternalId`、`SourceItem`、`SourceType` 放入 `Synapse.Foundation/Shared/`。两个上下文都需要它们——Ingestion 生产、Digest 消费。作为 Published Language 契约放共享内核。

**值对象约定：** 所有值对象和实体均使用 C# `record` 类型——不可变性由 `init` 和 `with` 表达式保证，值相等由编译器自动生成。

---

### Task 1: 创建解决方案结构和项目骨架

**Files:**
- Create: `Synapse.slnx`
- Create: `src/Synapse.Foundation/Foundation.csproj`
- Create: `src/Synapse.Ingestion/Ingestion.csproj`
- Create: `src/Synapse.Digest/Digest.csproj`
- Create: `src/Synapse.Cli/Cli.csproj`
- Create: `tests/Synapse.Foundation.Tests/Foundation.Tests.csproj`
- Create: `tests/Synapse.Ingestion.Tests/Ingestion.Tests.csproj`
- Create: `tests/Synapse.Digest.Tests/Digest.Tests.csproj`

- [ ] **Step 1: 创建 slnx 解决方案**

```bash
cd D:/Repos/synapse && dotnet new slnx -n Synapse
```

- [ ] **Step 2: 创建类库项目**

```bash
dotnet new classlib -n Synapse.Foundation -o src/Synapse.Foundation -f net10.0
dotnet new classlib -n Synapse.Ingestion -o src/Synapse.Ingestion -f net10.0
dotnet new classlib -n Synapse.Digest -o src/Synapse.Digest -f net10.0
```

- [ ] **Step 3: 创建测试项目**

```bash
dotnet new xunit -n Synapse.Foundation.Tests -o tests/Synapse.Foundation.Tests -f net10.0
dotnet new xunit -n Synapse.Ingestion.Tests -o tests/Synapse.Ingestion.Tests -f net10.0
dotnet new xunit -n Synapse.Digest.Tests -o tests/Synapse.Digest.Tests -f net10.0
```

- [ ] **Step 4: 创建 CLI 控制台项目**

```bash
dotnet new console -n Synapse.Cli -o src/Synapse.Cli -f net10.0
```

- [ ] **Step 5: 添加项目引用**

```bash
dotnet add src/Synapse.Ingestion/Ingestion.csproj reference src/Synapse.Foundation/Foundation.csproj
dotnet add src/Synapse.Digest/Digest.csproj reference src/Synapse.Foundation/Foundation.csproj
dotnet add src/Synapse.Cli/Cli.csproj reference src/Synapse.Ingestion/Ingestion.csproj
dotnet add src/Synapse.Cli/Cli.csproj reference src/Synapse.Digest/Digest.csproj
dotnet add src/Synapse.Cli/Cli.csproj reference src/Synapse.Foundation/Foundation.csproj
```

- [ ] **Step 6: 添加测试项目引用**

```bash
dotnet add tests/Synapse.Foundation.Tests/Foundation.Tests.csproj reference src/Synapse.Foundation/Foundation.csproj
dotnet add tests/Synapse.Ingestion.Tests/Ingestion.Tests.csproj reference src/Synapse.Ingestion/Ingestion.csproj
dotnet add tests/Synapse.Digest.Tests/Digest.Tests.csproj reference src/Synapse.Digest/Digest.csproj
```

- [ ] **Step 7: 将所有项目加入解决方案**

```bash
dotnet sln Synapse.slnx add src/Synapse.Foundation/Foundation.csproj
dotnet sln Synapse.slnx add src/Synapse.Ingestion/Ingestion.csproj
dotnet sln Synapse.slnx add src/Synapse.Digest/Digest.csproj
dotnet sln Synapse.slnx add src/Synapse.Cli/Cli.csproj
dotnet sln Synapse.slnx add tests/Synapse.Foundation.Tests/Foundation.Tests.csproj
dotnet sln Synapse.slnx add tests/Synapse.Ingestion.Tests/Ingestion.Tests.csproj
dotnet sln Synapse.slnx add tests/Synapse.Digest.Tests/Digest.Tests.csproj
```

- [ ] **Step 8: 为所有测试项目添加 FluentAssertions**

```bash
dotnet add tests/Synapse.Foundation.Tests/Foundation.Tests.csproj package FluentAssertions
dotnet add tests/Synapse.Ingestion.Tests/Ingestion.Tests.csproj package FluentAssertions
dotnet add tests/Synapse.Digest.Tests/Digest.Tests.csproj package FluentAssertions
```

- [ ] **Step 9: 为各项目添加 NuGet 包**

```bash
dotnet add src/Synapse.Cli/Cli.csproj package Microsoft.Extensions.DependencyInjection
dotnet add src/Synapse.Cli/Cli.csproj package Microsoft.Extensions.Configuration.Json
dotnet add src/Synapse.Cli/Cli.csproj package Microsoft.Extensions.Configuration.EnvironmentVariables
dotnet add src/Synapse.Ingestion/Ingestion.csproj package Microsoft.Extensions.Http
dotnet add src/Synapse.Ingestion/Ingestion.csproj package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add src/Synapse.Digest/Digest.csproj package Microsoft.Extensions.Http
dotnet add src/Synapse.Digest/Digest.csproj package Scriban
```

- [ ] **Step 10: 验证构建**

```bash
dotnet build Synapse.slnx
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 11: 提交**

```bash
git add Synapse.slnx src/ tests/
git commit -m "feat: scaffold solution with Foundation, Ingestion, Digest, Cli projects"
```

---

### Task 2: Foundation — Stereotype 标注和基础异常

**Files:**
- Create: `src/Synapse.Foundation/Stereotype/PortAttribute.cs`
- Create: `src/Synapse.Foundation/Stereotype/AdapterAttribute.cs`
- Create: `src/Synapse.Foundation/Stereotype/DomainServiceAttribute.cs`
- Create: `src/Synapse.Foundation/Stereotype/AggregateAttribute.cs`
- Create: `src/Synapse.Foundation/Exception/ApplicationException.cs`
- Create: `src/Synapse.Foundation/Exception/DomainException.cs`
- Test: `tests/Synapse.Foundation.Tests/Stereotype/StereotypeAttributeTests.cs`
- Test: `tests/Synapse.Foundation.Tests/Exception/ExceptionTests.cs`

- [ ] **Step 1: 写 Stereotype 测试**

```csharp
// tests/Synapse.Foundation.Tests/Stereotype/StereotypeAttributeTests.cs
using FluentAssertions;

namespace Synapse.Foundation.Tests.Stereotype;

public class StereotypeAttributeTests
{
    [Fact]
    public void Port_attribute_should_exist()
    {
        var attr = new PortAttribute();
        attr.Should().BeOfType<PortAttribute>();
    }

    [Fact]
    public void Adapter_attribute_should_inherit_from_attribute()
    {
        typeof(AdapterAttribute).Should().BeDerivedFrom<Attribute>();
    }

    [Fact]
    public void Port_attribute_should_target_interfaces()
    {
        var usage = Attribute.GetCustomAttribute(
            typeof(PortAttribute), typeof(AttributeUsageAttribute)) as AttributeUsageAttribute;
        usage.Should().NotBeNull();
        usage!.ValidOn.Should().Be(AttributeTargets.Interface);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

```bash
dotnet test tests/Synapse.Foundation.Tests/ --filter "StereotypeAttributeTests"
```

Expected: FAIL — types not found.

- [ ] **Step 3: 实现 Stereotype 标注**

```csharp
// src/Synapse.Foundation/Stereotype/PortAttribute.cs
namespace Synapse.Foundation.Stereotype;

[AttributeUsage(AttributeTargets.Interface)]
public class PortAttribute : Attribute;

// src/Synapse.Foundation/Stereotype/AdapterAttribute.cs
namespace Synapse.Foundation.Stereotype;

[AttributeUsage(AttributeTargets.Class)]
public class AdapterAttribute : Attribute;

// src/Synapse.Foundation/Stereotype/DomainServiceAttribute.cs
namespace Synapse.Foundation.Stereotype;

[AttributeUsage(AttributeTargets.Class)]
public class DomainServiceAttribute : Attribute;

// src/Synapse.Foundation/Stereotype/AggregateAttribute.cs
namespace Synapse.Foundation.Stereotype;

[AttributeUsage(AttributeTargets.Class)]
public class AggregateAttribute : Attribute;
```

- [ ] **Step 4: 写异常测试**

```csharp
// tests/Synapse.Foundation.Tests/Exception/ExceptionTests.cs
using FluentAssertions;

namespace Synapse.Foundation.Tests.Exception;

public class ExceptionTests
{
    [Fact]
    public void ApplicationException_should_store_message()
    {
        var ex = new ApplicationException("test error");
        ex.Message.Should().Be("test error");
    }

    [Fact]
    public void DomainException_should_store_message()
    {
        var ex = new DomainException("invalid state");
        ex.Message.Should().Be("invalid state");
    }
}
```

- [ ] **Step 5: 实现基础异常**

```csharp
// src/Synapse.Foundation/Exception/ApplicationException.cs
namespace Synapse.Foundation.Exception;

public class ApplicationException(string message) : System.Exception(message)
{
    public ApplicationException(string message, System.Exception inner)
        : this(message) { }
}

// src/Synapse.Foundation/Exception/DomainException.cs
namespace Synapse.Foundation.Exception;

public class DomainException(string message) : System.Exception(message);
```

- [ ] **Step 6: 运行测试验证通过**

```bash
dotnet test tests/Synapse.Foundation.Tests/
```

Expected: All tests PASS.

- [ ] **Step 7: 提交**

```bash
git add src/Synapse.Foundation/ tests/Synapse.Foundation.Tests/
git commit -m "feat: add foundation stereotypes and base exceptions"
```

---

### Task 3: Foundation — 共享领域类型 ExternalId、SourceType、SourceItem

**Files:**
- Create: `src/Synapse.Foundation/Shared/ExternalId.cs`
- Create: `src/Synapse.Foundation/Shared/SourceType.cs`
- Create: `src/Synapse.Foundation/Shared/SourceItem.cs`
- Test: `tests/Synapse.Foundation.Tests/Shared/ExternalIdTests.cs`
- Test: `tests/Synapse.Foundation.Tests/Shared/SourceItemTests.cs`

- [ ] **Step 1: 写 ExternalId 测试**

```csharp
// tests/Synapse.Foundation.Tests/Shared/ExternalIdTests.cs
using FluentAssertions;

namespace Synapse.Foundation.Tests.Shared;

public class ExternalIdTests
{
    [Fact]
    public void Should_store_value()
    {
        var id = new ExternalId("github:rust-lang/rust");
        id.Value.Should().Be("github:rust-lang/rust");
    }

    [Fact]
    public void Same_value_should_be_equal()
    {
        var a = new ExternalId("hn:12345");
        var b = new ExternalId("hn:12345");
        a.Should().Be(b);
    }

    [Fact]
    public void Different_value_should_not_be_equal()
    {
        var a = new ExternalId("hn:12345");
        var b = new ExternalId("hn:67890");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Should_reject_null_or_empty()
    {
        Action act = () => new ExternalId("");
        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

```bash
dotnet test tests/Synapse.Foundation.Tests/ --filter "ExternalIdTests"
```

Expected: FAIL.

- [ ] **Step 3: 实现 ExternalId（record 值对象）**

```csharp
// src/Synapse.Foundation/Shared/ExternalId.cs
namespace Synapse.Foundation.Shared;

public sealed record ExternalId
{
    public string Value { get; }

    public ExternalId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public override string ToString() => Value;
}
```

`record` 自动提供值相等——`Equals`、`GetHashCode`、`==` 均由编译器生成，比较 `Value` 属性。

- [ ] **Step 4: 运行测试验证通过**

```bash
dotnet test tests/Synapse.Foundation.Tests/ --filter "ExternalIdTests"
```

Expected: PASS.

- [ ] **Step 5: 写 SourceType 和 SourceItem 测试**

```csharp
// tests/Synapse.Foundation.Tests/Shared/SourceItemTests.cs
using FluentAssertions;

namespace Synapse.Foundation.Tests.Shared;

public class SourceItemTests
{
    [Fact]
    public void Should_create_source_item()
    {
        var item = new SourceItem(
            ExternalId: new ExternalId("github:test/repo"),
            Type: SourceType.GitHubTrending,
            Title: "Test Repo",
            Url: new Uri("https://github.com/test/repo"),
            Description: "A test repository",
            Metadata: new Dictionary<string, string> { ["stars"] = "100" },
            FetchedAt: DateTimeOffset.UtcNow
        );

        item.ExternalId.Value.Should().Be("github:test/repo");
        item.Type.Should().Be(SourceType.GitHubTrending);
        item.Metadata["stars"].Should().Be("100");
    }

    [Fact]
    public void SourceType_enum_values_exist()
    {
        Enum.GetValues<SourceType>().Should().Contain(SourceType.GitHubTrending);
        Enum.GetValues<SourceType>().Should().Contain(SourceType.HackerNews);
    }

    [Fact]
    public void SourceItem_with_expression_creates_modified_copy()
    {
        var original = new SourceItem(
            new ExternalId("github:a/b"), SourceType.GitHubTrending,
            "A", new Uri("https://a.com"), "desc", new(), DateTimeOffset.UtcNow);

        var modified = original with { Title = "B" };

        modified.Title.Should().Be("B");
        modified.ExternalId.Should().Be(original.ExternalId);
        original.Title.Should().Be("A"); // immutable
    }
}
```

- [ ] **Step 6: 实现 SourceType 和 SourceItem（record）**

```csharp
// src/Synapse.Foundation/Shared/SourceType.cs
namespace Synapse.Foundation.Shared;

public enum SourceType
{
    GitHubTrending,
    HackerNews
}
```

```csharp
// src/Synapse.Foundation/Shared/SourceItem.cs
namespace Synapse.Foundation.Shared;

public sealed record SourceItem(
    ExternalId ExternalId,
    SourceType Type,
    string Title,
    Uri Url,
    string Description,
    Dictionary<string, string> Metadata,
    DateTimeOffset FetchedAt
);
```

`record` 的主构造函数形式——所有属性自动 `{ get; init; }`，不可变，支持 `with` 表达式。

- [ ] **Step 7: 运行测试验证通过**

```bash
dotnet test tests/Synapse.Foundation.Tests/
```

Expected: All PASS.

- [ ] **Step 8: 提交**

```bash
git add src/Synapse.Foundation/Shared/ tests/Synapse.Foundation.Tests/Shared/
git commit -m "feat: add shared kernel types ExternalId, SourceType, SourceItem as records"
```

---

### Task 4: Foundation — Result<T> 和 IFileStorage

**Files:**
- Create: `src/Synapse.Foundation/Abstractions/Result.cs`
- Create: `src/Synapse.Foundation/Abstractions/IFileStorage.cs`
- Test: `tests/Synapse.Foundation.Tests/Abstractions/ResultTests.cs`

- [ ] **Step 1: 写 Result<T> 测试**

```csharp
// tests/Synapse.Foundation.Tests/Abstractions/ResultTests.cs
using FluentAssertions;

namespace Synapse.Foundation.Tests.Abstractions;

public class ResultTests
{
    [Fact]
    public void Success_should_be_successful()
    {
        var result = Result<int>.Success(42);
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_should_store_error()
    {
        var result = Result<int>.Failure("something went wrong");
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("something went wrong");
    }

    [Fact]
    public void Accessing_value_on_failure_should_throw()
    {
        var result = Result<int>.Failure("error");
        Action act = () => { var _ = result.Value; };
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Implicit_conversion_from_value()
    {
        Result<string> result = "hello";
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }
}
```

- [ ] **Step 2: 实现 Result<T>**

```csharp
// src/Synapse.Foundation/Abstractions/Result.cs
namespace Synapse.Foundation.Abstractions;

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) =>
        new(true, value, null);

    public static Result<T> Failure(string error) =>
        new(false, default, error);

    public static implicit operator Result<T>(T value) =>
        Success(value);
}
```

- [ ] **Step 3: 运行测试验证通过**

```bash
dotnet test tests/Synapse.Foundation.Tests/ --filter "ResultTests"
```

Expected: PASS.

- [ ] **Step 4: 实现 IFileStorage**

```csharp
// src/Synapse.Foundation/Abstractions/IFileStorage.cs
namespace Synapse.Foundation.Abstractions;

public interface IFileStorage
{
    Task SaveAsync<T>(string relativePath, T data, CancellationToken ct = default);
    Task<T?> LoadAsync<T>(string relativePath, CancellationToken ct = default);
    Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default);
}
```

- [ ] **Step 5: 运行全部测试并提交**

```bash
dotnet test tests/Synapse.Foundation.Tests/
git add src/Synapse.Foundation/Abstractions/ tests/Synapse.Foundation.Tests/Abstractions/
git commit -m "feat: add Result<T> and IFileStorage abstraction"
```

---

### Task 5: Ingestion — South Ports 接口定义

**Files:**
- Create: `src/Synapse.Ingestion/South/Port/SourceReader/ISourceReader.cs`
- Create: `src/Synapse.Ingestion/South/Port/Repository/ISourceItemRepository.cs`

- [ ] **Step 1: 定义 ISourceReader**

```csharp
// src/Synapse.Ingestion/South/Port/SourceReader/ISourceReader.cs
using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;

namespace Synapse.Ingestion.South.Port.SourceReader;

[Port]
public interface ISourceReader
{
    SourceType Type { get; }
    Task<IReadOnlyList<SourceItem>> FetchAsync(CancellationToken ct = default);
}
```

- [ ] **Step 2: 定义 ISourceItemRepository**

```csharp
// src/Synapse.Ingestion/South/Port/Repository/ISourceItemRepository.cs
using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;

namespace Synapse.Ingestion.South.Port.Repository;

[Port]
public interface ISourceItemRepository
{
    Task SaveAsync(DateOnly date, IEnumerable<SourceItem> items, CancellationToken ct = default);
    Task<IReadOnlyList<SourceItem>> LoadAsync(DateOnly date, CancellationToken ct = default);
}
```

- [ ] **Step 3: 验证构建并提交**

```bash
dotnet build src/Synapse.Ingestion/
git add src/Synapse.Ingestion/South/Port/
git commit -m "feat: add ingestion south port interfaces"
```

---

### Task 6: Ingestion — South Adapters (SourceItemFileAdapter)

**Files:**
- Create: `src/Synapse.Ingestion/South/Adapter/FileSystemStorage.cs`
- Create: `src/Synapse.Ingestion/South/Adapter/Repositories/SourceItemFileAdapter.cs`
- Test: `tests/Synapse.Ingestion.Tests/South/Adapter/SourceItemFileAdapterTests.cs`

- [ ] **Step 1: 写 SourceItemFileAdapter 测试**

```csharp
// tests/Synapse.Ingestion.Tests/South/Adapter/SourceItemFileAdapterTests.cs
using FluentAssertions;
using Synapse.Foundation.Shared;
using Synapse.Ingestion.South.Adapter.Repositories;

namespace Synapse.Ingestion.Tests.South.Adapter;

public class SourceItemFileAdapterTests : IDisposable
{
    private readonly string _tempDir;

    public SourceItemFileAdapterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"synapse-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public async Task Save_and_load_roundtrip()
    {
        var adapter = new SourceItemFileAdapter(_tempDir);
        var date = new DateOnly(2026, 5, 5);
        var items = new[]
        {
            new SourceItem(
                new ExternalId("github:test/repo"),
                SourceType.GitHubTrending,
                "Test Repo",
                new Uri("https://github.com/test/repo"),
                "A test repo",
                new Dictionary<string, string> { ["stars"] = "50" },
                DateTimeOffset.UtcNow
            )
        };

        await adapter.SaveAsync(date, items);
        var loaded = await adapter.LoadAsync(date);

        loaded.Should().HaveCount(1);
        loaded[0].ExternalId.Value.Should().Be("github:test/repo");
        loaded[0].Title.Should().Be("Test Repo");
    }

    [Fact]
    public async Task Load_should_return_empty_when_no_file_exists()
    {
        var adapter = new SourceItemFileAdapter(_tempDir);

        var loaded = await adapter.LoadAsync(new DateOnly(2099, 1, 1));

        loaded.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_should_deduplicate_by_external_id()
    {
        var adapter = new SourceItemFileAdapter(_tempDir);
        var date = new DateOnly(2026, 5, 5);
        var first = new[]
        {
            new SourceItem(new ExternalId("github:a/b"), SourceType.GitHubTrending,
                "A", new Uri("https://github.com/a/b"), "", new(), DateTimeOffset.UtcNow)
        };
        var second = new[]
        {
            new SourceItem(new ExternalId("github:a/b"), SourceType.GitHubTrending,
                "A Updated", new Uri("https://github.com/a/b"), "", new(), DateTimeOffset.UtcNow),
            new SourceItem(new ExternalId("github:c/d"), SourceType.GitHubTrending,
                "C", new Uri("https://github.com/c/d"), "", new(), DateTimeOffset.UtcNow)
        };

        await adapter.SaveAsync(date, first);
        await adapter.SaveAsync(date, second);
        var loaded = await adapter.LoadAsync(date);

        loaded.Should().HaveCount(2);
        loaded.Should().ContainSingle(x =>
            x.ExternalId.Value == "github:a/b" && x.Title == "A Updated");
        loaded.Should().ContainSingle(x =>
            x.ExternalId.Value == "github:c/d");
    }
}
```

- [ ] **Step 2: 实现 FileSystemStorage**

```csharp
// src/Synapse.Ingestion/South/Adapter/FileSystemStorage.cs
using System.Text.Json;
using Synapse.Foundation.Abstractions;

namespace Synapse.Ingestion.South.Adapter;

public class FileSystemStorage : IFileStorage
{
    private readonly string _basePath;

    public FileSystemStorage(string basePath) => _basePath = basePath;

    public async Task SaveAsync<T>(string relativePath, T data, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, relativePath);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(data, options);
        await File.WriteAllTextAsync(fullPath, json, ct);
    }

    public async Task<T?> LoadAsync<T>(string relativePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, relativePath);
        if (!File.Exists(fullPath)) return default;

        var json = await File.ReadAllTextAsync(fullPath, ct);
        return JsonSerializer.Deserialize<T>(json);
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }
}
```

- [ ] **Step 3: 实现 SourceItemFileAdapter**

```csharp
// src/Synapse.Ingestion/South/Adapter/Repositories/SourceItemFileAdapter.cs
using System.Text.Json;
using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;
using Synapse.Ingestion.South.Port.Repository;

namespace Synapse.Ingestion.South.Adapter.Repositories;

[Adapter]
public class SourceItemFileAdapter : ISourceItemRepository
{
    private readonly string _basePath;

    public SourceItemFileAdapter(string basePath) => _basePath = basePath;

    public async Task SaveAsync(DateOnly date, IEnumerable<SourceItem> items,
        CancellationToken ct = default)
    {
        var dir = Path.Combine(_basePath, "data", "raw", date.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dir);

        foreach (var group in items.GroupBy(i => i.Type))
        {
            var fileName = $"{group.Key.ToString().ToLowerInvariant()}.json";
            var filePath = Path.Combine(dir, fileName);

            List<SourceItem> existing = new();
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath, ct);
                existing = JsonSerializer.Deserialize<List<SourceItem>>(json) ?? new();
            }

            var merged = existing
                .Where(e => !group.Any(g => g.ExternalId == e.ExternalId))
                .Concat(group)
                .ToList();

            var options = new JsonSerializerOptions { WriteIndented = true };
            var mergedJson = JsonSerializer.Serialize(merged, options);
            await File.WriteAllTextAsync(filePath, mergedJson, ct);
        }
    }

    public async Task<IReadOnlyList<SourceItem>> LoadAsync(DateOnly date,
        CancellationToken ct = default)
    {
        var dir = Path.Combine(_basePath, "data", "raw", date.ToString("yyyy-MM-dd"));
        if (!Directory.Exists(dir)) return Array.Empty<SourceItem>();

        var results = new List<SourceItem>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var json = await File.ReadAllTextAsync(file, ct);
            var items = JsonSerializer.Deserialize<List<SourceItem>>(json);
            if (items is not null) results.AddRange(items);
        }
        return results;
    }
}
```

- [ ] **Step 4: 运行测试验证通过并提交**

```bash
dotnet test tests/Synapse.Ingestion.Tests/ --filter "SourceItemFileAdapterTests"
git add src/Synapse.Ingestion/South/Adapter/ tests/Synapse.Ingestion.Tests/
git commit -m "feat: add SourceItemFileAdapter with FileSystemStorage"
```

---

### Task 7: Ingestion — South Adapters (GitHubTrendingAdapter + HackerNewsAdapter)

**Files:**
- Create: `src/Synapse.Ingestion/South/Adapter/Sources/GitHubTrendingAdapter.cs`
- Create: `src/Synapse.Ingestion/South/Adapter/Sources/HackerNewsAdapter.cs`
- Test: `tests/Synapse.Ingestion.Tests/South/Adapter/GitHubTrendingAdapterTests.cs`
- Test: `tests/Synapse.Ingestion.Tests/South/Adapter/HackerNewsAdapterTests.cs`

- [ ] **Step 1: 写 ExternalId 和 SourceType 测试**

```csharp
// tests/Synapse.Ingestion.Tests/South/Adapter/GitHubTrendingAdapterTests.cs
using FluentAssertions;
using Synapse.Foundation.Shared;
using Synapse.Ingestion.South.Adapter.Sources;

namespace Synapse.Ingestion.Tests.South.Adapter;

public class GitHubTrendingAdapterTests
{
    [Fact]
    public void SourceType_should_be_GitHubTrending()
    {
        var adapter = new GitHubTrendingAdapter();
        adapter.Type.Should().Be(SourceType.GitHubTrending);
    }

    [Fact]
    public void ExternalId_format_is_github_prefix()
    {
        var id = new ExternalId("github:dotnet/runtime");
        id.Value.Should().Be("github:dotnet/runtime");
    }
}
```

```csharp
// tests/Synapse.Ingestion.Tests/South/Adapter/HackerNewsAdapterTests.cs
using FluentAssertions;
using Synapse.Foundation.Shared;
using Synapse.Ingestion.South.Adapter.Sources;

namespace Synapse.Ingestion.Tests.South.Adapter;

public class HackerNewsAdapterTests
{
    [Fact]
    public void SourceType_should_be_HackerNews()
    {
        var adapter = new HackerNewsAdapter();
        adapter.Type.Should().Be(SourceType.HackerNews);
    }

    [Fact]
    public void ExternalId_format_is_hn_prefix()
    {
        var id = new ExternalId("hn:37854123");
        id.Value.Should().Be("hn:37854123");
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

```bash
dotnet test tests/Synapse.Ingestion.Tests/ --filter "GitHubTrending|HackerNews"
```

Expected: FAIL — adapters not implemented.

- [ ] **Step 3: 实现 GitHubTrendingAdapter**

```csharp
// src/Synapse.Ingestion/South/Adapter/Sources/GitHubTrendingAdapter.cs
using System.Text.RegularExpressions;
using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;
using Synapse.Ingestion.South.Port.SourceReader;

namespace Synapse.Ingestion.South.Adapter.Sources;

[Adapter]
public partial class GitHubTrendingAdapter : ISourceReader
{
    private readonly HttpClient _httpClient;
    public SourceType Type => SourceType.GitHubTrending;

    public GitHubTrendingAdapter(HttpClient httpClient) => _httpClient = httpClient;
    public GitHubTrendingAdapter() : this(new HttpClient()) { }

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
            var repoMatch = Regex.Match(block, @"href=""/(""?(.+?)/(.+?))""");
            if (!repoMatch.Success) continue;

            var owner = repoMatch.Groups[2].Value.Trim();
            var name = repoMatch.Groups[3].Value.Trim();
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

            items.Add(new SourceItem(
                new ExternalId($"github:{owner}/{name}"),
                SourceType.GitHubTrending,
                $"{owner}/{name}",
                new Uri($"https://github.com/{owner}/{name}"),
                descMatch.Success ? descMatch.Groups[1].Value.Trim() : "",
                metadata,
                DateTimeOffset.UtcNow
            ));
        }

        return items;
    }
}
```

- [ ] **Step 4: 实现 HackerNewsAdapter**

```csharp
// src/Synapse.Ingestion/South/Adapter/Sources/HackerNewsAdapter.cs
using System.Text.Json;
using System.Text.Json.Serialization;
using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;
using Synapse.Ingestion.South.Port.SourceReader;

namespace Synapse.Ingestion.South.Adapter.Sources;

[Adapter]
public class HackerNewsAdapter : ISourceReader
{
    private readonly HttpClient _httpClient;
    public SourceType Type => SourceType.HackerNews;

    public HackerNewsAdapter(HttpClient httpClient) => _httpClient = httpClient;
    public HackerNewsAdapter() : this(new HttpClient()) { }

    public async Task<IReadOnlyList<SourceItem>> FetchAsync(CancellationToken ct = default)
    {
        var ids = await _httpClient.GetFromJsonAsync<int[]>(
            "https://hacker-news.firebaseio.com/v0/topstories.json", ct) ?? [];

        var items = new List<SourceItem>();
        foreach (var id in ids.Take(30))
        {
            var item = await _httpClient.GetFromJsonAsync<HnItem>(
                $"https://hacker-news.firebaseio.com/v0/item/{id}.json", ct);

            if (item is null || string.IsNullOrWhiteSpace(item.Title)) continue;

            var metadata = new Dictionary<string, string>
            {
                ["score"] = (item.Score ?? 0).ToString(),
                ["author"] = item.By ?? "unknown",
                ["commentCount"] = (item.Descendants ?? 0).ToString()
            };

            var url = item.Url is not null
                && Uri.TryCreate(item.Url, UriKind.Absolute, out var uri)
                    ? uri
                    : new Uri($"https://news.ycombinator.com/item?id={id}");

            items.Add(new SourceItem(
                new ExternalId($"hn:{id}"),
                SourceType.HackerNews,
                item.Title,
                url,
                item.Title,
                metadata,
                DateTimeOffset.UtcNow
            ));
        }

        return items;
    }

    private sealed record HnItem(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("score")] int? Score,
        [property: JsonPropertyName("by")] string? By,
        [property: JsonPropertyName("descendants")] int? Descendants
    );
}
```

- [ ] **Step 5: 运行测试验证通过并提交**

```bash
dotnet test tests/Synapse.Ingestion.Tests/
git add src/Synapse.Ingestion/South/Adapter/Sources/ tests/Synapse.Ingestion.Tests/South/Adapter/
git commit -m "feat: add GitHubTrendingAdapter and HackerNewsAdapter"
```

---

### Task 8: Ingestion — Local AppService

**Files:**
- Create: `src/Synapse.Ingestion/Local/AppService/IngestAppService.cs`
- Create: `src/Synapse.Ingestion/Local/Message/IngestCommand.cs`
- Create: `src/Synapse.Ingestion/Local/Message/IngestResult.cs`
- Test: `tests/Synapse.Ingestion.Tests/Local/AppService/IngestAppServiceTests.cs`

- [ ] **Step 1: 写 IngestAppService 测试**

```csharp
// tests/Synapse.Ingestion.Tests/Local/AppService/IngestAppServiceTests.cs
using FluentAssertions;
using Synapse.Foundation.Shared;
using Synapse.Ingestion.Local.AppService;
using Synapse.Ingestion.Local.Message;
using Synapse.Ingestion.South.Adapter.Repositories;
using Synapse.Ingestion.South.Port.SourceReader;

namespace Synapse.Ingestion.Tests.Local.AppService;

public class IngestAppServiceTests
{
    [Fact]
    public async Task Should_fetch_from_all_readers_and_save()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"synapse-ingest-{Guid.NewGuid()}");
        var repo = new SourceItemFileAdapter(tempDir);
        var readers = new ISourceReader[]
        {
            new FakeSourceReader(SourceType.GitHubTrending,
                new SourceItem(new ExternalId("github:a/b"), SourceType.GitHubTrending,
                    "A/B", new Uri("https://github.com/a/b"), "Test",
                    new(), DateTimeOffset.UtcNow)),
            new FakeSourceReader(SourceType.HackerNews,
                new SourceItem(new ExternalId("hn:123"), SourceType.HackerNews,
                    "HN Item", new Uri("https://news.ycombinator.com/item?id=123"), "Test",
                    new(), DateTimeOffset.UtcNow))
        };

        var service = new IngestAppService(readers, repo);
        var command = new IngestCommand(SourceFilter: "all",
            Date: new DateOnly(2026, 5, 5));
        var result = await service.ExecuteAsync(command);

        result.Status.Should().Be(IngestStatus.Ok);
        result.TotalFetched.Should().Be(2);
        result.Steps.Should().HaveCount(2);
        result.Steps.All(s => s.Status == IngestStatus.Ok).Should().BeTrue();

        var loaded = await repo.LoadAsync(new DateOnly(2026, 5, 5));
        loaded.Should().HaveCount(2);

        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task Should_continue_on_one_reader_failure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"synapse-ingest-{Guid.NewGuid()}");
        var repo = new SourceItemFileAdapter(tempDir);
        var readers = new ISourceReader[]
        {
            new FailingSourceReader(SourceType.GitHubTrending),
            new FakeSourceReader(SourceType.HackerNews,
                new SourceItem(new ExternalId("hn:123"), SourceType.HackerNews,
                    "HN Item", new Uri("https://news.ycombinator.com/item?id=123"), "Test",
                    new(), DateTimeOffset.UtcNow))
        };

        var service = new IngestAppService(readers, repo);
        var command = new IngestCommand(SourceFilter: "all",
            Date: new DateOnly(2026, 5, 5));
        var result = await service.ExecuteAsync(command);

        result.TotalFetched.Should().Be(1);
        result.Steps.Should().ContainSingle(s => s.Status == IngestStatus.Error);
        result.Steps.Should().ContainSingle(s => s.Status == IngestStatus.Ok);

        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task Should_return_error_when_all_readers_fail()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"synapse-ingest-{Guid.NewGuid()}");
        var repo = new SourceItemFileAdapter(tempDir);
        var readers = new ISourceReader[]
        {
            new FailingSourceReader(SourceType.GitHubTrending),
            new FailingSourceReader(SourceType.HackerNews)
        };

        var service = new IngestAppService(readers, repo);
        var result = await service.ExecuteAsync(
            new IngestCommand(SourceFilter: "all", Date: new DateOnly(2026, 5, 5)));

        result.Status.Should().Be(IngestStatus.Error);
        result.ErrorMessage.Should().NotBeNull();

        try { Directory.Delete(tempDir, true); } catch { }
    }
}

public class FakeSourceReader : ISourceReader
{
    public SourceType Type { get; }
    private readonly SourceItem[] _items;
    public FakeSourceReader(SourceType type, params SourceItem[] items)
    { Type = type; _items = items; }

    public Task<IReadOnlyList<SourceItem>> FetchAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SourceItem>>(_items);
}

public class FailingSourceReader : ISourceReader
{
    public SourceType Type { get; }
    public FailingSourceReader(SourceType type) => Type = type;

    public Task<IReadOnlyList<SourceItem>> FetchAsync(CancellationToken ct = default)
        => throw new HttpRequestException("Connection failed");
}
```

- [ ] **Step 2: 运行测试验证失败**

```bash
dotnet test tests/Synapse.Ingestion.Tests/ --filter "IngestAppServiceTests"
```

Expected: FAIL.

- [ ] **Step 3: 实现 Message DTOs（record）**

```csharp
// src/Synapse.Ingestion/Local/Message/IngestCommand.cs
namespace Synapse.Ingestion.Local.Message;

public sealed record IngestCommand(string SourceFilter, DateOnly Date);
```

```csharp
// src/Synapse.Ingestion/Local/Message/IngestResult.cs
namespace Synapse.Ingestion.Local.Message;

public sealed record IngestResult(
    int TotalFetched,
    IngestStatus Status,
    string? ErrorMessage,
    IReadOnlyList<IngestStepResult> Steps
);

public sealed record IngestStepResult(
    string SourceName,
    IngestStatus Status,
    int ItemCount,
    string? Error
);

public enum IngestStatus { Ok, Error }
```

- [ ] **Step 4: 实现 IngestAppService**

```csharp
// src/Synapse.Ingestion/Local/AppService/IngestAppService.cs
using Synapse.Foundation.Stereotype;
using Synapse.Ingestion.Local.Message;
using Synapse.Ingestion.South.Port.Repository;
using Synapse.Ingestion.South.Port.SourceReader;

namespace Synapse.Ingestion.Local.AppService;

[DomainService]
public class IngestAppService
{
    private readonly IEnumerable<ISourceReader> _readers;
    private readonly ISourceItemRepository _repository;

    public IngestAppService(
        IEnumerable<ISourceReader> readers,
        ISourceItemRepository repository)
    {
        _readers = readers;
        _repository = repository;
    }

    public async Task<IngestResult> ExecuteAsync(
        IngestCommand command, CancellationToken ct = default)
    {
        var steps = new List<IngestStepResult>();
        var allItems = new List<Foundation.Shared.SourceItem>();

        foreach (var reader in _readers)
        {
            try
            {
                var items = await reader.FetchAsync(ct);
                allItems.AddRange(items);
                steps.Add(new IngestStepResult(
                    reader.Type.ToString(), IngestStatus.Ok, items.Count, null));
            }
            catch (Exception ex)
            {
                steps.Add(new IngestStepResult(
                    reader.Type.ToString(), IngestStatus.Error, 0, ex.Message));
            }
        }

        if (allItems.Count > 0)
            await _repository.SaveAsync(command.Date, allItems, ct);

        var hasAnySuccess = steps.Any(s => s.Status == IngestStatus.Ok);

        return new IngestResult(
            TotalFetched: allItems.Count,
            Status: hasAnySuccess ? IngestStatus.Ok : IngestStatus.Error,
            ErrorMessage: hasAnySuccess ? null : "All sources failed to fetch",
            Steps: steps
        );
    }
}
```

- [ ] **Step 5: 运行测试验证通过并提交**

```bash
dotnet test tests/Synapse.Ingestion.Tests/
git add src/Synapse.Ingestion/Local/ tests/Synapse.Ingestion.Tests/Local/
git commit -m "feat: add IngestAppService with record message DTOs"
```

---

### Task 9: Digest — Domain 模型

**Files:**
- Create: `src/Synapse.Digest/Domain/Highlight.cs`
- Create: `src/Synapse.Digest/Domain/TechStack.cs`
- Create: `src/Synapse.Digest/Domain/AnalyzedItem.cs`
- Create: `src/Synapse.Digest/Domain/DigestStatus.cs`
- Create: `src/Synapse.Digest/Domain/Digest.cs`
- Create: `src/Synapse.Digest/Domain/Exception/DigestGenerationException.cs`
- Test: `tests/Synapse.Digest.Tests/Domain/DomainModelTests.cs`

- [ ] **Step 1: 写 Domain 测试**

```csharp
// tests/Synapse.Digest.Tests/Domain/DomainModelTests.cs
using FluentAssertions;

namespace Synapse.Digest.Tests.Domain;

public class DomainModelTests
{
    [Fact]
    public void Highlight_equal_by_value()
    {
        new Highlight("A").Should().Be(new Highlight("A"));
        new Highlight("A").Should().NotBe(new Highlight("B"));
    }

    [Fact]
    public void TechStack_equal_by_value()
    {
        new TechStack(["a", "b"]).Should().Be(new TechStack(["a", "b"]));
        new TechStack(["a"]).Should().NotBe(new TechStack(["b"]));
    }

    [Fact]
    public void AnalyzedItem_is_immutable_record()
    {
        var item = new AnalyzedItem(
            SourceRef: new Foundation.Shared.ExternalId("github:a/b"),
            Category: "tool",
            TechStack: new TechStack(["rust"]),
            Highlight: new Highlight("Faster builds"),
            Suitability: "production ready",
            Score: 8
        );

        item.Category.Should().Be("tool");
        item.Score.Should().Be(8);

        var modified = item with { Score = 9 };
        modified.Score.Should().Be(9);
        item.Score.Should().Be(8); // immutable
    }

    [Fact]
    public void Digest_default_status_is_pending()
    {
        var digest = new Digest(
            new DateOnly(2026, 5, 5),
            DateTimeOffset.UtcNow,
            [],
            "",
            DigestStatus.Pending
        );
        digest.Status.Should().Be(DigestStatus.Pending);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

```bash
dotnet test tests/Synapse.Digest.Tests/ --filter "DomainModelTests"
```

Expected: FAIL.

- [ ] **Step 3: 实现所有 Domain 模型（record）**

```csharp
// src/Synapse.Digest/Domain/Highlight.cs
namespace Synapse.Digest.Domain;

public sealed record Highlight(string Text);
```

```csharp
// src/Synapse.Digest/Domain/TechStack.cs
namespace Synapse.Digest.Domain;

public sealed record TechStack(IReadOnlyList<string> Tags);
```

```csharp
// src/Synapse.Digest/Domain/AnalyzedItem.cs
using Synapse.Foundation.Shared;

namespace Synapse.Digest.Domain;

public sealed record AnalyzedItem(
    ExternalId SourceRef,
    string Category,
    TechStack TechStack,
    Highlight Highlight,
    string Suitability,
    int Score
);
```

```csharp
// src/Synapse.Digest/Domain/DigestStatus.cs
namespace Synapse.Digest.Domain;

public enum DigestStatus { Pending, Published, Failed }
```

```csharp
// src/Synapse.Digest/Domain/Digest.cs
using Synapse.Foundation.Stereotype;

namespace Synapse.Digest.Domain;

[Aggregate]
public sealed record Digest(
    DateOnly Id,
    DateTimeOffset GeneratedAt,
    List<AnalyzedItem> Items,
    string Summary,
    DigestStatus Status
);
```

```csharp
// src/Synapse.Digest/Domain/Exception/DigestGenerationException.cs
namespace Synapse.Digest.Domain.Exception;

public class DigestGenerationException(string message)
    : Foundation.Exception.DomainException(message);
```

- [ ] **Step 4: 运行测试验证通过并提交**

```bash
dotnet test tests/Synapse.Digest.Tests/
git add src/Synapse.Digest/Domain/ tests/Synapse.Digest.Tests/Domain/
git commit -m "feat: add digest domain models as records"
```

---

### Task 10: Digest — South Ports 接口定义

**Files:**
- Create: `src/Synapse.Digest/South/Port/Analyzer/IAnalyzer.cs`
- Create: `src/Synapse.Digest/South/Port/Output/OutputChannel.cs`
- Create: `src/Synapse.Digest/South/Port/Output/IOutputPort.cs`
- Create: `src/Synapse.Digest/South/Port/Repository/IDigestRepository.cs`

- [ ] **Step 1: 定义所有 Port 接口**

```csharp
// src/Synapse.Digest/South/Port/Analyzer/IAnalyzer.cs
using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;
using Synapse.Digest.Domain;

namespace Synapse.Digest.South.Port.Analyzer;

[Port]
public interface IAnalyzer
{
    Task<AnalyzedItem> AnalyzeAsync(SourceItem source, CancellationToken ct = default);
}
```

```csharp
// src/Synapse.Digest/South/Port/Output/OutputChannel.cs
namespace Synapse.Digest.South.Port.Output;

public enum OutputChannel { WeCom, Slack, Email }
```

```csharp
// src/Synapse.Digest/South/Port/Output/IOutputPort.cs
using Synapse.Foundation.Stereotype;

namespace Synapse.Digest.South.Port.Output;

[Port]
public interface IOutputPort
{
    OutputChannel Channel { get; }
    Task<bool> DeliverAsync(Digest.Domain.Digest digest, CancellationToken ct = default);
}
```

```csharp
// src/Synapse.Digest/South/Port/Repository/IDigestRepository.cs
using Synapse.Foundation.Stereotype;

namespace Synapse.Digest.South.Port.Repository;

[Port]
public interface IDigestRepository
{
    Task SaveAsync(Digest.Domain.Digest digest, CancellationToken ct = default);
    Task<Digest.Domain.Digest?> GetAsync(DateOnly date, CancellationToken ct = default);
}
```

- [ ] **Step 2: 验证构建并提交**

```bash
dotnet build src/Synapse.Digest/
git add src/Synapse.Digest/South/Port/
git commit -m "feat: add digest south port interfaces"
```

---

### Task 11: Digest — South Adapters

**Files:**
- Create: `src/Synapse.Digest/South/Adapter/Repositories/DigestFileAdapter.cs`
- Create: `src/Synapse.Digest/South/Adapter/Analyzers/OpenAIAnalyzerAdapter.cs`
- Create: `src/Synapse.Digest/South/Adapter/Outputs/WeComAdapter.cs`
- Test: `tests/Synapse.Digest.Tests/South/Adapter/DigestFileAdapterTests.cs`

- [ ] **Step 1: 写 DigestFileAdapter 测试**

```csharp
// tests/Synapse.Digest.Tests/South/Adapter/DigestFileAdapterTests.cs
using FluentAssertions;
using Synapse.Digest.Domain;
using Synapse.Digest.South.Adapter.Repositories;
using Synapse.Foundation.Shared;

namespace Synapse.Digest.Tests.South.Adapter;

public class DigestFileAdapterTests : IDisposable
{
    private readonly string _tempDir;

    public DigestFileAdapterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"synapse-digest-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public async Task Save_and_load_roundtrip()
    {
        var adapter = new DigestFileAdapter(_tempDir);
        var digest = new Digest(
            Id: new DateOnly(2026, 5, 5),
            GeneratedAt: DateTimeOffset.UtcNow,
            Items: [new AnalyzedItem(
                new ExternalId("github:test/repo"), "tool",
                new TechStack(["rust"]), new Highlight("Very fast"),
                "cli tools", 8)],
            Summary: "Today's top projects",
            Status: DigestStatus.Published
        );

        await adapter.SaveAsync(digest);
        var loaded = await adapter.GetAsync(new DateOnly(2026, 5, 5));

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(new DateOnly(2026, 5, 5));
        loaded.Summary.Should().Be("Today's top projects");
        loaded.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Get_should_return_null_when_no_file()
    {
        var adapter = new DigestFileAdapter(_tempDir);
        var result = await adapter.GetAsync(new DateOnly(2099, 1, 1));
        result.Should().BeNull();
    }
}
```

- [ ] **Step 2: 实现 DigestFileAdapter**

```csharp
// src/Synapse.Digest/South/Adapter/Repositories/DigestFileAdapter.cs
using System.Text.Json;
using Synapse.Foundation.Stereotype;
using Synapse.Digest.Domain;
using Synapse.Digest.South.Port.Repository;

namespace Synapse.Digest.South.Adapter.Repositories;

[Adapter]
public class DigestFileAdapter : IDigestRepository
{
    private readonly string _basePath;

    public DigestFileAdapter(string basePath) => _basePath = basePath;

    public async Task SaveAsync(Digest digest, CancellationToken ct = default)
    {
        var dir = Path.Combine(_basePath, "data", "digests");
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"{digest.Id:yyyy-MM-dd}.json");

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(digest, options);
        await File.WriteAllTextAsync(filePath, json, ct);
    }

    public async Task<Digest?> GetAsync(DateOnly date, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, "data", "digests",
            $"{date:yyyy-MM-dd}.json");
        if (!File.Exists(filePath)) return null;

        var json = await File.ReadAllTextAsync(filePath, ct);
        return JsonSerializer.Deserialize<Digest>(json);
    }
}
```

- [ ] **Step 3: 运行测试验证通过**

```bash
dotnet test tests/Synapse.Digest.Tests/ --filter "DigestFileAdapterTests"
```

Expected: PASS.

- [ ] **Step 4: 实现 OpenAIAnalyzerAdapter（支持 Base URL 环境变量）**

```csharp
// src/Synapse.Digest/South/Adapter/Analyzers/OpenAIAnalyzerAdapter.cs
using System.Text;
using System.Text.Json;
using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;
using Synapse.Digest.Domain;
using Synapse.Digest.South.Port.Analyzer;

namespace Synapse.Digest.South.Adapter.Analyzers;

[Adapter]
public class OpenAIAnalyzerAdapter : IAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    public OpenAIAnalyzerAdapter(
        HttpClient httpClient,
        string apiKey,
        string model = "gpt-4o-mini",
        string? baseUrl = null)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _baseUrl = (baseUrl ?? "https://api.openai.com").TrimEnd('/');
    }

    public async Task<AnalyzedItem> AnalyzeAsync(SourceItem source,
        CancellationToken ct = default)
    {
        var prompt = BuildPrompt(source);
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content =
                    "You are a technical analyst. Output valid JSON only, no markdown." },
                new { role = "user", content = prompt }
            },
            temperature = 0.3
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{_baseUrl}/v1/chat/completions") { Content = content };
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);
            var message = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()!;

            return ParseResponse(message, source);
        }
        catch
        {
            // Degradation: return unanalyzed item
            return new AnalyzedItem(
                source.ExternalId,
                Category: "未分类",
                TechStack: new TechStack(Array.Empty<string>()),
                Highlight: new Highlight(source.Description.Length > 120
                    ? source.Description[..120] : source.Description),
                Suitability: "",
                Score: 0
            );
        }
    }

    private static string BuildPrompt(SourceItem source)
    {
        var starsHint = source.Metadata.TryGetValue("stars", out var s)
            ? $"{s} stars" : "";
        var scoreHint = source.Metadata.TryGetValue("score", out var sc)
            ? $"{sc} HN points" : "";
        var langHint = source.Metadata.TryGetValue("language", out var l) ? l : "";

        return $$"""
        Analyze this project and output JSON with these fields:
        {
          "category": "framework|tool|library|article|other",
          "techStack": ["tech1", "tech2"],
          "highlight": "one sentence in Chinese why this is worth attention",
          "suitability": "suitable for what scenarios",
          "score": 1-10
        }

        Project: {{source.Title}}
        Description: {{source.Description}}
        {{(starsHint + " " + scoreHint).Trim()}}
        Language: {{langHint}}
        """;
    }

    private static AnalyzedItem ParseResponse(string text, SourceItem source)
    {
        var json = text.Trim();
        if (json.StartsWith("```"))
        {
            var start = json.IndexOf('\n') + 1;
            var end = json.LastIndexOf("```");
            json = json[start..end].Trim();
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var category = root.TryGetProperty("category", out var c)
            ? c.GetString() ?? "未分类" : "未分类";

        var tags = new List<string>();
        if (root.TryGetProperty("techStack", out var ts))
            foreach (var tag in ts.EnumerateArray())
                tags.Add(tag.GetString()!);

        var highlight = root.TryGetProperty("highlight", out var h)
            ? h.GetString() ?? "" : "";

        var suitability = root.TryGetProperty("suitability", out var su)
            ? su.GetString() ?? "" : "";

        var score = root.TryGetProperty("score", out var sc)
            ? sc.GetInt32() : 0;

        return new AnalyzedItem(
            source.ExternalId,
            category,
            new TechStack(tags),
            new Highlight(highlight),
            suitability,
            Math.Clamp(score, 1, 10)
        );
    }
}
```

- [ ] **Step 5: 实现 WeComAdapter**

```csharp
// src/Synapse.Digest/South/Adapter/Outputs/WeComAdapter.cs
using System.Text;
using System.Text.Json;
using Synapse.Foundation.Stereotype;
using Synapse.Digest.Domain;
using Synapse.Digest.South.Port.Output;

namespace Synapse.Digest.South.Adapter.Outputs;

[Adapter]
public class WeComAdapter : IOutputPort
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;
    public OutputChannel Channel => OutputChannel.WeCom;

    public WeComAdapter(HttpClient httpClient, string webhookUrl)
    {
        _httpClient = httpClient;
        _webhookUrl = webhookUrl;
    }

    public async Task<bool> DeliverAsync(Digest digest, CancellationToken ct = default)
    {
        try
        {
            await SendMarkdownAsync(BuildOverview(digest), ct);
            foreach (var chunk in BuildItemChunks(digest))
                await SendMarkdownAsync(chunk, ct);
            return true;
        }
        catch { return false; }
    }

    private async Task SendMarkdownAsync(string markdown, CancellationToken ct)
    {
        var body = new { msgtype = "markdown", markdown = new { content = markdown } };
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_webhookUrl, content, ct);
        response.EnsureSuccessStatusCode();
    }

    private static string BuildOverview(Digest digest)
        => $"## Synapse 日报 {digest.Id:yyyy-MM-dd}\n\n" +
           $"**{digest.Summary}**\n\n共 {digest.Items.Count} 条资讯";

    private static IEnumerable<string> BuildItemChunks(Digest digest)
    {
        var chunk = new StringBuilder();
        foreach (var item in digest.Items)
        {
            var line =
                $"\n> **{item.Score}/10** {item.Highlight.Text}\n" +
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
}
```

- [ ] **Step 6: 验证构建并提交**

```bash
dotnet build src/Synapse.Digest/
git add src/Synapse.Digest/South/Adapter/ tests/Synapse.Digest.Tests/South/
git commit -m "feat: add digest south adapters with configurable OpenAI base URL"
```

---

### Task 12: Digest — Local AppService

**Files:**
- Create: `src/Synapse.Digest/Local/AppService/GenerateDigestAppService.cs`
- Create: `src/Synapse.Digest/Local/Message/GenerateDigestCommand.cs`
- Create: `src/Synapse.Digest/Local/Message/GenerateDigestResult.cs`
- Test: `tests/Synapse.Digest.Tests/Local/AppService/GenerateDigestAppServiceTests.cs`

- [ ] **Step 1: 写 GenerateDigestAppService 测试**

```csharp
// tests/Synapse.Digest.Tests/Local/AppService/GenerateDigestAppServiceTests.cs
using FluentAssertions;
using Synapse.Foundation.Shared;
using Synapse.Digest.Domain;
using Synapse.Digest.Local.AppService;
using Synapse.Digest.Local.Message;
using Synapse.Digest.South.Port.Analyzer;
using Synapse.Digest.South.Port.Output;
using Synapse.Digest.South.Port.Repository;
using Synapse.Digest.South.Adapter.Repositories;
using ISourceItemRepository = Synapse.Ingestion.South.Port.Repository.ISourceItemRepository;

namespace Synapse.Digest.Tests.Local.AppService;

public class GenerateDigestAppServiceTests
{
    [Fact]
    public async Task Should_load_sources_analyze_and_save_digest()
    {
        var tempDir = Path.Combine(Path.GetTempPath(),
            $"synapse-digest-app-{Guid.NewGuid()}");

        // Seed raw data
        var sourceRepo = new Synapse.Ingestion.South.Adapter.Repositories
            .SourceItemFileAdapter(tempDir);
        var sources = new[]
        {
            new SourceItem(new ExternalId("github:a/b"), SourceType.GitHubTrending,
                "Test Repo", new Uri("https://github.com/a/b"), "desc",
                new() { ["stars"] = "100" }, DateTimeOffset.UtcNow),
            new SourceItem(new ExternalId("hn:123"), SourceType.HackerNews,
                "HN Item", new Uri("https://example.com"), "desc",
                new() { ["score"] = "50" }, DateTimeOffset.UtcNow)
        };
        await sourceRepo.SaveAsync(new DateOnly(2026, 5, 5), sources);

        var digestRepo = new DigestFileAdapter(tempDir);
        var analyzer = new FakeAnalyzer();
        var outputs = new[] { new FakeOutputPort() };

        var service = new GenerateDigestAppService(
            sourceRepo, analyzer, outputs, digestRepo);
        var result = await service.ExecuteAsync(
            new GenerateDigestCommand(new DateOnly(2026, 5, 5)));

        result.Status.Should().Be(DigestGenerationStatus.Published);
        result.TotalItems.Should().Be(2);
        result.DeliveryResults[0].Success.Should().BeTrue();

        var saved = await digestRepo.GetAsync(new DateOnly(2026, 5, 5));
        saved!.Items.Should().HaveCount(2);

        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task Should_degrade_on_analyzer_failure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(),
            $"synapse-degrade-{Guid.NewGuid()}");
        var sourceRepo = new Synapse.Ingestion.South.Adapter.Repositories
            .SourceItemFileAdapter(tempDir);
        await sourceRepo.SaveAsync(new DateOnly(2026, 5, 5), [
            new SourceItem(new ExternalId("github:a/b"), SourceType.GitHubTrending,
                "Test", new Uri("https://github.com/a/b"), "A test repo",
                new(), DateTimeOffset.UtcNow)
        ]);

        var digestRepo = new DigestFileAdapter(tempDir);
        var service = new GenerateDigestAppService(
            sourceRepo, new FailingAnalyzer(),
            Array.Empty<IOutputPort>(), digestRepo);

        var result = await service.ExecuteAsync(
            new GenerateDigestCommand(new DateOnly(2026, 5, 5)));

        result.Status.Should().Be(DigestGenerationStatus.Published);
        result.TotalItems.Should().Be(1);

        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task Should_return_empty_when_no_sources()
    {
        var tempDir = Path.Combine(Path.GetTempPath(),
            $"synapse-empty-{Guid.NewGuid()}");
        var sourceRepo = new Synapse.Ingestion.South.Adapter.Repositories
            .SourceItemFileAdapter(tempDir);
        var digestRepo = new DigestFileAdapter(tempDir);
        var service = new GenerateDigestAppService(
            sourceRepo, new FakeAnalyzer(),
            Array.Empty<IOutputPort>(), digestRepo);

        var result = await service.ExecuteAsync(
            new GenerateDigestCommand(new DateOnly(2099, 1, 1)));

        result.TotalItems.Should().Be(0);

        try { Directory.Delete(tempDir, true); } catch { }
    }
}

public class FakeAnalyzer : IAnalyzer
{
    public Task<AnalyzedItem> AnalyzeAsync(SourceItem source,
        CancellationToken ct = default)
        => Task.FromResult(new AnalyzedItem(source.ExternalId, "tool",
            new TechStack(["go"]), new Highlight("Worth checking out"),
            "general", 7));
}

public class FailingAnalyzer : IAnalyzer
{
    public Task<AnalyzedItem> AnalyzeAsync(SourceItem source,
        CancellationToken ct = default)
        => throw new InvalidOperationException("API unavailable");
}

public class FakeOutputPort : IOutputPort
{
    public OutputChannel Channel => OutputChannel.WeCom;
    public Task<bool> DeliverAsync(Digest.Domain.Digest digest,
        CancellationToken ct = default) => Task.FromResult(true);
}
```

- [ ] **Step 2: 运行测试验证失败**

```bash
dotnet test tests/Synapse.Digest.Tests/ --filter "GenerateDigestAppServiceTests"
```

Expected: FAIL.

- [ ] **Step 3: 实现 Message DTOs（record）**

```csharp
// src/Synapse.Digest/Local/Message/GenerateDigestCommand.cs
namespace Synapse.Digest.Local.Message;

public sealed record GenerateDigestCommand(DateOnly Date);
```

```csharp
// src/Synapse.Digest/Local/Message/GenerateDigestResult.cs
namespace Synapse.Digest.Local.Message;

public sealed record GenerateDigestResult(
    DigestGenerationStatus Status,
    int TotalItems,
    string? ErrorMessage,
    IReadOnlyList<DeliveryStepResult> DeliveryResults
);

public sealed record DeliveryStepResult(
    string Channel,
    bool Success,
    string? Error
);

public enum DigestGenerationStatus { Published, Failed }
```

- [ ] **Step 4: 实现 GenerateDigestAppService**

```csharp
// src/Synapse.Digest/Local/AppService/GenerateDigestAppService.cs
using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;
using Synapse.Digest.Domain;
using Synapse.Digest.Local.Message;
using Synapse.Digest.South.Port.Analyzer;
using Synapse.Digest.South.Port.Output;
using Synapse.Digest.South.Port.Repository;
using ISourceItemRepository = Synapse.Ingestion.South.Port.Repository.ISourceItemRepository;

namespace Synapse.Digest.Local.AppService;

[DomainService]
public class GenerateDigestAppService
{
    private readonly ISourceItemRepository _sourceRepo;
    private readonly IAnalyzer _analyzer;
    private readonly IEnumerable<IOutputPort> _outputs;
    private readonly IDigestRepository _digestRepo;

    public GenerateDigestAppService(
        ISourceItemRepository sourceRepo,
        IAnalyzer analyzer,
        IEnumerable<IOutputPort> outputs,
        IDigestRepository digestRepo)
    {
        _sourceRepo = sourceRepo;
        _analyzer = analyzer;
        _outputs = outputs;
        _digestRepo = digestRepo;
    }

    public async Task<GenerateDigestResult> ExecuteAsync(
        GenerateDigestCommand command, CancellationToken ct = default)
    {
        var sources = await _sourceRepo.LoadAsync(command.Date, ct);

        if (sources.Count == 0)
        {
            var emptyDigest = new Digest(command.Date, DateTimeOffset.UtcNow,
                [], "今日无数据", DigestStatus.Published);
            await _digestRepo.SaveAsync(emptyDigest, ct);
            return new GenerateDigestResult(DigestGenerationStatus.Published,
                0, null, Array.Empty<DeliveryStepResult>());
        }

        // Analyze each source with degradation
        var analyzedItems = new List<AnalyzedItem>();
        foreach (var source in sources)
        {
            try
            {
                analyzedItems.Add(await _analyzer.AnalyzeAsync(source, ct));
            }
            catch
            {
                analyzedItems.Add(new AnalyzedItem(
                    source.ExternalId, "未分类",
                    new TechStack(Array.Empty<string>()),
                    new Highlight(source.Description.Length > 120
                        ? source.Description[..120] : source.Description),
                    "", 0));
            }
        }

        // Organize by score descending
        var sorted = analyzedItems
            .OrderByDescending(i => i.Score)
            .ToList();

        var summary = sorted.Count > 0
            ? $"今日共 {sorted.Count} 条技术资讯，最高评分 {sorted.Max(i => i.Score)}/10"
            : "今日无资讯";

        var digest = new Digest(command.Date, DateTimeOffset.UtcNow,
            sorted, summary, DigestStatus.Published);

        await _digestRepo.SaveAsync(digest, ct);

        // Deliver to all output channels
        var deliveryResults = new List<DeliveryStepResult>();
        foreach (var output in _outputs)
        {
            try
            {
                var success = await output.DeliverAsync(digest, ct);
                deliveryResults.Add(new DeliveryStepResult(
                    output.Channel.ToString(), success,
                    success ? null : "Delivery returned false"));
            }
            catch (Exception ex)
            {
                deliveryResults.Add(new DeliveryStepResult(
                    output.Channel.ToString(), false, ex.Message));
            }
        }

        return new GenerateDigestResult(DigestGenerationStatus.Published,
            sorted.Count, null, deliveryResults);
    }
}
```

- [ ] **Step 5: 运行测试验证通过并提交**

```bash
dotnet test tests/Synapse.Digest.Tests/
git add src/Synapse.Digest/Local/ tests/Synapse.Digest.Tests/Local/
git commit -m "feat: add GenerateDigestAppService with degradation and multi-channel delivery"
```

---

### Task 13: Synapse.Cli — DI 注册和命令路由

**Files:**
- Create: `src/Synapse.Cli/Program.cs`
- Create: `src/Synapse.Cli/appsettings.json`

- [ ] **Step 1: 写 appsettings.json**

```json
{
  "OpenAI": {
    "ApiKey": "",
    "BaseUrl": "https://api.openai.com",
    "Model": "gpt-4o-mini"
  },
  "WeCom": {
    "WebhookUrl": ""
  },
  "DataPath": "."
}
```

- [ ] **Step 2: 实现 Program.cs（环境变量优先配置）**

```csharp
// src/Synapse.Cli/Program.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Synapse.Ingestion.Local.AppService;
using Synapse.Ingestion.Local.Message;
using Synapse.Ingestion.South.Adapter.Repositories;
using Synapse.Ingestion.South.Adapter.Sources;
using Synapse.Ingestion.South.Port.Repository;
using Synapse.Ingestion.South.Port.SourceReader;
using Synapse.Digest.Local.AppService;
using Synapse.Digest.Local.Message;
using Synapse.Digest.South.Adapter.Analyzers;
using Synapse.Digest.South.Adapter.Outputs;
using Synapse.Digest.South.Adapter.Repositories;
using Synapse.Digest.South.Port.Analyzer;
using Synapse.Digest.South.Port.Output;
using Synapse.Digest.South.Port.Repository;

// ---- Configuration (env vars override appsettings) ----
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var dataPath = config.GetValue<string>("DataPath") ?? ".";

// ---- DI Container ----
var services = new ServiceCollection();

// Ingestion: Ports & Adapters
services.AddSingleton<ISourceItemRepository>(
    new SourceItemFileAdapter(dataPath));
services.AddSingleton<ISourceReader>(sp =>
    new GitHubTrendingAdapter(sp.GetRequiredService<HttpClient>()));
services.AddSingleton<ISourceReader>(sp =>
    new HackerNewsAdapter(sp.GetRequiredService<HttpClient>()));
services.AddSingleton<IngestAppService>();

// Digest: Ports & Adapters
services.AddSingleton<IDigestRepository>(
    new DigestFileAdapter(dataPath));

services.AddSingleton<IAnalyzer>(sp =>
{
    var apiKey = config.GetValue<string>("OpenAI:ApiKey")
        ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? throw new InvalidOperationException(
            "OpenAI API key is required. Set OpenAI:ApiKey in appsettings " +
            "or OPENAI_API_KEY env var.");

    var baseUrl = config.GetValue<string>("OpenAI:BaseUrl")
        ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL");

    var model = config.GetValue<string>("OpenAI:Model")
        ?? "gpt-4o-mini";

    return new OpenAIAnalyzerAdapter(
        sp.GetRequiredService<HttpClient>(), apiKey, model, baseUrl);
});

services.AddSingleton<IOutputPort>(sp =>
{
    var webhookUrl = config.GetValue<string>("WeCom:WebhookUrl")
        ?? Environment.GetEnvironmentVariable("WECOM_WEBHOOK_URL")
        ?? throw new InvalidOperationException(
            "WeCom webhook URL is required. Set WeCom:WebhookUrl in appsettings " +
            "or WECOM_WEBHOOK_URL env var.");

    return new WeComAdapter(sp.GetRequiredService<HttpClient>(), webhookUrl);
});

services.AddSingleton<GenerateDigestAppService>();
services.AddHttpClient();

var provider = services.BuildServiceProvider();

// ---- Command Routing ----
var args = Environment.GetCommandLineArgs();

if (args.Length < 2)
{
    Console.WriteLine("Usage: Synapse.Cli <command> [options]");
    Console.WriteLine("  ingest --source all [--date yyyy-MM-dd]");
    Console.WriteLine("  generate-digest [--date yyyy-MM-dd]");
    return 1;
}

var command = args[1];
var date = DateOnly.FromDateTime(DateTime.UtcNow.Date);
var dateIdx = Array.IndexOf(args, "--date");
if (dateIdx >= 0 && dateIdx + 1 < args.Length
    && DateOnly.TryParse(args[dateIdx + 1], out var parsed))
    date = parsed;

try
{
    switch (command)
    {
        case "ingest":
            var sourceArg = args.Contains("--source")
                && Array.IndexOf(args, "--source") + 1 < args.Length
                    ? args[Array.IndexOf(args, "--source") + 1] : "all";

            var ingestSvc = provider.GetRequiredService<IngestAppService>();
            var ingestResult = await ingestSvc.ExecuteAsync(
                new IngestCommand(sourceArg, date));

            Console.WriteLine($"Ingest complete: {ingestResult.TotalFetched} items, " +
                              $"status: {ingestResult.Status}");
            foreach (var step in ingestResult.Steps)
                Console.WriteLine($"  {step.SourceName}: {step.Status} " +
                                  $"({step.ItemCount} items)" +
                                  $"{(step.Error is not null ? $" - {step.Error}" : "")}");
            break;

        case "generate-digest":
            var digestSvc = provider.GetRequiredService<GenerateDigestAppService>();
            var digestResult = await digestSvc.ExecuteAsync(
                new GenerateDigestCommand(date));

            Console.WriteLine($"Digest generated: {digestResult.TotalItems} items, " +
                              $"status: {digestResult.Status}");
            foreach (var d in digestResult.DeliveryResults)
                Console.WriteLine($"  {d.Channel}: {(d.Success ? "OK" : "FAILED")}" +
                                  $"{(d.Error is not null ? $" - {d.Error}" : "")}");
            break;

        default:
            Console.Error.WriteLine($"Unknown command: {command}");
            return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

return 0;
```

- [ ] **Step 3: 确保 Cli.csproj 为 Exe 输出类型**

修改 `src/Synapse.Cli/Cli.csproj`，确保 `<OutputType>Exe</OutputType>` 存在。`dotnet new console` 模板应已默认设置。

- [ ] **Step 4: 验证构建并测试 help 输出**

```bash
dotnet build src/Synapse.Cli/
dotnet run --project src/Synapse.Cli/
```

Expected: Usage message printed.

- [ ] **Step 5: 提交**

```bash
git add src/Synapse.Cli/
git commit -m "feat: add CLI with env var config support and command routing"
```

---

### Task 14: GitHub Actions Workflows

**Files:**
- Create: `.github/workflows/ingest.yml`
- Create: `.github/workflows/daily-digest.yml`

- [ ] **Step 1: 创建 ingest workflow**

```yaml
# .github/workflows/ingest.yml
name: Ingest Sources

on:
  schedule:
    - cron: "0 */6 * * *"
  workflow_dispatch:

jobs:
  ingest:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Run ingestion
        env:
          OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
          WECOM_WEBHOOK_URL: ${{ secrets.WECOM_WEBHOOK_URL }}
        run: dotnet run --project src/Synapse.Cli -- ingest --source all

      - name: Archive raw data
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add data/
          git diff --staged --quiet || git commit -m "chore: archive ingestion data $(date +%F-%H%M)"
          git push
```

- [ ] **Step 2: 创建 daily-digest workflow**

```yaml
# .github/workflows/daily-digest.yml
name: Daily Digest

on:
  schedule:
    - cron: "0 8 * * *"
  workflow_dispatch:

jobs:
  digest:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Generate and deliver digest
        env:
          OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
          OPENAI_BASE_URL: ${{ secrets.OPENAI_BASE_URL }}
          WECOM_WEBHOOK_URL: ${{ secrets.WECOM_WEBHOOK_URL }}
        run: dotnet run --project src/Synapse.Cli -- generate-digest

      - name: Archive digest
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add data/
          git diff --staged --quiet || git commit -m "chore: archive daily digest $(date +%F)"
          git push
```

- [ ] **Step 3: 提交**

```bash
git add .github/workflows/
git commit -m "feat: add GitHub Actions workflows with env var secrets"
```

---

### Task 15: 端到端验证

- [ ] **Step 1: 运行全部单元测试**

```bash
dotnet test Synapse.slnx
```

Expected: All tests PASS.

- [ ] **Step 2: 本地端到端 ingest（无需 API Key）**

```bash
dotnet run --project src/Synapse.Cli -- ingest --source all --date 2026-05-05
```

Expected: 输出抓取结果，`data/raw/2026-05-05/` 下生成 JSON 文件。检查文件内容。

- [ ] **Step 3: 本地端到端 generate-digest（需要 API Keys）**

```bash
export OPENAI_API_KEY="sk-..."
export WECOM_WEBHOOK_URL="https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=..."
dotnet run --project src/Synapse.Cli -- generate-digest --date 2026-05-05
```

Expected: 生成日报 → 保存 `data/digests/2026-05-05.json` → 推送企业微信。

- [ ] **Step 4: 最终提交**

```bash
git status
git add .
git commit -m "chore: finalize implementation with all tests passing"
```

---

## 实现顺序依赖图

```
Task 1  (骨架)
  ├─→ Task 2  (Foundation: Stereotype + Exceptions)
  ├─→ Task 3  (Foundation: Shared types — records)
  ├─→ Task 4  (Foundation: Result<T> + IFileStorage)
  │
  ├─→ Task 5  (Ingestion: Ports)
  │     ├─→ Task 6  (Ingestion: File Adapter)
  │     ├─→ Task 7  (Ingestion: HTTP Adapters)
  │     └─→ Task 8  (Ingestion: AppService) ← depends on 6+7
  │
  └─→ Task 9  (Digest: Domain records)
        ├─→ Task 10 (Digest: Ports)
        └─→ Task 11 (Digest: Adapters — OpenAI with Base URL)
              └─→ Task 12 (Digest: AppService) ← depends on 8+11

Task 13 (CLI) ← depends on 8+12
Task 14 (CI)  ← depends on 13
Task 15 (E2E) ← depends on 14
```

Task 2-4 可部分并行；Task 5-8 和 Task 9-12 是两个独立的上下文流，可并行推进。
