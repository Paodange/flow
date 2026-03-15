using FlowAgent.Core.Tools;

namespace FlowAgent.Core.Plugins;

/// <summary>
/// 描述单个已加载插件的信息
/// </summary>
public sealed class PluginInfo
{
    /// <summary>插件 DLL 的完整路径</summary>
    public string FilePath { get; }

    /// <summary>从该插件发现的工具实例列表（只读）</summary>
    public IReadOnlyList<ITool> Tools { get; }

    /// <summary>加载时间</summary>
    public DateTime LoadedAt { get; } = DateTime.UtcNow;

    internal ToolAssemblyLoadContext? LoadContext { get; }

    internal PluginInfo(string filePath, IReadOnlyList<ITool> tools, ToolAssemblyLoadContext? loadContext)
    {
        FilePath = filePath;
        Tools = tools;
        LoadContext = loadContext;
    }
}
