using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using FlowAgent.Core.Models;
using FlowAgent.Core.Tools;

namespace FlowAgent.Core.LLM;

/// <summary>
/// 基于 Microsoft.Extensions.AI 的 LLM 客户端实现。
/// 支持任何实现 <see cref="IChatClient"/> 接口的 AI 服务提供商，
/// 例如 OpenAI、Azure OpenAI、Ollama 等。
/// </summary>
public class MicrosoftAiClient : ILlmClient, IDisposable
{
    private readonly IChatClient _chatClient;
    private readonly int _maxOutputTokens;
    private readonly float _temperature;
    private bool _disposed;

    /// <summary>
    /// 创建 Microsoft.Extensions.AI 客户端实例
    /// </summary>
    /// <param name="chatClient">Microsoft.Extensions.AI IChatClient 实例</param>
    /// <param name="maxOutputTokens">最大输出 token 数，默认 2048</param>
    /// <param name="temperature">采样温度（0~2），默认 0.7</param>
    public MicrosoftAiClient(IChatClient chatClient, int maxOutputTokens = 2048, double temperature = 0.7)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _maxOutputTokens = maxOutputTokens;
        _temperature = (float)temperature;
    }

    /// <inheritdoc />
    public async Task<LlmResponse> CompleteAsync(
        IReadOnlyList<Message> messages,
        IReadOnlyDictionary<string, ITool>? tools = null,
        CancellationToken cancellationToken = default)
    {
        var chatMessages = ConvertMessages(messages);
        var options = CreateChatOptions(tools);

        var response = await _chatClient.GetResponseAsync(chatMessages, options, cancellationToken);
        return ParseChatResponse(response);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamCompleteAsync(
        IReadOnlyList<Message> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatMessages = ConvertMessages(messages);
        var options = new ChatOptions
        {
            MaxOutputTokens = _maxOutputTokens,
            Temperature = _temperature
        };

        await foreach (var update in _chatClient.GetStreamingResponseAsync(chatMessages, options, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 私有辅助方法
    // ──────────────────────────────────────────────────────────────────────────

    private static List<ChatMessage> ConvertMessages(IReadOnlyList<Message> messages)
    {
        var result = new List<ChatMessage>(messages.Count);
        foreach (var msg in messages)
            result.Add(ConvertMessage(msg));
        return result;
    }

    private static ChatMessage ConvertMessage(Message message)
    {
        var role = message.Role switch
        {
            MessageRole.System => ChatRole.System,
            MessageRole.User => ChatRole.User,
            MessageRole.Assistant => ChatRole.Assistant,
            MessageRole.Tool => ChatRole.Tool,
            _ => ChatRole.User
        };

        // Assistant 消息包含工具调用时，需要同时携带工具调用内容
        if (message.Role == MessageRole.Assistant && message.ToolCalls?.Count > 0)
        {
            var contents = new List<AIContent>();
            if (!string.IsNullOrEmpty(message.Content))
                contents.Add(new TextContent(message.Content));
            foreach (var tc in message.ToolCalls)
                contents.Add(new FunctionCallContent(tc.Id, tc.Name, ParseJsonArguments(tc.Arguments)));
            return new ChatMessage(role, contents);
        }

        // 工具执行结果消息需要包含 FunctionResultContent
        if (message.Role == MessageRole.Tool)
        {
            var contents = new List<AIContent>
            {
                new FunctionResultContent(message.ToolCallId ?? string.Empty, message.Content)
            };
            return new ChatMessage(ChatRole.Tool, contents);
        }

        return new ChatMessage(role, message.Content);
    }

    private ChatOptions CreateChatOptions(IReadOnlyDictionary<string, ITool>? tools)
    {
        var options = new ChatOptions
        {
            MaxOutputTokens = _maxOutputTokens,
            Temperature = _temperature
        };

        if (tools is { Count: > 0 })
        {
            var emptySchema = JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone();
            var aiTools = new List<AITool>(tools.Count);
            foreach (var tool in tools.Values)
            {
                JsonElement schemaElement;
                try
                {
                    schemaElement = JsonDocument.Parse(tool.ParametersSchema).RootElement.Clone();
                }
                catch (JsonException)
                {
                    schemaElement = emptySchema;
                }

                aiTools.Add(AIFunctionFactory.CreateDeclaration(tool.Name, tool.Description, schemaElement));
            }

            options.Tools = aiTools;
            options.ToolMode = ChatToolMode.Auto;
        }

        return options;
    }

    private static LlmResponse ParseChatResponse(ChatResponse response)
    {
        var result = new LlmResponse
        {
            FinishReason = response.FinishReason?.Value
        };

        var textBuilder = new StringBuilder();
        List<ToolCall>? toolCalls = null;

        foreach (var message in response.Messages)
        {
            foreach (var content in message.Contents)
            {
                if (content is TextContent text)
                    textBuilder.Append(text.Text);
                else if (content is FunctionCallContent funcCall)
                {
                    toolCalls ??= new List<ToolCall>();
                    toolCalls.Add(new ToolCall
                    {
                        Id = funcCall.CallId,
                        Name = funcCall.Name,
                        Arguments = SerializeArguments(funcCall.Arguments)
                    });
                }
            }
        }

        var textResult = textBuilder.ToString();
        if (!string.IsNullOrEmpty(textResult))
            result.Content = textResult;

        if (toolCalls?.Count > 0)
            result.ToolCalls = toolCalls;

        return result;
    }

    private static IDictionary<string, object?>? ParseJsonArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments) || arguments == "{}")
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SerializeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments == null || arguments.Count == 0)
            return "{}";

        try
        {
            return JsonSerializer.Serialize(arguments);
        }
        catch (JsonException)
        {
            return "{}";
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _chatClient.Dispose();
            _disposed = true;
        }
    }
}
