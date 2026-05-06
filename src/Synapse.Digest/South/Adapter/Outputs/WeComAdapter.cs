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
        => $"## Synapse 日报 {digest.Id:yyyy-MM-dd}\n\n**{digest.Summary}**\n\n共 {digest.Items.Count} 条资讯";

    private static IEnumerable<string> BuildItemChunks(Digest digest)
    {
        var chunk = new StringBuilder();
        foreach (var item in digest.Items)
        {
            var desc = item.Description.Length > 80
                ? item.Description[..80] + "..." : item.Description;
            var line = $"\n> **{item.Score}/10** {item.Highlight.Text}\n" +
                       $"> {desc}\n" +
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
