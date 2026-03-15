using FlowAgent.Core.Tools;

namespace FlowAgent.Core.Skills;

/// <summary>
/// 技能基类 - 提供工具调用辅助方法，方便派生类实现多步骤逻辑
/// </summary>
public abstract class SkillBase : ISkill
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract string Description { get; }

    /// <inheritdoc/>
    public abstract string ParametersSchema { get; }

    /// <inheritdoc/>
    public abstract Task<string> ExecuteAsync(
        string arguments,
        IReadOnlyDictionary<string, ITool> tools,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 调用已注册工具的辅助方法。若工具不存在则返回错误信息。
    /// </summary>
    /// <param name="tools">工具字典</param>
    /// <param name="toolName">工具名称</param>
    /// <param name="arguments">工具参数（JSON 格式）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工具执行结果</returns>
    protected static async Task<string> InvokeToolAsync(
        IReadOnlyDictionary<string, ITool> tools,
        string toolName,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        if (!tools.TryGetValue(toolName, out var tool))
            return $"错误: 技能依赖的工具 '{toolName}' 未注册或已禁用";

        return await tool.ExecuteAsync(arguments, cancellationToken);
    }

    /// <summary>
    /// 检查所需工具是否全部可用，并返回缺失工具列表。
    /// </summary>
    /// <param name="tools">工具字典</param>
    /// <param name="requiredTools">所需工具名称列表</param>
    /// <returns>缺失的工具名称列表（为空表示全部可用）</returns>
    protected static IReadOnlyList<string> CheckRequiredTools(
        IReadOnlyDictionary<string, ITool> tools,
        params string[] requiredTools)
    {
        return requiredTools.Where(t => !tools.ContainsKey(t)).ToList();
    }
}
