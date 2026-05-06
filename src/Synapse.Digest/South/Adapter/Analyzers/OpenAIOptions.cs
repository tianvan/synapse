namespace Synapse.Digest.South.Adapter.Analyzers;

public class OpenAIOptions
{
    public const string Section = "OpenAI";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.deepseek.com/";
    public string Model { get; set; } = "deepseek-v4-pro";

    public string SystemPrompt { get; set; } =
        "你是一位资深技术编辑，擅长用精炼且有人情味的语言点评技术项目。\n" +
        "你的分析准确、务实，不说套话，不堆砌术语。\n" +
        "输出合法 JSON，不要 markdown。";

    public string UserPromptTemplate { get; set; } =
        "分析以下技术项目，输出 JSON：\n" +
        "{\n" +
        "  \"category\": \"framework|tool|library|article|other\",\n" +
        "  \"techStack\": [\"具体技术名\"],\n" +
        "  \"highlight\": \"一句话中文亮点，为什么值得关注（20-40字）\",\n" +
        "  \"description\": \"项目做什么、核心价值在哪（100-150字），口语化中文，像在给同事介绍\",\n" +
        "  \"suitability\": \"适合什么场景使用\",\n" +
        "  \"score\": 1-10\n" +
        "}\n\n" +
        "要求：\n" +
        "- 禁用\"赋能\"\"抓手\"\"拉通\"\"值得关注的是\"\"此外\"\"综上所述\"等套话\n" +
        "- techStack 要具体，避免只说\"AI\"，应具体到\"强化学习\"\"LLM\"\"RAG\"等\n" +
        "- description 用长短句交替，可以插入口语化过渡词（\"其实\"\"你发现没\"\"说来有趣\"）\n" +
        "- 评分严格：7 分以上必须有明显技术突破或独特设计思路\n" +
        "- 项目名、技术名词保持原文，不要翻译\n\n" +
        "项目: {{Title}}\n" +
        "描述: {{Description}}\n" +
        "{{Stars}}{{Score}}语言: {{Language}}";
}
