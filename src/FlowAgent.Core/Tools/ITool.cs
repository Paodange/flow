namespace FlowAgent.Core.Tools;

/// <summary>
/// 工具接口 - 定义智能体可以调用的工具
/// </summary>
public interface ITool
{
    /// <summary>
    /// 工具名称（必须唯一）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 工具描述（告诉智能体这个工具的用途）
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 工具参数的JSON Schema
    /// 描述工具需要什么参数
    /// </summary>
    string ParametersSchema { get; }

    /// <summary>
    /// 执行工具
    /// </summary>
    /// <param name="arguments">工具参数（JSON格式）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工具执行结果</returns>
    Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default);
}
