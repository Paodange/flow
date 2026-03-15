using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowAgent.Core.Models;
using FlowAgent.Core.Tools;

namespace FlowAgent.Core.LLM;

/// <summary>
/// OpenAI 兼容的 LLM 客户端实现。
/// 支持 OpenAI、Azure OpenAI、DeepSeek、通义千问等兼容 OpenAI API 格式的服务。
/// </summary>
public class OpenAiClient : ILlmClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly int _maxTokens;
    private readonly double _temperature;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    /// <summary>
    /// 创建 OpenAI 客户端实例
    /// </summary>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">模型名称，默认 gpt-4o-mini</param>
    /// <param name="baseUrl">API 基础 URL，默认 https://api.openai.com/v1</param>
    /// <param name="maxTokens">最大生成 token 数，默认 2048</param>
    /// <param name="temperature">采样温度（0~2），默认 0.7</param>
    public OpenAiClient(
        string apiKey,
        string model = "gpt-4o-mini",
        string baseUrl = "https://api.openai.com/v1",
        int maxTokens = 2048,
        double temperature = 0.7)
        : this(new HttpClient(), apiKey, model, baseUrl, maxTokens, temperature)
    {
        _ownsHttpClient = true;
    }

    /// <summary>
    /// 使用外部提供的 <see cref="HttpClient"/> 创建实例（推荐在生产环境中配合 IHttpClientFactory 使用）。
    /// </summary>
    /// <param name="httpClient">外部管理的 HttpClient（本实例不会释放它）</param>
    /// <param name="apiKey">API 密钥</param>
    /// <param name="model">模型名称，默认 gpt-4o-mini</param>
    /// <param name="baseUrl">API 基础 URL，默认 https://api.openai.com/v1</param>
    /// <param name="maxTokens">最大生成 token 数，默认 2048</param>
    /// <param name="temperature">采样温度（0~2），默认 0.7</param>
    public OpenAiClient(
        HttpClient httpClient,
        string apiKey,
        string model = "gpt-4o-mini",
        string baseUrl = "https://api.openai.com/v1",
        int maxTokens = 2048,
        double temperature = 0.7)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API 密钥不能为空", nameof(apiKey));

        _model = model;
        _maxTokens = maxTokens;
        _temperature = temperature;
        _ownsHttpClient = false;

        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var normalizedBase = baseUrl.TrimEnd('/');
        _httpClient.BaseAddress = new Uri(normalizedBase + "/");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public async Task<LlmResponse> CompleteAsync(
        IReadOnlyList<Message> messages,
        IReadOnlyDictionary<string, ITool>? tools = null,
        CancellationToken cancellationToken = default)
    {
        var requestBody = BuildRequestBody(messages, tools);

        var jsonContent = new StringContent(
            requestBody.ToJsonString(),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("chat/completions", jsonContent, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"LLM API 请求失败 (HTTP {(int)response.StatusCode}): {responseBody}");
        }

        return ParseResponse(responseBody);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 私有辅助方法
    // ──────────────────────────────────────────────────────────────────────────

    private JsonObject BuildRequestBody(
        IReadOnlyList<Message> messages,
        IReadOnlyDictionary<string, ITool>? tools)
    {
        var body = new JsonObject
        {
            ["model"] = _model,
            ["max_tokens"] = _maxTokens,
            ["temperature"] = _temperature
        };

        // 构建消息数组
        var messagesArray = new JsonArray();
        foreach (var msg in messages)
        {
            messagesArray.Add(ConvertMessage(msg));
        }
        body["messages"] = messagesArray;

        // 如果有工具则附加工具定义
        if (tools != null && tools.Count > 0)
        {
            var toolsArray = new JsonArray();
            foreach (var tool in tools.Values)
            {
                toolsArray.Add(ConvertTool(tool));
            }
            body["tools"] = toolsArray;
            body["tool_choice"] = "auto";
        }

        return body;
    }

    /// <summary>
    /// 将内部 Message 对象转换为 OpenAI API 格式
    /// </summary>
    private static JsonObject ConvertMessage(Message message)
    {
        var obj = new JsonObject { ["role"] = RoleToString(message.Role) };

        if (message.Role == MessageRole.Assistant && message.ToolCalls != null && message.ToolCalls.Count > 0)
        {
            // Assistant 发起工具调用：content 置为 null，附带 tool_calls 数组
            obj["content"] = JsonValue.Create<string?>(null);
            var toolCallsArray = new JsonArray();
            foreach (var tc in message.ToolCalls)
            {
                toolCallsArray.Add(new JsonObject
                {
                    ["id"] = tc.Id,
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = tc.Name,
                        ["arguments"] = tc.Arguments
                    }
                });
            }
            obj["tool_calls"] = toolCallsArray;
        }
        else if (message.Role == MessageRole.Tool)
        {
            // 工具执行结果消息需要对应的 tool_call_id
            obj["content"] = message.Content;
            obj["tool_call_id"] = message.ToolCallId ?? string.Empty;
        }
        else
        {
            obj["content"] = message.Content;
        }

        return obj;
    }

    /// <summary>
    /// 将 ITool 工具定义转换为 OpenAI function calling 格式
    /// </summary>
    private static JsonObject ConvertTool(ITool tool)
    {
        JsonNode? parametersNode;
        try
        {
            parametersNode = JsonNode.Parse(tool.ParametersSchema);
        }
        catch (JsonException)
        {
            parametersNode = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() };
        }

        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = parametersNode
            }
        };
    }

    /// <summary>
    /// 解析 OpenAI API 返回的 JSON 响应
    /// </summary>
    private static LlmResponse ParseResponse(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"LLM 响应格式不正确，缺少 choices 字段: {responseBody}");
        }

        var choice = choices[0];
        var message = choice.GetProperty("message");
        var finishReason = choice.TryGetProperty("finish_reason", out var fr)
            ? fr.GetString()
            : null;

        var response = new LlmResponse { FinishReason = finishReason };

        // 获取文本内容（可能为 null）
        if (message.TryGetProperty("content", out var content) && content.ValueKind != JsonValueKind.Null)
        {
            response.Content = content.GetString();
        }

        // 获取工具调用
        if (message.TryGetProperty("tool_calls", out var toolCallsEl) &&
            toolCallsEl.ValueKind == JsonValueKind.Array)
        {
            response.ToolCalls = new List<ToolCall>();
            foreach (var tcEl in toolCallsEl.EnumerateArray())
            {
                var id = tcEl.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                var funcEl = tcEl.GetProperty("function");
                var name = funcEl.GetProperty("name").GetString() ?? "";
                var arguments = funcEl.GetProperty("arguments").GetString() ?? "{}";

                response.ToolCalls.Add(new ToolCall
                {
                    Id = id,
                    Name = name,
                    Arguments = arguments
                });
            }
        }

        return response;
    }

    private static string RoleToString(MessageRole role) => role switch
    {
        MessageRole.System => "system",
        MessageRole.User => "user",
        MessageRole.Assistant => "assistant",
        MessageRole.Tool => "tool",
        _ => "user"
    };

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
            _disposed = true;
        }
    }
}
