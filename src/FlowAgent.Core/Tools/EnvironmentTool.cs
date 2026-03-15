using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace FlowAgent.Core.Tools;

/// <summary>
/// 环境与系统信息工具 - 获取环境变量、系统信息、工作目录等
/// </summary>
public class EnvironmentTool : ITool
{
    /// <summary>列出环境变量时，单个值的最大显示字符数</summary>
    private const int MaxEnvironmentValueLength = 200;
    public string Name => "environment";

    public string Description => "获取系统环境信息，包括环境变量、操作系统信息、当前目录、机器名等。";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""action"": {
                ""type"": ""string"",
                ""enum"": [""get_env"", ""list_env"", ""system_info"", ""current_dir"", ""machine_name"", ""username""],
                ""description"": ""操作类型：get_env-获取指定环境变量，list_env-列出所有环境变量，system_info-获取系统信息，current_dir-当前工作目录，machine_name-主机名，username-当前用户名""
            },
            ""name"": {
                ""type"": ""string"",
                ""description"": ""环境变量名称（仅 get_env 操作需要）""
            }
        },
        ""required"": [""action""]
    }";

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var args = JsonSerializer.Deserialize<EnvironmentArgs>(arguments, options);
            if (args == null)
                return Task.FromResult("错误: 无法解析参数");

            var result = args.Action switch
            {
                "get_env"      => GetEnvironmentVariable(args.Name),
                "list_env"     => ListEnvironmentVariables(),
                "system_info"  => GetSystemInfo(),
                "current_dir"  => $"当前工作目录: {Directory.GetCurrentDirectory()}",
                "machine_name" => $"主机名: {Environment.MachineName}",
                "username"     => $"当前用户名: {Environment.UserName}",
                _              => $"错误: 不支持的操作 '{args.Action}'"
            };

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult($"错误: {ex.Message}");
        }
    }

    // ── 获取指定环境变量 ──────────────────────────────────────────────────────

    private static string GetEnvironmentVariable(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "错误: name 参数不能为空（请指定环境变量名称）";

        var value = Environment.GetEnvironmentVariable(name);
        return value != null
            ? $"环境变量 {name} = {value}"
            : $"环境变量 '{name}' 不存在或未设置";
    }

    // ── 列出所有环境变量 ──────────────────────────────────────────────────────

    private static string ListEnvironmentVariables()
    {
        var envVars = Environment.GetEnvironmentVariables();
        var sb = new StringBuilder();
        sb.AppendLine($"环境变量列表（共 {envVars.Count} 个）:");

        var sorted = envVars.Keys
            .Cast<string>()
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 过滤掉可能包含敏感信息的变量（如包含 KEY、SECRET、TOKEN、PASSWORD 的变量）
        foreach (var key in sorted)
        {
            var upperKey = key.ToUpperInvariant();
            if (upperKey.Contains("KEY") || upperKey.Contains("SECRET") ||
                upperKey.Contains("TOKEN") || upperKey.Contains("PASSWORD") ||
                upperKey.Contains("PASSWD") || upperKey.Contains("CREDENTIAL"))
            {
                sb.AppendLine($"  {key} = [已隐藏]");
            }
            else
            {
                var val = Environment.GetEnvironmentVariable(key) ?? "";
                // 截断过长的值
                if (val.Length > MaxEnvironmentValueLength)
                    val = val[..MaxEnvironmentValueLength] + "...";
                sb.AppendLine($"  {key} = {val}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    // ── 获取系统信息 ──────────────────────────────────────────────────────────

    private static string GetSystemInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine("系统信息:");
        sb.AppendLine($"  操作系统: {Environment.OSVersion}");
        sb.AppendLine($"  OS 平台: {(OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : OperatingSystem.IsMacOS() ? "macOS" : "Unknown")}");
        sb.AppendLine($"  处理器架构: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"  CPU 核心数: {Environment.ProcessorCount}");
        sb.AppendLine($"  .NET 运行时: {Environment.Version}");
        sb.AppendLine($"  机器名: {Environment.MachineName}");
        sb.AppendLine($"  用户名: {Environment.UserName}");
        sb.AppendLine($"  用户域: {Environment.UserDomainName}");
        sb.AppendLine($"  系统目录: {Environment.SystemDirectory}");
        sb.AppendLine($"  工作目录: {Directory.GetCurrentDirectory()}");

        // 内存信息（仅 GC 统计，不依赖外部库）
        var gcMemory = GC.GetTotalMemory(false);
        sb.AppendLine($"  进程 GC 内存: {gcMemory / 1024 / 1024:F1} MB");
        sb.AppendLine($"  系统页面大小: {Environment.SystemPageSize} 字节");

        return sb.ToString().TrimEnd();
    }

    private class EnvironmentArgs
    {
        public string Action { get; set; } = string.Empty;
        public string? Name { get; set; }
    }
}
