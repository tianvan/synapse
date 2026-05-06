using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;
using Synapse.Digest.Domain;
using Synapse.Digest.South.Port.Analyzer;

namespace Synapse.Digest.South.Adapter.Analyzers;

[Adapter]
public class OpenAIAnalyzerAdapter : IAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly OpenAIOptions _options;
    private readonly ILogger<OpenAIAnalyzerAdapter> _logger;

    public OpenAIAnalyzerAdapter(
        HttpClient httpClient,
        IOptions<OpenAIOptions> options,
        ILogger<OpenAIAnalyzerAdapter> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AnalyzedItem> AnalyzeAsync(SourceItem source, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(source);
        var requestBody = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = "You are a technical analyst. Output valid JSON only, no markdown." },
                new { role = "user", content = prompt }
            },
            temperature = 0.3
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{_options.BaseUrl.TrimEnd('/')}/v1/chat/completions") { Content = content };
        request.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("OpenAI API returned {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode, errorBody);
                return Degrade(source);
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);
            var message = doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString()!;

            _logger.LogDebug("AI response for {SourceId}: {Content}",
                source.ExternalId, message);

            return ParseResponse(message, source);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis failed for {SourceId}: {Error}",
                source.ExternalId, ex.Message);
            return Degrade(source);
        }
    }

    private static AnalyzedItem Degrade(SourceItem source) =>
        new(
            source.ExternalId,
            Category: "未分类",
            TechStack: new TechStack(Array.Empty<string>()),
            Highlight: new Highlight(source.Description.Length > 120
                ? source.Description[..120] : source.Description),
            Description: "",
            Suitability: "",
            Score: 0
        );

    private static string BuildPrompt(SourceItem source)
    {
        var starsHint = source.Metadata.TryGetValue("stars", out var s) ? $"{s} stars" : "";
        var scoreHint = source.Metadata.TryGetValue("score", out var sc) ? $"{sc} HN points" : "";
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

        var category = root.TryGetProperty("category", out var c) ? c.GetString() ?? "未分类" : "未分类";
        var tags = new List<string>();
        if (root.TryGetProperty("techStack", out var ts))
            foreach (var tag in ts.EnumerateArray()) tags.Add(tag.GetString()!);
        var highlight = root.TryGetProperty("highlight", out var h) ? h.GetString() ?? "" : "";
        var description = root.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";
        var suitability = root.TryGetProperty("suitability", out var su) ? su.GetString() ?? "" : "";
        var score = root.TryGetProperty("score", out var sc) ? sc.GetInt32() : 0;

        return new AnalyzedItem(source.ExternalId, category, new TechStack(tags),
            new Highlight(highlight), description, suitability, Math.Clamp(score, 1, 10));
    }
}
