using System.Text.Json;

namespace FlowAgent.Core.Tools;

/// <summary>
/// 文件操作工具 - 支持文件的读、写、追加、删除、列目录及存在检查
/// </summary>
public class FileOperationTool : ITool
{
    private readonly string _baseDirectory;

    /// <summary>
    /// 创建文件操作工具实例
    /// </summary>
    /// <param name="baseDirectory">
    ///   允许操作的根目录。所有路径均相对于此目录解析，以防止目录遍历攻击。
    ///   若为 null，则使用当前工作目录。
    /// </param>
    public FileOperationTool(string? baseDirectory = null)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory ?? Directory.GetCurrentDirectory());
    }

    public string Name => "file_operation";

    public string Description => "执行文件操作，包括读取、写入、追加、删除文件，以及列出目录内容和检查文件是否存在。";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""operation"": {
                ""type"": ""string"",
                ""enum"": [""read"", ""write"", ""append"", ""delete"", ""list_dir"", ""exists""],
                ""description"": ""操作类型：read-读取文件，write-写入文件，append-追加内容，delete-删除文件，list_dir-列出目录，exists-检查存在""
            },
            ""path"": {
                ""type"": ""string"",
                ""description"": ""文件或目录的相对路径""
            },
            ""content"": {
                ""type"": ""string"",
                ""description"": ""写入或追加的内容（write/append 操作时使用）""
            }
        },
        ""required"": [""operation"", ""path""]
    }";

    public async Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var args = JsonSerializer.Deserialize<FileArgs>(arguments, options);
            if (args == null)
                return "错误: 无法解析参数";

            var fullPath = ResolveSafePath(args.Path);
            if (fullPath == null)
                return $"错误: 路径 '{args.Path}' 超出允许的根目录范围";

            return args.Operation switch
            {
                "read" => await ReadFileAsync(fullPath, cancellationToken),
                "write" => await WriteFileAsync(fullPath, args.Content ?? string.Empty, cancellationToken),
                "append" => await AppendFileAsync(fullPath, args.Content ?? string.Empty, cancellationToken),
                "delete" => DeleteFile(fullPath),
                "list_dir" => ListDirectory(fullPath),
                "exists" => CheckExists(fullPath),
                _ => $"错误: 不支持的操作 '{args.Operation}'"
            };
        }
        catch (Exception ex)
        {
            return $"错误: {ex.Message}";
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 私有方法
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 解析路径并验证是否在允许的根目录内（防止目录遍历）
    /// </summary>
    private string? ResolveSafePath(string relativePath)
    {
        // 组合并规范化路径
        var combined = Path.Combine(_baseDirectory, relativePath);
        var fullPath = Path.GetFullPath(combined);

        // 确保路径位于根目录之内
        if (!fullPath.StartsWith(_baseDirectory, StringComparison.OrdinalIgnoreCase))
            return null;

        return fullPath;
    }

    private static async Task<string> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return $"错误: 文件 '{path}' 不存在";

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        return $"文件内容 ({path}):\n{content}";
    }

    private static async Task<string> WriteFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, content, cancellationToken);
        return $"已成功写入文件: {path}（{content.Length} 个字符）";
    }

    private static async Task<string> AppendFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await File.AppendAllTextAsync(path, content, cancellationToken);
        return $"已成功追加内容到文件: {path}（追加 {content.Length} 个字符）";
    }

    private static string DeleteFile(string path)
    {
        if (!File.Exists(path))
            return $"错误: 文件 '{path}' 不存在";

        File.Delete(path);
        return $"已成功删除文件: {path}";
    }

    private static string ListDirectory(string path)
    {
        if (!Directory.Exists(path))
            return $"错误: 目录 '{path}' 不存在";

        var entries = new List<string>();

        foreach (var dir in Directory.GetDirectories(path))
            entries.Add($"[目录] {Path.GetFileName(dir)}");

        foreach (var file in Directory.GetFiles(path))
            entries.Add($"[文件] {Path.GetFileName(file)}");

        if (entries.Count == 0)
            return $"目录 '{path}' 为空";

        return $"目录 '{path}' 内容:\n" + string.Join("\n", entries);
    }

    private static string CheckExists(string path)
    {
        if (File.Exists(path))
            return $"存在: '{path}' 是一个文件";

        if (Directory.Exists(path))
            return $"存在: '{path}' 是一个目录";

        return $"不存在: '{path}'";
    }

    private class FileArgs
    {
        public string Operation { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? Content { get; set; }
    }
}
