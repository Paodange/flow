using System.Text.Json.Serialization;

namespace FlowAgent.Core.Configuration;

/// <summary>
/// AI 服务提供商类型
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModelProvider
{
    /// <summary>OpenAI 原生接口或兼容 OpenAI 格式的接口（DeepSeek、通义千问、Ollama 等）</summary>
    OpenAI,

    /// <summary>Azure OpenAI 服务</summary>
    AzureOpenAI
}

/// <summary>
/// 用户的 AI 模型配置，可持久化保存到 JSON 配置文件。
/// 使用 <see cref="ModelSettingsManager"/> 加载和保存此配置。
/// </summary>
public class ModelSettings
{
    /// <summary>
    /// AI 服务提供商类型，默认 OpenAI
    /// </summary>
    public ModelProvider Provider { get; set; } = ModelProvider.OpenAI;

    /// <summary>
    /// API 密钥（OpenAI: sk-…；Azure OpenAI: 资源密钥）
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// API 端点 URL（可选）。
    /// <list type="bullet">
    ///   <item>OpenAI 默认: https://api.openai.com/v1（留空使用默认值）</item>
    ///   <item>Azure OpenAI: https://{resource}.openai.azure.com</item>
    ///   <item>Ollama: http://localhost:11434/v1</item>
    ///   <item>其他兼容接口: 对应的 Base URL</item>
    /// </list>
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// 模型名称，默认 gpt-4o-mini
    /// </summary>
    public string ModelId { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// 最大输出 token 数，默认 2048
    /// </summary>
    public int MaxOutputTokens { get; set; } = 2048;

    /// <summary>
    /// 采样温度（0~2），越高越随机，默认 0.7
    /// </summary>
    public double Temperature { get; set; } = 0.7;
}
