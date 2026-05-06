using System.Net.Http.Json;
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

            var description = item.Title ?? "";
            if (item is { Type: "story", Text: not null } && !string.IsNullOrWhiteSpace(item.Text))
            {
                description = item.Text.Length > 500 ? item.Text[..500] : item.Text;
            }

            items.Add(new SourceItem(
                new ExternalId($"hn:{id}"),
                SourceType.HackerNews,
                item.Title,
                url,
                description,
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
        [property: JsonPropertyName("descendants")] int? Descendants,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("text")] string? Text
    );
}
