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
                ""enum"": [""read"", ""write"", ""append"", ""delete"", ""list_dir"", ""exists"", ""mkdir"", ""move"", ""copy"", ""info""],
                ""description"": ""操作类型：read-读取文件，write-写入文件，append-追加内容，delete-删除文件，list_dir-列出目录，exists-检查存在，mkdir-创建目录，move-移动/重命名，copy-复制文件，info-文件信息""
            },
            ""path"": {
                ""type"": ""string"",
                ""description"": ""文件或目录的相对路径""
            },
            ""content"": {
                ""type"": ""string"",
                ""description"": ""写入或追加的内容（write/append 操作时使用）""
            },
            ""destination"": {
                ""type"": ""string"",
                ""description"": ""目标路径（move/copy 操作时使用）""
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

            // Resolve destination path for move/copy operations
            string? destPath = null;
            if (!string.IsNullOrEmpty(args.Destination))
            {
                destPath = ResolveSafePath(args.Destination);
                if (destPath == null)
                    return $"错误: 目标路径 '{args.Destination}' 超出允许的根目录范围";
            }

            return args.Operation switch
            {
                "read" => await ReadFileAsync(fullPath, cancellationToken),
                "write" => await WriteFileAsync(fullPath, args.Content ?? string.Empty, cancellationToken),
                "append" => await AppendFileAsync(fullPath, args.Content ?? string.Empty, cancellationToken),
                "delete" => DeleteFile(fullPath),
                "list_dir" => ListDirectory(fullPath),
                "exists" => CheckExists(fullPath),
                "mkdir" => CreateDirectory(fullPath),
                "move" => destPath != null ? MoveFileOrDirectory(fullPath, destPath) : "错误: move 操作需要提供 destination 参数",
                "copy" => destPath != null ? CopyFile(fullPath, destPath) : "错误: copy 操作需要提供 destination 参数",
                "info" => GetFileInfo(fullPath),
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

    private static string CreateDirectory(string path)
    {
        if (Directory.Exists(path))
            return $"目录已存在: '{path}'";

        Directory.CreateDirectory(path);
        return $"已成功创建目录: {path}";
    }

    private static string MoveFileOrDirectory(string source, string destination)
    {
        if (File.Exists(source))
        {
            if (File.Exists(destination))
                return $"错误: 目标文件已存在 '{destination}'";

            var destDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);
            File.Move(source, destination, overwrite: false);
            return $"已成功移动文件: {source} → {destination}";
        }

        if (Directory.Exists(source))
        {
            if (Directory.Exists(destination))
                return $"错误: 目标目录已存在 '{destination}'";

            Directory.Move(source, destination);
            return $"已成功移动目录: {source} → {destination}";
        }

        return $"错误: 源路径 '{source}' 不存在";
    }

    private static string CopyFile(string source, string destination)
    {
        if (!File.Exists(source))
            return $"错误: 文件 '{source}' 不存在";

        if (File.Exists(destination))
            return $"错误: 目标文件已存在 '{destination}'";

        var destDir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        File.Copy(source, destination, overwrite: false);
        return $"已成功复制文件: {source} → {destination}";
    }

    private static string GetFileInfo(string path)
    {
        if (File.Exists(path))
        {
            var info = new FileInfo(path);
            return $"文件信息 '{path}':\n" +
                   $"  大小: {info.Length} 字节\n" +
                   $"  创建时间: {info.CreationTime:yyyy-MM-dd HH:mm:ss}\n" +
                   $"  最后修改: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}\n" +
                   $"  只读: {info.IsReadOnly}";
        }

        if (Directory.Exists(path))
        {
            var info = new DirectoryInfo(path);
            var fileCount = info.GetFiles().Length;
            var dirCount = info.GetDirectories().Length;
            return $"目录信息 '{path}':\n" +
                   $"  文件数: {fileCount}\n" +
                   $"  子目录数: {dirCount}\n" +
                   $"  创建时间: {info.CreationTime:yyyy-MM-dd HH:mm:ss}\n" +
                   $"  最后修改: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
        }

        return $"不存在: '{path}'";
    }

    private class FileArgs
    {
        public string Operation { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? Destination { get; set; }
    }
}
