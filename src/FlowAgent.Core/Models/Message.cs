namespace FlowAgent.Core.Models;

/// <summary>
/// 消息角色
/// </summary>
public enum MessageRole
{
    /// <summary>
    /// 系统消息 - 用于设置智能体的行为和规则
    /// </summary>
    System,

    /// <summary>
    /// 用户消息 - 来自用户的输入
    /// </summary>
    User,

    /// <summary>
    /// 助手消息 - 来自智能体的回复
    /// </summary>
    Assistant,

    /// <summary>
    /// 工具消息 - 工具执行的结果
    /// </summary>
    Tool
}

/// <summary>
/// 表示对话中的一条消息
/// </summary>
public class Message
{
    /// <summary>
    /// 消息的唯一标识符
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 消息角色
    /// </summary>
    public MessageRole Role { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 工具调用信息（如果这是一个工具调用消息）
    /// </summary>
    public List<ToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// 工具调用ID（如果这是一个工具结果消息）
    /// </summary>
    public string? ToolCallId { get; set; }

    public Message()
    {
    }

    public Message(MessageRole role, string content)
    {
        Role = role;
        Content = content;
    }

    public override string ToString()
    {
        return $"[{Role}] {Content}";
    }
}

/// <summary>
/// 工具调用信息
/// </summary>
public class ToolCall
{
    /// <summary>
    /// 工具调用的唯一标识符
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 工具名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 工具参数（JSON格式）
    /// </summary>
    public string Arguments { get; set; } = string.Empty;
}
