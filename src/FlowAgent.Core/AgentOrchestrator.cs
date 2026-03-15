using FlowAgent.Core.Tools;

namespace FlowAgent.Core;

/// <summary>
/// 多智能体编排器 - 管理多个命名智能体，支持智能体间协作
/// </summary>
public class AgentOrchestrator
{
    private readonly Dictionary<string, Agent> _agents = new();

    /// <summary>
    /// 注册一个命名智能体
    /// </summary>
    /// <param name="name">智能体的唯一名称</param>
    /// <param name="agent">智能体实例</param>
    public void RegisterAgent(string name, Agent agent)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("智能体名称不能为空", nameof(name));

        if (_agents.ContainsKey(name))
            throw new InvalidOperationException($"智能体 '{name}' 已注册");

        _agents[name] = agent;
        Console.WriteLine($"✓ 已注册智能体: {name}");
    }

    /// <summary>
    /// 获取已注册的智能体
    /// </summary>
    public IReadOnlyDictionary<string, Agent> Agents => _agents;

    /// <summary>
    /// 向指定智能体发送消息并获取回复
    /// </summary>
    /// <param name="agentName">目标智能体名称</param>
    /// <param name="message">用户消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>智能体的回复</returns>
    public async Task<string> SendMessageAsync(
        string agentName,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!_agents.TryGetValue(agentName, out var agent))
            throw new InvalidOperationException($"未找到智能体 '{agentName}'");

        Console.WriteLine($"📨 向智能体 '{agentName}' 发送消息: {message}");
        var reply = await agent.ChatAsync(message, cancellationToken);
        Console.WriteLine($"💬 智能体 '{agentName}' 回复: {reply}");
        return reply;
    }

    /// <summary>
    /// 创建一个可供某个智能体调用其他智能体的 <see cref="SubAgentTool"/>
    /// </summary>
    /// <param name="excludeAgentName">要排除的智能体名称（通常是宿主智能体本身，避免自我调用）</param>
    public SubAgentTool CreateSubAgentTool(string? excludeAgentName = null)
    {
        return new SubAgentTool(this, excludeAgentName);
    }
}

/// <summary>
/// 子智能体工具 - 允许一个智能体调用编排器中注册的其他智能体
/// </summary>
public class SubAgentTool : ITool
{
    private readonly AgentOrchestrator _orchestrator;
    private readonly string? _excludeAgentName;

    internal SubAgentTool(AgentOrchestrator orchestrator, string? excludeAgentName)
    {
        _orchestrator = orchestrator;
        _excludeAgentName = excludeAgentName;
    }

    public string Name => "call_agent";

    public string Description => "调用另一个专业智能体来处理特定任务。";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""agent_name"": {
                ""type"": ""string"",
                ""description"": ""要调用的智能体名称""
            },
            ""message"": {
                ""type"": ""string"",
                ""description"": ""发送给目标智能体的消息""
            }
        },
        ""required"": [""agent_name"", ""message""]
    }";

    public async Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var args = System.Text.Json.JsonSerializer.Deserialize<SubAgentArgs>(arguments, options);
            if (args == null)
                return "错误: 无法解析参数";

            if (string.IsNullOrWhiteSpace(args.AgentName))
                return "错误: agent_name 不能为空";

            if (string.IsNullOrWhiteSpace(args.Message))
                return "错误: message 不能为空";

            // 防止调用自身
            if (!string.IsNullOrEmpty(_excludeAgentName) &&
                string.Equals(args.AgentName, _excludeAgentName, StringComparison.OrdinalIgnoreCase))
            {
                return $"错误: 不能调用自身智能体 '{args.AgentName}'";
            }

            if (!_orchestrator.Agents.ContainsKey(args.AgentName))
            {
                var available = string.Join(", ", _orchestrator.Agents.Keys
                    .Where(k => !string.Equals(k, _excludeAgentName, StringComparison.OrdinalIgnoreCase)));
                return $"错误: 未找到智能体 '{args.AgentName}'。可用智能体: {available}";
            }

            return await _orchestrator.SendMessageAsync(args.AgentName, args.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            return $"错误: {ex.Message}";
        }
    }

    private class SubAgentArgs
    {
        [System.Text.Json.Serialization.JsonPropertyName("agent_name")]
        public string AgentName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
