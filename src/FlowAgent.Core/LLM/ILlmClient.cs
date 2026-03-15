using FlowAgent.Core.Models;
using FlowAgent.Core.Tools;

namespace FlowAgent.Core.LLM;

/// <summary>
/// LLM 客户端接口，定义与大语言模型交互的契约
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// 向 LLM 发送对话请求并获取回复
    /// </summary>
    /// <param name="messages">对话历史消息列表</param>
    /// <param name="tools">可供 LLM 选择调用的工具（可为空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>LLM 的回复（可能包含文本内容或工具调用请求）</returns>
    Task<LlmResponse> CompleteAsync(
        IReadOnlyList<Message> messages,
        IReadOnlyDictionary<string, ITool>? tools = null,
        CancellationToken cancellationToken = default);
}
