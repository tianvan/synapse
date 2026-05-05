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
