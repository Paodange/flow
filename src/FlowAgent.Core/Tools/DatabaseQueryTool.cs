using System.Text;
using System.Text.Json;

namespace FlowAgent.Core.Tools;

/// <summary>
/// 内存数据库查询工具 - 提供简单的内存表操作，支持创建表、插入、查询和删除数据
/// </summary>
public class DatabaseQueryTool : ITool
{
    // 内存表存储：表名 → 行列表（每行是列名→值的字典）
    private readonly Dictionary<string, List<Dictionary<string, string>>> _tables = new();

    public string Name => "database_query";

    public string Description => "操作内存数据库，支持创建表、插入数据、查询数据、删除数据和列出所有表。";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""operation"": {
                ""type"": ""string"",
                ""enum"": [""create_table"", ""insert"", ""select"", ""delete"", ""list_tables"", ""drop_table""],
                ""description"": ""操作类型：create_table-创建表，insert-插入数据，select-查询数据，delete-删除数据，list_tables-列出所有表，drop_table-删除表""
            },
            ""table"": {
                ""type"": ""string"",
                ""description"": ""表名""
            },
            ""columns"": {
                ""type"": ""array"",
                ""items"": {""type"": ""string""},
                ""description"": ""列名列表（create_table 操作时使用）""
            },
            ""row"": {
                ""type"": ""object"",
                ""description"": ""要插入的行数据（列名→值的键值对）""
            },
            ""where"": {
                ""type"": ""object"",
                ""description"": ""过滤条件（列名→值的键值对，select/delete 操作时使用，留空则匹配所有行）""
            }
        },
        ""required"": [""operation""]
    }";

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            var operation = root.TryGetProperty("operation", out var opEl) ? opEl.GetString() ?? "" : "";
            var table = root.TryGetProperty("table", out var tableEl) ? tableEl.GetString() ?? "" : "";

            return operation switch
            {
                "list_tables" => Task.FromResult(ListTables()),
                "create_table" => Task.FromResult(CreateTable(root, table)),
                "insert" => Task.FromResult(Insert(root, table)),
                "select" => Task.FromResult(Select(root, table)),
                "delete" => Task.FromResult(Delete(root, table)),
                "drop_table" => Task.FromResult(DropTable(table)),
                _ => Task.FromResult($"错误: 不支持的操作 '{operation}'")
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult($"错误: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 私有方法
    // ──────────────────────────────────────────────────────────────────────────

    private string ListTables()
    {
        if (_tables.Count == 0)
            return "数据库中没有表";

        var sb = new StringBuilder("数据库中的表:\n");
        foreach (var (name, rows) in _tables)
            sb.AppendLine($"  - {name}（{rows.Count} 行）");

        return sb.ToString().TrimEnd();
    }

    private string CreateTable(JsonElement root, string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return "错误: 表名不能为空";

        if (_tables.ContainsKey(table))
            return $"错误: 表 '{table}' 已存在";

        // 读取列名（只作为元数据记录，不强制约束）
        var columns = new List<string>();
        if (root.TryGetProperty("columns", out var colsEl) &&
            colsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var col in colsEl.EnumerateArray())
                columns.Add(col.GetString() ?? "");
        }

        _tables[table] = new List<Dictionary<string, string>>();
        var colDesc = columns.Count > 0 ? string.Join(", ", columns) : "（无预定义列）";
        return $"已创建表 '{table}'，列: {colDesc}";
    }

    private string Insert(JsonElement root, string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return "错误: 表名不能为空";

        if (!_tables.TryGetValue(table, out var rows))
            return $"错误: 表 '{table}' 不存在";

        if (!root.TryGetProperty("row", out var rowEl) ||
            rowEl.ValueKind != JsonValueKind.Object)
            return "错误: 缺少 row 参数";

        var row = new Dictionary<string, string>();
        foreach (var prop in rowEl.EnumerateObject())
            row[prop.Name] = prop.Value.ToString();

        rows.Add(row);
        return $"已向表 '{table}' 插入 1 行数据";
    }

    private string Select(JsonElement root, string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return "错误: 表名不能为空";

        if (!_tables.TryGetValue(table, out var rows))
            return $"错误: 表 '{table}' 不存在";

        var where = ParseWhere(root);
        var matched = FilterRows(rows, where);

        if (matched.Count == 0)
            return $"表 '{table}' 中没有匹配的数据";

        var sb = new StringBuilder($"表 '{table}' 查询结果（{matched.Count} 行）:\n");
        for (int i = 0; i < matched.Count; i++)
        {
            sb.Append($"  [{i + 1}] ");
            sb.AppendLine(string.Join(", ", matched[i].Select(kv => $"{kv.Key}={kv.Value}")));
        }

        return sb.ToString().TrimEnd();
    }

    private string Delete(JsonElement root, string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return "错误: 表名不能为空";

        if (!_tables.TryGetValue(table, out var rows))
            return $"错误: 表 '{table}' 不存在";

        var where = ParseWhere(root);
        var toDelete = FilterRows(rows, where);

        foreach (var row in toDelete)
            rows.Remove(row);

        return $"已从表 '{table}' 删除 {toDelete.Count} 行数据";
    }

    private string DropTable(string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return "错误: 表名不能为空";

        if (!_tables.Remove(table))
            return $"错误: 表 '{table}' 不存在";

        return $"已删除表 '{table}'";
    }

    private static Dictionary<string, string>? ParseWhere(JsonElement root)
    {
        if (!root.TryGetProperty("where", out var whereEl) ||
            whereEl.ValueKind != JsonValueKind.Object)
            return null;

        var where = new Dictionary<string, string>();
        foreach (var prop in whereEl.EnumerateObject())
            where[prop.Name] = prop.Value.ToString();

        return where.Count > 0 ? where : null;
    }

    private static List<Dictionary<string, string>> FilterRows(
        List<Dictionary<string, string>> rows,
        Dictionary<string, string>? where)
    {
        if (where == null || where.Count == 0)
            return new List<Dictionary<string, string>>(rows);

        return rows.Where(row =>
            where.All(kv =>
                row.TryGetValue(kv.Key, out var val) &&
                string.Equals(val, kv.Value, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
