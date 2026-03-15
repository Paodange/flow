using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace FlowAgent.Core.Tools;

/// <summary>
/// SQLite 数据库工具 - 提供持久化的 SQLite 数据库操作，支持完整的 CRUD 以及自定义 SQL
/// </summary>
public class SqliteDatabaseTool : ITool, IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    /// <summary>
    /// 使用指定文件路径的 SQLite 数据库创建工具实例
    /// </summary>
    /// <param name="databasePath">SQLite 数据库文件路径。若为 null，则使用内存数据库（":memory:"）。</param>
    public SqliteDatabaseTool(string? databasePath = null)
    {
        var dataSource = string.IsNullOrWhiteSpace(databasePath) ? ":memory:" : databasePath;
        _connection = new SqliteConnection($"Data Source={dataSource}");
        _connection.Open();
    }

    public string Name => "sqlite_database";

    public string Description => "操作 SQLite 数据库，支持创建表、插入、查询、更新、删除数据，以及执行自定义 SQL 语句。";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""operation"": {
                ""type"": ""string"",
                ""enum"": [""execute_sql"", ""create_table"", ""insert"", ""select"", ""update"", ""delete"", ""drop_table"", ""list_tables""],
                ""description"": ""操作类型：execute_sql-执行自定义SQL，create_table-创建表，insert-插入数据，select-查询数据，update-更新数据，delete-删除数据，drop_table-删除表，list_tables-列出所有表""
            },
            ""sql"": {
                ""type"": ""string"",
                ""description"": ""自定义 SQL 语句（execute_sql 操作时使用）""
            },
            ""table"": {
                ""type"": ""string"",
                ""description"": ""表名""
            },
            ""columns"": {
                ""type"": ""array"",
                ""items"": {""type"": ""object""},
                ""description"": ""列定义列表（create_table 时使用），每项包含 name 和 type 字段，如 [{name:id,type:INTEGER PRIMARY KEY},{name:title,type:TEXT}]""
            },
            ""row"": {
                ""type"": ""object"",
                ""description"": ""要插入的行数据（列名→值的键值对）""
            },
            ""set"": {
                ""type"": ""object"",
                ""description"": ""要更新的字段（列名→新值的键值对，update 操作时使用）""
            },
            ""where"": {
                ""type"": ""object"",
                ""description"": ""过滤条件（列名→值的键值对，select/update/delete 操作时使用）""
            },
            ""limit"": {
                ""type"": ""integer"",
                ""description"": ""查询结果的最大行数（select 操作时使用，默认 100）""
            }
        },
        ""required"": [""operation""]
    }";

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            var operation = root.TryGetProperty("operation", out var opEl) ? opEl.GetString() ?? "" : "";
            var table = root.TryGetProperty("table", out var tableEl) ? tableEl.GetString() ?? "" : "";

            return operation switch
            {
                "execute_sql" => Task.FromResult(ExecuteSql(root)),
                "list_tables" => Task.FromResult(ListTables()),
                "create_table" => Task.FromResult(CreateTable(root, table)),
                "insert" => Task.FromResult(Insert(root, table)),
                "select" => Task.FromResult(Select(root, table)),
                "update" => Task.FromResult(Update(root, table)),
                "delete" => Task.FromResult(Delete(root, table)),
                "drop_table" => Task.FromResult(DropTable(table)),
                _ => Task.FromResult($"错误: 不支持的操作 '{operation}'")
            };
        }
        catch (SqliteException ex)
        {
            return Task.FromResult($"数据库错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"错误: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 私有方法
    // ──────────────────────────────────────────────────────────────────────────

    private string ExecuteSql(JsonElement root)
    {
        if (!root.TryGetProperty("sql", out var sqlEl))
            return "错误: 缺少 sql 参数";

        var sql = sqlEl.GetString();
        if (string.IsNullOrWhiteSpace(sql))
            return "错误: SQL 语句不能为空";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        // Detect if the SQL is a query (SELECT) or a modification
        var trimmed = sql.TrimStart().ToUpperInvariant();
        if (trimmed.StartsWith("SELECT"))
        {
            return ReadResults(cmd, $"SQL 查询结果");
        }
        else
        {
            var affected = cmd.ExecuteNonQuery();
            return $"SQL 执行成功，影响 {affected} 行";
        }
    }

    private string ListTables()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";

        var tables = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            tables.Add(reader.GetString(0));

        if (tables.Count == 0)
            return "数据库中没有表";

        var sb = new StringBuilder($"数据库中的表（共 {tables.Count} 个）:\n");
        foreach (var t in tables)
        {
            // Get row count for each table; escape embedded double-quotes in the name
            using var countCmd = _connection.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM \"{t.Replace("\"", "\"\"")}\"";
            var count = (long)(countCmd.ExecuteScalar() ?? 0L);
            sb.AppendLine($"  - {t}（{count} 行）");
        }

        return sb.ToString().TrimEnd();
    }

    private string CreateTable(JsonElement root, string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return "错误: 表名不能为空";

        if (!root.TryGetProperty("columns", out var colsEl) ||
            colsEl.ValueKind != JsonValueKind.Array)
            return "错误: 缺少 columns 参数（列定义数组）";

        var columnDefs = new List<string>();
        foreach (var col in colsEl.EnumerateArray())
        {
            string? name = null;
            string type = "TEXT";

            if (col.ValueKind == JsonValueKind.Object)
            {
                if (col.TryGetProperty("name", out var nameEl)) name = nameEl.GetString();
                if (col.TryGetProperty("type", out var typeEl)) type = typeEl.GetString() ?? "TEXT";
            }
            else if (col.ValueKind == JsonValueKind.String)
            {
                // Simple string format: just the column name, default to TEXT
                name = col.GetString();
            }

            if (!string.IsNullOrWhiteSpace(name))
                columnDefs.Add($"\"{name}\" {type}");
        }

        if (columnDefs.Count == 0)
            return "错误: 至少需要定义一列";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"CREATE TABLE IF NOT EXISTS \"{table}\" ({string.Join(", ", columnDefs)})";
        cmd.ExecuteNonQuery();

        return $"已创建表 '{table}'，列: {string.Join(", ", columnDefs)}";
    }

    private string Insert(JsonElement root, string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return "错误: 表名不能为空";

        if (!root.TryGetProperty("row", out var rowEl) ||
            rowEl.ValueKind != JsonValueKind.Object)
            return "错误: 缺少 row 参数";

        var columns = new List<string>();
        var paramNames = new List<string>();
        var values = new Dictionary<string, object?>();

        foreach (var prop in rowEl.EnumerateObject())
        {
            columns.Add($"\"{prop.Name}\"");
            var paramName = $"@p{columns.Count}";
            paramNames.Add(paramName);
            values[paramName] = prop.Value.ValueKind == JsonValueKind.Null
                ? (object?)null
                : prop.Value.ToString();
        }

        if (columns.Count == 0)
            return "错误: row 不能为空";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"INSERT INTO \"{table}\" ({string.Join(", ", columns)}) VALUES ({string.Join(", ", paramNames)})";

        foreach (var (paramName, value) in values)
            cmd.Parameters.AddWithValue(paramName, value ?? DBNull.Value);

        cmd.ExecuteNonQuery();
        return $"已向表 '{table}' 插入 1 行数据";
    }

    private string Select(JsonElement root, string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return "错误: 表名不能为空";

        var limit = root.TryGetProperty("limit", out var limitEl) && limitEl.TryGetInt32(out var lv) ? lv : 100;
        var (whereClause, parameters) = BuildWhereClause(root);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM \"{table}\"{whereClause} LIMIT {limit}";

        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

        return ReadResults(cmd, $"表 '{table}' 查询结果");
    }

    private string Update(JsonElement root, string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return "错误: 表名不能为空";

        if (!root.TryGetProperty("set", out var setEl) ||
            setEl.ValueKind != JsonValueKind.Object)
            return "错误: 缺少 set 参数";

        var setClauses = new List<string>();
        var parameters = new Dictionary<string, object?>();
        var setIndex = 0;

        foreach (var prop in setEl.EnumerateObject())
        {
            var paramName = $"@s{setIndex++}";
            setClauses.Add($"\"{prop.Name}\" = {paramName}");
            parameters[paramName] = prop.Value.ValueKind == JsonValueKind.Null
                ? (object?)null
                : prop.Value.ToString();
        }

        if (setClauses.Count == 0)
            return "错误: set 不能为空";

        var (whereClause, whereParams) = BuildWhereClause(root, paramPrefix: "w");
        foreach (var (k, v) in whereParams)
            parameters[k] = v;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"UPDATE \"{table}\" SET {string.Join(", ", setClauses)}{whereClause}";

        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

        var affected = cmd.ExecuteNonQuery();
        return $"已更新表 '{table}' 中 {affected} 行数据";
    }

    private string Delete(JsonElement root, string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return "错误: 表名不能为空";

        var (whereClause, parameters) = BuildWhereClause(root);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM \"{table}\"{whereClause}";

        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

        var affected = cmd.ExecuteNonQuery();
        return $"已从表 '{table}' 删除 {affected} 行数据";
    }

    private string DropTable(string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return "错误: 表名不能为空";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"DROP TABLE IF EXISTS \"{table}\"";
        cmd.ExecuteNonQuery();
        return $"已删除表 '{table}'";
    }

    /// <summary>
    /// 构造 WHERE 子句和参数
    /// </summary>
    private static (string clause, Dictionary<string, object?> parameters) BuildWhereClause(
        JsonElement root, string paramPrefix = "p")
    {
        if (!root.TryGetProperty("where", out var whereEl) ||
            whereEl.ValueKind != JsonValueKind.Object)
            return (string.Empty, new Dictionary<string, object?>());

        var conditions = new List<string>();
        var parameters = new Dictionary<string, object?>();
        var idx = 0;

        foreach (var prop in whereEl.EnumerateObject())
        {
            var paramName = $"@{paramPrefix}{idx++}";
            conditions.Add($"\"{prop.Name}\" = {paramName}");
            parameters[paramName] = prop.Value.ValueKind == JsonValueKind.Null
                ? (object?)null
                : prop.Value.ToString();
        }

        if (conditions.Count == 0)
            return (string.Empty, new Dictionary<string, object?>());

        return ($" WHERE {string.Join(" AND ", conditions)}", parameters);
    }

    /// <summary>
    /// 从 SqliteCommand 执行并格式化查询结果
    /// </summary>
    private static string ReadResults(SqliteCommand cmd, string header)
    {
        using var reader = cmd.ExecuteReader();
        var columns = Enumerable.Range(0, reader.FieldCount)
                                .Select(i => reader.GetName(i))
                                .ToList();

        var rows = new List<List<string>>();
        while (reader.Read())
        {
            var row = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
                row.Add(reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString() ?? "");
            rows.Add(row);
        }

        if (rows.Count == 0)
            return $"{header}: 无数据";

        var sb = new StringBuilder($"{header}（{rows.Count} 行）:\n");

        // Header row
        sb.AppendLine("  " + string.Join(" | ", columns));
        sb.AppendLine("  " + string.Join("-+-", columns.Select(c => new string('-', c.Length))));

        // Data rows
        for (int i = 0; i < rows.Count; i++)
        {
            var cells = rows[i].Select((v, j) => v.PadRight(columns[j].Length)).ToList();
            sb.AppendLine($"  {string.Join(" | ", cells)}");
        }

        return sb.ToString().TrimEnd();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection.Dispose();
            _disposed = true;
        }
    }
}
