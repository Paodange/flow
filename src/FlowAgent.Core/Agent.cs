using FlowAgent.Core.LLM;
using FlowAgent.Core.Models;
using FlowAgent.Core.Tools;

namespace FlowAgent.Core;

/// <summary>
/// 智能体配置
/// </summary>
public class AgentConfig
{
    /// <summary>
    /// 智能体名称
    /// </summary>
    public string Name { get; set; } = "FlowAgent";

    /// <summary>
    /// 系统提示词 - 定义智能体的行为和角色
    /// </summary>
    public string SystemPrompt { get; set; } = "你是一个有用的AI助手。";

    /// <summary>
    /// 最大对话历史长度
    /// </summary>
    public int MaxHistoryLength { get; set; } = 100;

    /// <summary>
    /// 是否启用工具调用
    /// </summary>
    public bool EnableTools { get; set; } = true;

    // ── LLM 相关配置 ─────────────────────────────────────────────────────────

    /// <summary>
    /// LLM API 密钥（例如 OpenAI sk-…）
    /// </summary>
    public string? LlmApiKey { get; set; }

    /// <summary>
    /// LLM 服务基础 URL，支持 OpenAI 兼容接口，默认 https://api.openai.com/v1
    /// </summary>
    public string LlmBaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>
    /// 使用的模型名称，默认 gpt-4o-mini
    /// </summary>
    public string LlmModel { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// 最大生成 token 数，默认 2048
    /// </summary>
    public int LlmMaxTokens { get; set; } = 2048;

    /// <summary>
    /// 采样温度（0~2），越高越随机，默认 0.7
    /// </summary>
    public double LlmTemperature { get; set; } = 0.7;

    /// <summary>
    /// 智能体循环的最大迭代次数（防止工具调用死循环），默认 10
    /// </summary>
    public int MaxIterations { get; set; } = 10;
}

/// <summary>
/// 智能体类 - 核心类，管理对话和工具调用
/// </summary>
public class Agent
{
    private readonly AgentConfig _config;
    private readonly List<Message> _conversationHistory;
    private readonly Dictionary<string, ITool> _tools;
    private readonly ILlmClient? _llmClient;

    /// <summary>
    /// 智能体配置
    /// </summary>
    public AgentConfig Config => _config;

    /// <summary>
    /// 对话历史
    /// </summary>
    public IReadOnlyList<Message> ConversationHistory => _conversationHistory.AsReadOnly();

    /// <summary>
    /// 已注册的工具
    /// </summary>
    public IReadOnlyDictionary<string, ITool> Tools => _tools;

    /// <summary>
    /// 创建智能体
    /// </summary>
    /// <param name="config">智能体配置（为 null 时使用默认配置）</param>
    /// <param name="llmClient">
    ///   LLM 客户端实现。传入 null 时会尝试用 <see cref="AgentConfig"/> 中的 LLM 配置
    ///   自动创建 <see cref="OpenAiClient"/>；若配置中也没有 API Key 则保持为 null，
    ///   此时调用 <see cref="ChatAsync"/> 将抛出异常。
    /// </param>
    public Agent(AgentConfig? config = null, ILlmClient? llmClient = null)
    {
        _config = config ?? new AgentConfig();
        _conversationHistory = new List<Message>();
        _tools = new Dictionary<string, ITool>();

        // 如果调用者没有传入 llmClient，尝试用配置自动创建
        if (llmClient != null)
        {
            _llmClient = llmClient;
        }
        else if (!string.IsNullOrWhiteSpace(_config.LlmApiKey))
        {
            _llmClient = new OpenAiClient(
                _config.LlmApiKey,
                _config.LlmModel,
                _config.LlmBaseUrl,
                _config.LlmMaxTokens,
                _config.LlmTemperature);
        }

        // 添加系统消息
        if (!string.IsNullOrEmpty(_config.SystemPrompt))
        {
            _conversationHistory.Add(new Message(MessageRole.System, _config.SystemPrompt));
        }
    }

    /// <summary>
    /// 注册工具
    /// </summary>
    public void RegisterTool(ITool tool)
    {
        if (_tools.ContainsKey(tool.Name))
        {
            throw new InvalidOperationException($"工具 '{tool.Name}' 已经注册");
        }

        _tools[tool.Name] = tool;
        Console.WriteLine($"✓ 已注册工具: {tool.Name} - {tool.Description}");
    }

    /// <summary>
    /// 添加用户消息到对话历史
    /// </summary>
    public void AddUserMessage(string content)
    {
        var message = new Message(MessageRole.User, content);
        _conversationHistory.Add(message);

        // 限制历史长度
        if (_conversationHistory.Count > _config.MaxHistoryLength)
        {
            // 保留系统消息，删除最旧的对话
            var systemMessages = _conversationHistory.Where(m => m.Role == MessageRole.System).ToList();
            var otherMessages = _conversationHistory.Where(m => m.Role != MessageRole.System)
                .Skip(_conversationHistory.Count - _config.MaxHistoryLength + systemMessages.Count)
                .ToList();

            _conversationHistory.Clear();
            _conversationHistory.AddRange(systemMessages);
            _conversationHistory.AddRange(otherMessages);
        }
    }

    /// <summary>
    /// 添加助手消息到对话历史
    /// </summary>
    public void AddAssistantMessage(string content, List<ToolCall>? toolCalls = null)
    {
        var message = new Message(MessageRole.Assistant, content)
        {
            ToolCalls = toolCalls
        };
        _conversationHistory.Add(message);
    }

    /// <summary>
    /// 执行工具调用
    /// </summary>
    public async Task<string> ExecuteToolAsync(string toolName, string arguments, CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            return $"错误: 未找到工具 '{toolName}'";
        }

        Console.WriteLine($"🔧 执行工具: {toolName}");
        Console.WriteLine($"   参数: {arguments}");

        var result = await tool.ExecuteAsync(arguments, cancellationToken);

        Console.WriteLine($"   结果: {result}");

        return result;
    }

    /// <summary>
    /// 清空对话历史（保留系统消息）
    /// </summary>
    public void ClearHistory()
    {
        var systemMessages = _conversationHistory.Where(m => m.Role == MessageRole.System).ToList();
        _conversationHistory.Clear();
        _conversationHistory.AddRange(systemMessages);
    }

    /// <summary>
    /// 获取对话历史的摘要信息
    /// </summary>
    public string GetHistorySummary()
    {
        var summary = $"对话历史: {_conversationHistory.Count} 条消息\n";
        summary += $"- 系统消息: {_conversationHistory.Count(m => m.Role == MessageRole.System)} 条\n";
        summary += $"- 用户消息: {_conversationHistory.Count(m => m.Role == MessageRole.User)} 条\n";
        summary += $"- 助手消息: {_conversationHistory.Count(m => m.Role == MessageRole.Assistant)} 条\n";
        summary += $"- 工具消息: {_conversationHistory.Count(m => m.Role == MessageRole.Tool)} 条\n";
        summary += $"已注册工具: {_tools.Count} 个";

        return summary;
    }

    /// <summary>
    /// 向智能体发送一条用户消息，并通过 LLM 驱动完整的 推理→工具调用→回复 循环。
    /// </summary>
    /// <remarks>
    /// 工作流程：
    /// <list type="number">
    ///   <item>将用户消息加入对话历史</item>
    ///   <item>调用 LLM，传递当前对话历史与可用工具列表</item>
    ///   <item>若 LLM 返回工具调用请求，则依次执行工具，将结果写入历史，然后再次调用 LLM</item>
    ///   <item>重复步骤 2-3，直到 LLM 返回最终文本回复，或达到最大迭代次数</item>
    ///   <item>将最终回复写入历史并返回</item>
    /// </list>
    /// </remarks>
    /// <param name="userMessage">用户输入的消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>智能体的最终文本回复</returns>
    /// <exception cref="InvalidOperationException">未配置 LLM 客户端时抛出</exception>
    public async Task<string> ChatAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        if (_llmClient == null)
        {
            throw new InvalidOperationException(
                "未配置 LLM 客户端。请在 AgentConfig 中设置 LlmApiKey，或在构造 Agent 时传入 ILlmClient 实例。");
        }

        // 1. 将用户消息写入历史
        AddUserMessage(userMessage);

        var tools = _config.EnableTools && _tools.Count > 0 ? _tools : null;

        for (int iteration = 0; iteration < _config.MaxIterations; iteration++)
        {
            // 2. 调用 LLM
            Console.WriteLine($"🤖 调用 LLM（第 {iteration + 1} 次迭代）...");
            var llmResponse = await _llmClient.CompleteAsync(_conversationHistory, tools, cancellationToken);

            if (llmResponse.HasToolCalls)
            {
                // 3a. LLM 请求调用工具：先把 assistant 的工具调用消息写入历史
                var assistantMessage = new Message(MessageRole.Assistant, llmResponse.Content ?? string.Empty)
                {
                    ToolCalls = llmResponse.ToolCalls
                };
                _conversationHistory.Add(assistantMessage);

                // 3b. 逐一执行工具，把结果写入历史
                foreach (var toolCall in llmResponse.ToolCalls!)
                {
                    Console.WriteLine($"🔧 LLM 请求调用工具: {toolCall.Name}");
                    Console.WriteLine($"   参数: {toolCall.Arguments}");

                    string toolResult;
                    if (_tools.TryGetValue(toolCall.Name, out var tool))
                    {
                        toolResult = await tool.ExecuteAsync(toolCall.Arguments, cancellationToken);
                    }
                    else
                    {
                        toolResult = $"错误: 未找到工具 '{toolCall.Name}'";
                    }

                    Console.WriteLine($"   结果: {toolResult}");

                    _conversationHistory.Add(new Message(MessageRole.Tool, toolResult)
                    {
                        ToolCallId = toolCall.Id
                    });
                }

                // 3c. 继续循环，让 LLM 看到工具结果后再做决策
                continue;
            }

            // 4. LLM 返回了最终文本回复
            var reply = llmResponse.Content ?? string.Empty;
            _conversationHistory.Add(new Message(MessageRole.Assistant, reply));
            return reply;
        }

        // 超出最大迭代次数，返回错误信息
        var fallback = $"[智能体达到最大迭代次数 {_config.MaxIterations}，未能获得最终回复]";
        _conversationHistory.Add(new Message(MessageRole.Assistant, fallback));
        return fallback;
    }
}
