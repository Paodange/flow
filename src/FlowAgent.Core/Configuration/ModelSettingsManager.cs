using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowAgent.Core.LLM;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace FlowAgent.Core.Configuration;

/// <summary>
/// 模型配置管理器：负责加载、保存 <see cref="ModelSettings"/>，
/// 以及根据配置创建 <see cref="IChatClient"/> 和 <see cref="MicrosoftAiClient"/>。
/// </summary>
public static class ModelSettingsManager
{
    /// <summary>
    /// 默认配置文件路径（用户主目录下的 .flowagent/settings.json）
    /// </summary>
    public static string DefaultSettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".flowagent",
            "settings.json");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// 从指定路径异步加载模型配置。
    /// 若文件不存在则返回包含默认值的新实例。
    /// </summary>
    /// <param name="path">配置文件路径，为 null 时使用 <see cref="DefaultSettingsPath"/></param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task<ModelSettings> LoadAsync(
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        var filePath = path ?? DefaultSettingsPath;

        if (!File.Exists(filePath))
            return new ModelSettings();

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<ModelSettings>(json, _jsonOptions) ?? new ModelSettings();
    }

    /// <summary>
    /// 从指定路径同步加载模型配置。
    /// 若文件不存在则返回包含默认值的新实例。
    /// </summary>
    /// <param name="path">配置文件路径，为 null 时使用 <see cref="DefaultSettingsPath"/></param>
    public static ModelSettings Load(string? path = null)
    {
        var filePath = path ?? DefaultSettingsPath;

        if (!File.Exists(filePath))
            return new ModelSettings();

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<ModelSettings>(json, _jsonOptions) ?? new ModelSettings();
    }

    /// <summary>
    /// 将模型配置异步保存到指定路径（目录不存在时自动创建）。
    /// </summary>
    /// <param name="settings">要保存的模型配置</param>
    /// <param name="path">目标文件路径，为 null 时使用 <see cref="DefaultSettingsPath"/></param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task SaveAsync(
        ModelSettings settings,
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var filePath = path ?? DefaultSettingsPath;
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    /// <summary>
    /// 将模型配置同步保存到指定路径（目录不存在时自动创建）。
    /// </summary>
    /// <param name="settings">要保存的模型配置</param>
    /// <param name="path">目标文件路径，为 null 时使用 <see cref="DefaultSettingsPath"/></param>
    public static void Save(ModelSettings settings, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var filePath = path ?? DefaultSettingsPath;
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// 根据 <see cref="ModelSettings"/> 创建 Microsoft.Extensions.AI 的 <see cref="IChatClient"/>。
    /// </summary>
    /// <param name="settings">模型配置</param>
    /// <returns>配置好的 <see cref="IChatClient"/> 实例</returns>
    /// <exception cref="ArgumentNullException">settings 为 null</exception>
    /// <exception cref="InvalidOperationException">API 密钥为空，或 Azure OpenAI 缺少 Endpoint</exception>
    public static IChatClient CreateChatClient(ModelSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException(
                "ModelSettings.ApiKey 不能为空，请在配置文件中设置 API 密钥。");

        return settings.Provider switch
        {
            ModelProvider.AzureOpenAI => CreateAzureOpenAiChatClient(settings),
            _ => CreateOpenAiChatClient(settings)
        };
    }

    /// <summary>
    /// 根据 <see cref="ModelSettings"/> 创建封装好的 <see cref="MicrosoftAiClient"/>，
    /// 可直接作为 <see cref="ILlmClient"/> 传入 <see cref="FlowAgent.Core.Agent"/>。
    /// </summary>
    /// <param name="settings">模型配置</param>
    /// <returns>封装好的 <see cref="MicrosoftAiClient"/> 实例</returns>
    public static MicrosoftAiClient CreateLlmClient(ModelSettings settings)
    {
        var chatClient = CreateChatClient(settings);
        return new MicrosoftAiClient(chatClient, settings.MaxOutputTokens, settings.Temperature);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 私有工厂方法
    // ──────────────────────────────────────────────────────────────────────────

    private static OpenAIClientOptions BuildClientOptions(string endpoint) =>
        new() { Endpoint = NormalizeEndpointUri(endpoint) };

    private static Uri NormalizeEndpointUri(string endpoint) =>
        new(endpoint.TrimEnd('/') + "/");

    private static IChatClient CreateOpenAiChatClient(ModelSettings settings)
    {
        OpenAIClientOptions? options = string.IsNullOrWhiteSpace(settings.Endpoint)
            ? null
            : BuildClientOptions(settings.Endpoint);

        var chatClient = options != null
            ? new ChatClient(settings.ModelId, new ApiKeyCredential(settings.ApiKey!), options)
            : new ChatClient(settings.ModelId, settings.ApiKey!);

        return chatClient.AsIChatClient();
    }

    private static IChatClient CreateAzureOpenAiChatClient(ModelSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Endpoint))
            throw new InvalidOperationException(
                "使用 Azure OpenAI 时，ModelSettings.Endpoint 不能为空，请设置 Azure 资源端点 URL。");

        var chatClient = new ChatClient(
            settings.ModelId,
            new ApiKeyCredential(settings.ApiKey!),
            BuildClientOptions(settings.Endpoint));

        return chatClient.AsIChatClient();
    }
}
