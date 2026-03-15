using FlowAgent.Core.Models;

namespace FlowAgent.Core.LLM;

/// <summary>
/// LLM 调用的响应结果
/// </summary>
public class LlmResponse
{
    /// <summary>
    /// 文本回复内容（当 LLM 直接给出文字答案时）
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 工具调用列表（当 LLM 决定调用工具时）
    /// </summary>
    public List<ToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// 是否包含工具调用
    /// </summary>
    public bool HasToolCalls => ToolCalls != null && ToolCalls.Count > 0;

    /// <summary>
    /// 结束原因（stop / tool_calls / length 等）
    /// </summary>
    public string? FinishReason { get; set; }
}
