using FlowAgent.Core.Tools;

namespace FlowAgent.Core.Skills;

/// <summary>
/// 技能接口 - 定义可以编排多个工具、执行多步骤任务的复合能力
/// </summary>
/// <remarks>
/// 技能（Skill）与工具（Tool）的区别：
/// <list type="bullet">
///   <item><description>工具是单一的原子操作（如计算、文件读写）</description></item>
///   <item><description>技能是多步骤的复合能力，可以内部调用多个工具来完成复杂任务</description></item>
/// </list>
/// 注册到 <see cref="Agent"/> 后，技能会以工具的形式暴露给 LLM，LLM 可以通过工具调用触发技能执行。
/// </remarks>
public interface ISkill
{
    /// <summary>
    /// 技能名称（必须唯一，不能与已注册的工具重名）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 技能描述（告诉 LLM 这个技能的用途，应尽量清晰具体）
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 技能参数的 JSON Schema（与工具参数格式一致）
    /// </summary>
    string ParametersSchema { get; }

    /// <summary>
    /// 执行技能
    /// </summary>
    /// <param name="arguments">技能参数（JSON 格式）</param>
    /// <param name="tools">当前智能体中处于激活状态的工具字典，技能可以调用这些工具</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>技能执行结果（字符串）</returns>
    Task<string> ExecuteAsync(
        string arguments,
        IReadOnlyDictionary<string, ITool> tools,
        CancellationToken cancellationToken = default);
}
