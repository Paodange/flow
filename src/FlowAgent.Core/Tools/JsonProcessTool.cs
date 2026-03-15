using System.Text.Json;
using System.Text.Json.Nodes;

namespace FlowAgent.Core.Tools;

/// <summary>
/// JSON 处理工具 - 提供 JSON 解析、格式化、查询和转换功能
/// </summary>
public class JsonProcessTool : ITool
{
    /// <summary>为 to_table 操作，扫描列名时最多检查的行数</summary>
    private const int MaxRowsForKeyExtraction = 50;

    /// <summary>为 to_table 操作，实际渲染输出的最大行数</summary>
    private const int MaxDisplayedRows = 20;
    public string Name => "json_process";

    public string Description => "处理 JSON 数据，支持格式化、压缩、解析字段值、验证合法性及转换为其他格式。";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""operation"": {
                ""type"": ""string"",
                ""enum"": [""format"", ""minify"", ""get"", ""set"", ""validate"", ""keys"", ""to_table""],
                ""description"": ""操作类型：format-格式化美化，minify-压缩，get-按路径读取字段，set-按路径设置字段，validate-验证是否合法 JSON，keys-列出顶层键名，to_table-转换为可读表格""
            },
            ""json"": {
                ""type"": ""string"",
                ""description"": ""要处理的 JSON 字符串""
            },
            ""path"": {
                ""type"": ""string"",
                ""description"": ""字段路径，使用点号分隔（如 user.name）或数组索引（如 items.0.title）""
            },
            ""value"": {
                ""type"": ""string"",
                ""description"": ""set 操作时要设置的值（字符串形式）""
            }
        },
        ""required"": [""operation"", ""json""]
    }";

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var args = JsonSerializer.Deserialize<JsonProcessArgs>(arguments, options);
            if (args == null)
                return Task.FromResult("错误: 无法解析参数");
            if (string.IsNullOrWhiteSpace(args.Json))
                return Task.FromResult("错误: json 参数不能为空");

            return args.Operation switch
            {
                "format"   => Task.FromResult(FormatJson(args.Json)),
                "minify"   => Task.FromResult(MinifyJson(args.Json)),
                "get"      => Task.FromResult(GetJsonValue(args.Json, args.Path)),
                "set"      => Task.FromResult(SetJsonValue(args.Json, args.Path, args.Value)),
                "validate" => Task.FromResult(ValidateJson(args.Json)),
                "keys"     => Task.FromResult(GetJsonKeys(args.Json)),
                "to_table" => Task.FromResult(JsonToTable(args.Json)),
                _          => Task.FromResult($"错误: 不支持的操作 '{args.Operation}'")
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult($"错误: {ex.Message}");
        }
    }

    // ── 格式化（美化输出） ────────────────────────────────────────────────────

    private static string FormatJson(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            var formatted = node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            return formatted != null ? $"格式化结果:\n{formatted}" : "错误: 解析失败";
        }
        catch (JsonException ex)
        {
            return $"错误: JSON 格式不合法 - {ex.Message}";
        }
    }

    // ── 压缩（单行输出） ──────────────────────────────────────────────────────

    private static string MinifyJson(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            var minified = node?.ToJsonString();
            return minified != null ? $"压缩结果: {minified}" : "错误: 解析失败";
        }
        catch (JsonException ex)
        {
            return $"错误: JSON 格式不合法 - {ex.Message}";
        }
    }

    // ── 按路径获取字段值 ──────────────────────────────────────────────────────

    private static string GetJsonValue(string json, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "错误: path 参数不能为空";

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null)
                return "错误: JSON 解析失败";

            var current = node;
            var segments = path.Split('.');

            foreach (var segment in segments)
            {
                if (current == null)
                    return $"错误: 路径 '{path}' 不存在（在 '{segment}' 处中断）";

                if (int.TryParse(segment, out var index))
                {
                    if (current is JsonArray arr)
                    {
                        current = index >= 0 && index < arr.Count ? arr[index] : null;
                    }
                    else
                    {
                        current = current[segment];
                    }
                }
                else
                {
                    current = current[segment];
                }
            }

            if (current == null)
                return $"结果: null（路径 '{path}' 的值为 null 或不存在）";

            return $"路径 '{path}' 的值: {current.ToJsonString()}";
        }
        catch (JsonException ex)
        {
            return $"错误: JSON 格式不合法 - {ex.Message}";
        }
    }

    // ── 按路径设置字段值 ──────────────────────────────────────────────────────

    private static string SetJsonValue(string json, string? path, string? value)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "错误: path 参数不能为空";
        if (value == null)
            return "错误: value 参数不能为空";

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null)
                return "错误: JSON 解析失败";

            var segments = path.Split('.');
            var current = node;

            for (int i = 0; i < segments.Length - 1; i++)
            {
                var seg = segments[i];
                if (current == null)
                    return $"错误: 路径 '{path}' 不存在（在 '{seg}' 处中断）";
                current = int.TryParse(seg, out var idx) && current is JsonArray a
                    ? a[idx]
                    : current[seg];
            }

            var lastSeg = segments[^1];
            JsonNode? newValue;
            try
            {
                newValue = JsonNode.Parse(value);
            }
            catch
            {
                newValue = JsonValue.Create(value);
            }

            if (current is JsonObject obj)
                obj[lastSeg] = newValue;
            else if (current is JsonArray arr && int.TryParse(lastSeg, out var arrIdx))
                arr[arrIdx] = newValue;
            else
                return $"错误: 无法在路径 '{path}' 处设置值";

            return $"设置成功，更新后的 JSON:\n{node.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}";
        }
        catch (JsonException ex)
        {
            return $"错误: JSON 格式不合法 - {ex.Message}";
        }
    }

    // ── 验证 JSON 合法性 ──────────────────────────────────────────────────────

    private static string ValidateJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return $"验证结果: ✅ 合法的 JSON（类型: {doc.RootElement.ValueKind}）";
        }
        catch (JsonException ex)
        {
            return $"验证结果: ❌ 不合法的 JSON - {ex.Message}";
        }
    }

    // ── 列出顶层键名 ──────────────────────────────────────────────────────────

    private static string GetJsonKeys(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
                return $"顶层键名（共 {keys.Count} 个）: {string.Join(", ", keys)}";
            }
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return $"根元素是数组，共 {doc.RootElement.GetArrayLength()} 个元素。" +
                       "请先用 get 操作取出某个对象元素再列键名。";
            }
            return $"根元素类型 {doc.RootElement.ValueKind} 不支持列出键名";
        }
        catch (JsonException ex)
        {
            return $"错误: JSON 格式不合法 - {ex.Message}";
        }
    }

    // ── 转换为可读表格 ────────────────────────────────────────────────────────

    private static string JsonToTable(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 对象数组 → 表格
            if (root.ValueKind == JsonValueKind.Array)
            {
                var items = root.EnumerateArray().ToList();
                if (items.Count == 0)
                    return "数组为空，无法生成表格";

                // 收集所有键名（取前 MaxRowsForKeyExtraction 行）
                var allKeys = items
                    .Take(MaxRowsForKeyExtraction)
                    .Where(e => e.ValueKind == JsonValueKind.Object)
                    .SelectMany(e => e.EnumerateObject().Select(p => p.Name))
                    .Distinct()
                    .ToList();

                if (allKeys.Count == 0)
                    return $"数组包含 {items.Count} 个基本值: {string.Join(", ", items.Take(MaxDisplayedRows).Select(e => e.ToString()))}";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"表格（共 {items.Count} 行）:");
                sb.AppendLine(string.Join(" | ", allKeys));
                sb.AppendLine(new string('-', allKeys.Sum(k => k.Length + 3)));

                foreach (var item in items.Take(MaxDisplayedRows))
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;
                    var row = allKeys.Select(k =>
                    {
                        if (item.TryGetProperty(k, out var v))
                            return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString();
                        return "";
                    });
                    sb.AppendLine(string.Join(" | ", row));
                }

                if (items.Count > MaxDisplayedRows)
                    sb.AppendLine($"... 仅显示前 {MaxDisplayedRows} 行，共 {items.Count} 行");

                return sb.ToString().TrimEnd();
            }

            // 对象 → 键值对列表
            if (root.ValueKind == JsonValueKind.Object)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("键值对列表:");
                foreach (var prop in root.EnumerateObject())
                {
                    var val = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? ""
                        : prop.Value.ToString();
                    sb.AppendLine($"  {prop.Name}: {val}");
                }
                return sb.ToString().TrimEnd();
            }

            return $"根元素类型 {root.ValueKind}，值: {root}";
        }
        catch (JsonException ex)
        {
            return $"错误: JSON 格式不合法 - {ex.Message}";
        }
    }

    private class JsonProcessArgs
    {
        public string Operation { get; set; } = string.Empty;
        public string Json { get; set; } = string.Empty;
        public string? Path { get; set; }
        public string? Value { get; set; }
    }
}
