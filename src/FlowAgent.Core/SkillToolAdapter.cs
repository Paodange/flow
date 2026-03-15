using FlowAgent.Core.Skills;
using FlowAgent.Core.Tools;

namespace FlowAgent.Core;

/// <summary>
/// 技能适配器 - 将 ISkill 包装为 ITool，使技能可以像工具一样被 LLM 调用
/// </summary>
internal sealed class SkillToolAdapter : ITool
{
    private readonly ISkill _skill;
    private readonly Agent _agent;

    internal SkillToolAdapter(ISkill skill, Agent agent)
    {
        _skill = skill;
        _agent = agent;
    }

    public string Name => _skill.Name;

    public string Description => _skill.Description;

    public string ParametersSchema => _skill.ParametersSchema;

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
        => _skill.ExecuteAsync(arguments, _agent.GetActiveTools(), cancellationToken);
}
