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
            "OpenAI API key is required. Set OpenAI:ApiKey in appsettings or OPENAI_API_KEY env var.");

    var baseUrl = config.GetValue<string>("OpenAI:BaseUrl")
        ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL");

    var model = config.GetValue<string>("OpenAI:Model") ?? "gpt-4o-mini";

    return new OpenAIAnalyzerAdapter(
        sp.GetRequiredService<HttpClient>(), apiKey, model, baseUrl);
});

services.AddSingleton<IOutputPort>(sp =>
{
    var webhookUrl = config.GetValue<string>("WeCom:WebhookUrl")
        ?? Environment.GetEnvironmentVariable("WECOM_WEBHOOK_URL")
        ?? throw new InvalidOperationException(
            "WeCom webhook URL is required. Set WeCom:WebhookUrl in appsettings or WECOM_WEBHOOK_URL env var.");

    return new WeComAdapter(sp.GetRequiredService<HttpClient>(), webhookUrl);
});

services.AddSingleton<GenerateDigestAppService>();
services.AddHttpClient();

var provider = services.BuildServiceProvider();

// ---- Command Routing ----
var cliArgs = Environment.GetCommandLineArgs();

if (cliArgs.Length < 2)
{
    Console.WriteLine("Usage: Synapse.Cli <command> [options]");
    Console.WriteLine("  ingest --source all [--date yyyy-MM-dd]");
    Console.WriteLine("  generate-digest [--date yyyy-MM-dd]");
    return 1;
}

var command = cliArgs[1];
var date = DateOnly.FromDateTime(DateTime.UtcNow.Date);
var dateIdx = Array.IndexOf(cliArgs, "--date");
if (dateIdx >= 0 && dateIdx + 1 < cliArgs.Length
    && DateOnly.TryParse(cliArgs[dateIdx + 1], out var parsed))
    date = parsed;

try
{
    switch (command)
    {
        case "ingest":
            var sourceArg = cliArgs.Contains("--source")
                && Array.IndexOf(cliArgs, "--source") + 1 < cliArgs.Length
                    ? cliArgs[Array.IndexOf(cliArgs, "--source") + 1] : "all";

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
