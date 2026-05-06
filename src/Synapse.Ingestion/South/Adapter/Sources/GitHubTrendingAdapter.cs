using System.Text.Json;
using System.Text.RegularExpressions;
using Synapse.Foundation.Shared;
using Synapse.Foundation.Stereotype;
using Synapse.Ingestion.South.Port.SourceReader;

namespace Synapse.Ingestion.South.Adapter.Sources;

[Adapter]
public class GitHubTrendingAdapter : ISourceReader
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
            var repoMatch = Regex.Match(block, @"href=""/(""?([^/""\s]+?)/([^/""\s]+?))""");
            if (!repoMatch.Success) continue;

            var owner = repoMatch.Groups[2].Value.Trim();
            var name = repoMatch.Groups[3].Value.Trim();

            // Filter out non-repository links (login, sponsors, settings, etc.)
            if (owner.Contains('?') || owner.Contains('%') || owner.Contains('&')
                || owner is "login" or "sponsors" or "settings" or "features" or "orgs"
                || name.Contains('?'))
                continue;
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

            var htmlDescription = descMatch.Success
                ? descMatch.Groups[1].Value.Trim() : "";

            var item = new SourceItem(
                new ExternalId($"github:{owner}/{name}"),
                SourceType.GitHubTrending,
                $"{owner}/{name}",
                new Uri($"https://github.com/{owner}/{name}"),
                htmlDescription,
                metadata,
                DateTimeOffset.UtcNow
            );

            items.Add(await EnrichFromApiAsync(item, owner, name, ct));
        }

        return items;
    }

    private async Task<SourceItem> EnrichFromApiAsync(
        SourceItem item, string owner, string name, CancellationToken ct)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{owner}/{name}");
            request.Headers.Add("User-Agent", "Synapse/1.0");
            request.Headers.Add("Accept", "application/vnd.github.v3+json");

            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return item;

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;

            var apiDescription = root.TryGetProperty("description", out var desc)
                && desc.ValueKind == JsonValueKind.String ? desc.GetString() : null;
            var topics = new List<string>();
            if (root.TryGetProperty("topics", out var ts) && ts.ValueKind == JsonValueKind.Array)
                foreach (var t in ts.EnumerateArray()) topics.Add(t.GetString()!);

            var description = !string.IsNullOrWhiteSpace(apiDescription)
                ? apiDescription : item.Description;
            var metadata = new Dictionary<string, string>(item.Metadata);
            if (topics.Count > 0) metadata["topics"] = string.Join(", ", topics);

            return item with { Description = description, Metadata = metadata };
        }
        catch
        {
            return item;
        }
    }
}
