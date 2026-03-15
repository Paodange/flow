using System.Reflection;
using System.Runtime.Loader;

namespace FlowAgent.Core.Plugins;

/// <summary>
/// 插件程序集加载上下文，为每个插件 DLL 提供独立的加载环境，支持热卸载。
/// </summary>
internal sealed class ToolAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>
    /// 插件 DLL 的完整路径
    /// </summary>
    public string PluginPath { get; }

    public ToolAssemblyLoadContext(string pluginPath)
        : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: true)
    {
        PluginPath = pluginPath;
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 先尝试用本地依赖解析器找到程序集路径
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // 回退到默认上下文（避免重复加载共享程序集，如 FlowAgent.Core）
        return null;
    }

    /// <inheritdoc />
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
