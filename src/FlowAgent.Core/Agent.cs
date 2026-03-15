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
}

/// <summary>
/// 智能体类 - 核心类，管理对话和工具调用
/// </summary>
public class Agent
{
    private readonly AgentConfig _config;
    private readonly List<Message> _conversationHistory;
    private readonly Dictionary<string, ITool> _tools;

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

    public Agent(AgentConfig? config = null)
    {
        _config = config ?? new AgentConfig();
        _conversationHistory = new List<Message>();
        _tools = new Dictionary<string, ITool>();

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
}
