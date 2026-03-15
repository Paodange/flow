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

    /// <summary>
    /// 是否并行执行同一轮中的多个工具调用（默认 false，顺序执行）
    /// </summary>
    public bool ParallelToolExecution { get; set; } = false;

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

    // ── 重试策略配置 ──────────────────────────────────────────────────────────

    /// <summary>
    /// LLM 调用失败时的最大重试次数（0 表示不重试），默认 3
    /// </summary>
    public int LlmMaxRetries { get; set; } = 3;

    /// <summary>
    /// 首次重试前的等待时间（毫秒），默认 1000ms；后续重试按指数退避递增
    /// </summary>
    public int LlmRetryDelayMs { get; set; } = 1000;
}

/// <summary>
/// 智能体类 - 核心类，管理对话和工具调用
/// </summary>
public class Agent
{
    private readonly AgentConfig _config;
    private readonly List<Message> _conversationHistory;
    private readonly Dictionary<string, ITool> _tools;
    private readonly HashSet<string> _disabledTools = new(StringComparer.OrdinalIgnoreCase);
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
    /// 注销（移除）已注册的工具。若工具不存在则静默忽略。
    /// </summary>
    /// <param name="toolName">工具名称</param>
    public void UnregisterTool(string toolName)
    {
        if (_tools.Remove(toolName))
        {
            _disabledTools.Remove(toolName);
            Console.WriteLine($"✗ 已注销工具: {toolName}");
        }
    }

    /// <summary>
    /// 禁用指定工具（工具保留注册状态，但不会传递给 LLM）。
    /// </summary>
    /// <param name="toolName">工具名称</param>
    public void DisableTool(string toolName)
    {
        if (!_tools.ContainsKey(toolName))
        {
            throw new InvalidOperationException($"工具 '{toolName}' 未注册，无法禁用");
        }

        _disabledTools.Add(toolName);
        Console.WriteLine($"⏸️  工具已禁用: {toolName}");
    }

    /// <summary>
    /// 启用已被禁用的工具。
    /// </summary>
    /// <param name="toolName">工具名称</param>
    public void EnableTool(string toolName)
    {
        if (!_tools.ContainsKey(toolName))
        {
            throw new InvalidOperationException($"工具 '{toolName}' 未注册，无法启用");
        }

        if (_disabledTools.Remove(toolName))
        {
            Console.WriteLine($"▶️  工具已启用: {toolName}");
        }
    }

    /// <summary>
    /// 获取当前处于启用状态的工具字典（已注册且未被禁用的工具）。
    /// </summary>
    public IReadOnlyDictionary<string, ITool> GetActiveTools()
    {
        return _tools
            .Where(kv => !_disabledTools.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
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
        if (_disabledTools.Count > 0)
        {
            summary += $"（其中 {_disabledTools.Count} 个已禁用）";
        }

        return summary;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 对话历史持久化
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 将当前对话历史序列化为 JSON 并保存到指定文件
    /// </summary>
    /// <param name="filePath">目标文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task SaveHistoryAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(_conversationHistory, options);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
        Console.WriteLine($"💾 对话历史已保存到: {filePath}（{_conversationHistory.Count} 条消息）");
    }

    /// <summary>
    /// 从指定文件加载对话历史（会替换当前历史，但保留现有系统消息）
    /// </summary>
    /// <param name="filePath">源文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task LoadHistoryAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"对话历史文件不存在: {filePath}");

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        var loaded = System.Text.Json.JsonSerializer.Deserialize<List<Message>>(json, options);
        if (loaded == null)
            throw new InvalidOperationException("对话历史文件格式无效");

        _conversationHistory.Clear();
        _conversationHistory.AddRange(loaded);
        Console.WriteLine($"📂 对话历史已从 '{filePath}' 加载（{loaded.Count} 条消息）");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // LLM 调用（含重试）
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 带指数退避重试的 LLM 调用
    /// </summary>
    private async Task<LlmResponse> CompleteWithRetryAsync(
        IReadOnlyDictionary<string, ITool>? tools,
        CancellationToken cancellationToken)
    {
        int maxRetries = _config.LlmMaxRetries;
        int delayMs = _config.LlmRetryDelayMs;

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return await _llmClient!.CompleteAsync(_conversationHistory, tools, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                int waitMs = Math.Min(delayMs * (1 << attempt), 30_000); // 指数退避，最长 30 秒
                Console.WriteLine($"⚠️  LLM 调用失败（第 {attempt + 1}/{maxRetries} 次重试，{waitMs}ms 后重试）: {ex.Message}");
                await Task.Delay(waitMs, cancellationToken);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ChatAsync（含并行工具执行）
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 向智能体发送一条用户消息，并通过 LLM 驱动完整的 推理→工具调用→回复 循环。
    /// </summary>
    /// <remarks>
    /// 工作流程：
    /// <list type="number">
    ///   <item>将用户消息加入对话历史</item>
    ///   <item>调用 LLM，传递当前对话历史与可用工具列表</item>
    ///   <item>若 LLM 返回工具调用请求，则执行工具（支持并行），将结果写入历史，然后再次调用 LLM</item>
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

        var activeTools = GetActiveTools();
        var tools = _config.EnableTools && activeTools.Count > 0 ? activeTools : null;

        for (int iteration = 0; iteration < _config.MaxIterations; iteration++)
        {
            // 2. 调用 LLM（含重试）
            Console.WriteLine($"🤖 调用 LLM（第 {iteration + 1} 次迭代）...");
            var llmResponse = await CompleteWithRetryAsync(tools, cancellationToken);

            if (llmResponse.HasToolCalls)
            {
                // 3a. LLM 请求调用工具：先把 assistant 的工具调用消息写入历史
                var assistantMessage = new Message(MessageRole.Assistant, llmResponse.Content ?? string.Empty)
                {
                    ToolCalls = llmResponse.ToolCalls
                };
                _conversationHistory.Add(assistantMessage);

                // 3b. 执行工具（顺序或并行）
                if (_config.ParallelToolExecution)
                {
                    await ExecuteToolCallsParallelAsync(llmResponse.ToolCalls!, cancellationToken);
                }
                else
                {
                    await ExecuteToolCallsSequentialAsync(llmResponse.ToolCalls!, cancellationToken);
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

    /// <summary>
    /// 以流式方式向智能体发送消息，实时返回 LLM 生成的文本片段（不执行工具调用）。
    /// </summary>
    /// <param name="userMessage">用户输入的消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>依次生成的文本片段</returns>
    /// <exception cref="InvalidOperationException">未配置 LLM 客户端时抛出</exception>
    public async IAsyncEnumerable<string> ChatStreamAsync(
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_llmClient == null)
        {
            throw new InvalidOperationException(
                "未配置 LLM 客户端。请在 AgentConfig 中设置 LlmApiKey，或在构造 Agent 时传入 ILlmClient 实例。");
        }

        // 将用户消息写入历史
        AddUserMessage(userMessage);

        Console.WriteLine("🤖 开始流式 LLM 调用...");

        var replyBuilder = new System.Text.StringBuilder();

        await foreach (var chunk in _llmClient.StreamCompleteAsync(_conversationHistory, cancellationToken))
        {
            replyBuilder.Append(chunk);
            yield return chunk;
        }

        // 将完整回复写入历史
        var fullReply = replyBuilder.ToString();
        _conversationHistory.Add(new Message(MessageRole.Assistant, fullReply));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 私有工具执行方法
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>顺序执行工具调用列表</summary>
    private async Task ExecuteToolCallsSequentialAsync(
        List<ToolCall> toolCalls,
        CancellationToken cancellationToken)
    {
        foreach (var toolCall in toolCalls)
        {
            var result = await InvokeToolAsync(toolCall, cancellationToken);
            _conversationHistory.Add(new Message(MessageRole.Tool, result)
            {
                ToolCallId = toolCall.Id
            });
        }
    }

    /// <summary>
    /// 并行执行工具调用列表，按原始顺序将结果写入对话历史。
    /// 注意：并行模式下各工具的副作用（如文件写入）顺序不可预期，请谨慎使用。
    /// </summary>
    private async Task ExecuteToolCallsParallelAsync(
        List<ToolCall> toolCalls,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"⚡ 并行执行 {toolCalls.Count} 个工具调用...");

        // 并行触发所有工具
        var tasks = toolCalls.Select(tc => InvokeToolAsync(tc, cancellationToken)).ToList();
        var results = await Task.WhenAll(tasks);

        // 按原顺序写入历史（保证 OpenAI API 对 tool_call_id 顺序的要求）
        for (int i = 0; i < toolCalls.Count; i++)
        {
            _conversationHistory.Add(new Message(MessageRole.Tool, results[i])
            {
                ToolCallId = toolCalls[i].Id
            });
        }
    }

    /// <summary>执行单个工具调用并返回结果字符串</summary>
    private async Task<string> InvokeToolAsync(ToolCall toolCall, CancellationToken cancellationToken)
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
        return toolResult;
    }
}
