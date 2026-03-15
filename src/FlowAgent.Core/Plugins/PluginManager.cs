using System.Reflection;
using FlowAgent.Core.Tools;

namespace FlowAgent.Core.Plugins;

/// <summary>
/// 插件管理器 - 负责自动发现插件目录中的工具插件、热加载（无需重启）以及生命周期管理。
/// <para>
/// 使用方式：
/// <code>
/// var manager = new PluginManager("./plugins");
/// manager.ToolDiscovered += (plugin, tool) => agent.RegisterTool(tool);
/// manager.ToolRemoved    += (plugin, tool) => agent.UnregisterTool(tool.Name);
/// await manager.StartAsync();          // 扫描已有插件并开始监视目录
/// // ... 将 *.dll 放入 ./plugins 目录即可自动加载，无需重启
/// manager.Dispose();                   // 停止监视并卸载所有插件
/// </code>
/// </para>
/// </summary>
public sealed class PluginManager : IDisposable
{
    private readonly string _pluginDirectory;
    private readonly Dictionary<string, PluginInfo> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    // ── 事件 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 发现新工具时触发（插件加载后每个工具触发一次）。
    /// </summary>
    public event Action<PluginInfo, ITool>? ToolDiscovered;

    /// <summary>
    /// 工具被移除时触发（插件卸载时每个工具触发一次）。
    /// </summary>
    public event Action<PluginInfo, ITool>? ToolRemoved;

    /// <summary>
    /// 插件加载失败时触发。
    /// </summary>
    public event Action<string, Exception>? PluginLoadFailed;

    // ── 属性 ─────────────────────────────────────────────────────────────────

    /// <summary>当前已加载的所有插件信息（只读快照）</summary>
    public IReadOnlyList<PluginInfo> LoadedPlugins
    {
        get
        {
            lock (_lock)
            {
                return _loadedPlugins.Values.ToList();
            }
        }
    }

    /// <summary>插件目录路径</summary>
    public string PluginDirectory => _pluginDirectory;

    // ── 构造函数 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 创建插件管理器
    /// </summary>
    /// <param name="pluginDirectory">
    ///   插件目录路径。管理器会扫描此目录中以 <c>*.Plugin.dll</c>、<c>*.Tools.dll</c>
    ///   或 <c>*Tool.dll</c> 结尾的 DLL（也可通过 <paramref name="searchPattern"/> 自定义）。
    /// </param>
    /// <param name="searchPattern">
    ///   DLL 搜索通配符，默认 <c>*.dll</c>（即目录下所有 DLL 都视为候选插件）。
    /// </param>
    public PluginManager(string pluginDirectory, string searchPattern = "*.dll")
    {
        _pluginDirectory = Path.GetFullPath(pluginDirectory);
        _searchPattern = searchPattern;
    }

    private readonly string _searchPattern;

    /// <summary>
    /// 文件系统事件触发后等待文件写入完成的延迟（毫秒）。
    /// </summary>
    private const int FileWriteSettleDelayMs = 500;

    // ── 公开方法 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 扫描插件目录中已有的 DLL 并开始监视目录（热加载）。
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Directory.Exists(_pluginDirectory))
        {
            Directory.CreateDirectory(_pluginDirectory);
            Console.WriteLine($"📁 插件目录已创建: {_pluginDirectory}");
        }

        // 扫描已有插件
        await ScanDirectoryAsync(cancellationToken);

        // 启动目录监视器
        StartWatcher();

        Console.WriteLine($"👁️  插件管理器已启动，正在监视: {_pluginDirectory}");
    }

    /// <summary>
    /// 手动扫描插件目录（无需调用 <see cref="StartAsync"/> 也可单独使用）。
    /// </summary>
    public async Task ScanDirectoryAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Directory.Exists(_pluginDirectory))
            return;

        var dllFiles = Directory.GetFiles(_pluginDirectory, _searchPattern, SearchOption.TopDirectoryOnly);
        foreach (var dllPath in dllFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LoadPluginAsync(dllPath, cancellationToken);
        }
    }

    /// <summary>
    /// 手动加载指定路径的插件 DLL。
    /// </summary>
    public Task LoadPluginAsync(string dllPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        dllPath = Path.GetFullPath(dllPath);

        lock (_lock)
        {
            if (_loadedPlugins.ContainsKey(dllPath))
            {
                Console.WriteLine($"⏭️  插件已加载，跳过: {Path.GetFileName(dllPath)}");
                return Task.CompletedTask;
            }
        }

        return Task.Run(() => LoadPluginInternal(dllPath), cancellationToken);
    }

    /// <summary>
    /// 卸载指定路径的插件 DLL（会触发 <see cref="ToolRemoved"/> 事件）。
    /// </summary>
    public void UnloadPlugin(string dllPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        dllPath = Path.GetFullPath(dllPath);

        PluginInfo? plugin;
        lock (_lock)
        {
            if (!_loadedPlugins.TryGetValue(dllPath, out plugin))
                return;

            _loadedPlugins.Remove(dllPath);
        }

        NotifyToolsRemoved(plugin);
        plugin.LoadContext?.Unload();
        Console.WriteLine($"🗑️  插件已卸载: {Path.GetFileName(dllPath)}");
    }

    /// <summary>
    /// 重新加载指定路径的插件（先卸载旧版本，再加载新版本）。
    /// </summary>
    public async Task ReloadPluginAsync(string dllPath, CancellationToken cancellationToken = default)
    {
        UnloadPlugin(dllPath);
        // 等待一段时间确保文件写入完成
        await Task.Delay(FileWriteSettleDelayMs, cancellationToken);
        await LoadPluginAsync(dllPath, cancellationToken);
    }

    /// <summary>
    /// 停止目录监视并卸载所有插件。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _watcher?.Dispose();
        _watcher = null;

        List<PluginInfo> plugins;
        lock (_lock)
        {
            plugins = _loadedPlugins.Values.ToList();
            _loadedPlugins.Clear();
        }

        foreach (var plugin in plugins)
        {
            NotifyToolsRemoved(plugin);
            plugin.LoadContext?.Unload();
        }

        Console.WriteLine("🛑 插件管理器已停止，所有插件已卸载。");
    }

    // ── 私有方法 ─────────────────────────────────────────────────────────────

    private void StartWatcher()
    {
        _watcher = new FileSystemWatcher(_pluginDirectory, _searchPattern)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        _watcher.Created += OnFileCreated;
        _watcher.Changed += OnFileChanged;
        _watcher.Deleted += OnFileDeleted;
    }

    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        // 等待文件写入完成
        await Task.Delay(FileWriteSettleDelayMs);
        try
        {
            await LoadPluginAsync(e.FullPath);
        }
        catch (Exception ex)
        {
            PluginLoadFailed?.Invoke(e.FullPath, ex);
            Console.WriteLine($"❌ 热加载插件时发生未处理异常 {Path.GetFileName(e.FullPath)}: {ex.Message}");
        }
    }

    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        await Task.Delay(FileWriteSettleDelayMs);
        try
        {
            await ReloadPluginAsync(e.FullPath);
        }
        catch (Exception ex)
        {
            PluginLoadFailed?.Invoke(e.FullPath, ex);
            Console.WriteLine($"❌ 热重载插件时发生未处理异常 {Path.GetFileName(e.FullPath)}: {ex.Message}");
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        UnloadPlugin(e.FullPath);
    }

    private void LoadPluginInternal(string dllPath)
    {
        try
        {
            Console.WriteLine($"📦 正在加载插件: {Path.GetFileName(dllPath)}");

            var loadContext = new ToolAssemblyLoadContext(dllPath);
            var assembly = loadContext.LoadFromAssemblyPath(dllPath);

            var tools = DiscoverTools(assembly);

            if (tools.Count == 0)
            {
                Console.WriteLine($"⚠️  插件中未找到 ITool 实现，跳过: {Path.GetFileName(dllPath)}");
                loadContext.Unload();
                return;
            }

            var pluginInfo = new PluginInfo(dllPath, tools, loadContext);

            lock (_lock)
            {
                _loadedPlugins[dllPath] = pluginInfo;
            }

            Console.WriteLine($"✅ 插件加载成功: {Path.GetFileName(dllPath)}（发现 {tools.Count} 个工具）");

            // 逐个触发事件
            foreach (var tool in tools)
            {
                Console.WriteLine($"   🔌 发现工具: {tool.Name} - {tool.Description}");
                ToolDiscovered?.Invoke(pluginInfo, tool);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 插件加载失败: {Path.GetFileName(dllPath)} - {ex.Message}");
            PluginLoadFailed?.Invoke(dllPath, ex);
        }
    }

    /// <summary>使用反射从程序集中找到所有公开的、可实例化的 ITool 实现</summary>
    private static List<ITool> DiscoverTools(Assembly assembly)
    {
        var toolInterfaceType = typeof(ITool);
        var tools = new List<ITool>();

        foreach (var type in assembly.GetExportedTypes())
        {
            if (!type.IsClass || type.IsAbstract)
                continue;

            if (!toolInterfaceType.IsAssignableFrom(type))
                continue;

            try
            {
                if (Activator.CreateInstance(type) is ITool tool)
                {
                    tools.Add(tool);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  无法实例化工具类型 {type.FullName}: {ex.Message}");
            }
        }

        return tools;
    }

    private void NotifyToolsRemoved(PluginInfo plugin)
    {
        foreach (var tool in plugin.Tools)
        {
            ToolRemoved?.Invoke(plugin, tool);
        }
    }
}
