namespace Synapse.Digest.South.Adapter.Analyzers;

public class OpenAIOptions
{
    public const string Section = "OpenAI";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.deepseek.com/";
    public string Model { get; set; } = "deepseek-v4-pro";
}
