using System.Text.Json;

namespace FlowAgent.Core.Tools;

/// <summary>
/// 日期时间工具 - 提供日期时间相关的功能
/// </summary>
public class DateTimeTool : ITool
{
    public string Name => "datetime";

    public string Description => "获取当前日期时间信息，或进行日期计算。";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""action"": {
                ""type"": ""string"",
                ""enum"": [""current"", ""add_days"", ""format""],
                ""description"": ""要执行的操作：current-获取当前时间，add_days-添加天数，format-格式化日期""
            },
            ""days"": {
                ""type"": ""integer"",
                ""description"": ""要添加的天数（可以是负数）""
            },
            ""format"": {
                ""type"": ""string"",
                ""description"": ""日期格式字符串，如 yyyy-MM-dd HH:mm:ss""
            }
        },
        ""required"": [""action""]
    }";

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var args = JsonSerializer.Deserialize<DateTimeArgs>(arguments, options);
            if (args == null)
            {
                return Task.FromResult("错误: 无法解析参数");
            }

            var now = DateTime.Now;
            string result;

            switch (args.Action)
            {
                case "current":
                    result = $"当前时间: {now:yyyy-MM-dd HH:mm:ss}";
                    break;

                case "add_days":
                    var newDate = now.AddDays(args.Days);
                    result = $"计算结果: {now:yyyy-MM-dd} + {args.Days} 天 = {newDate:yyyy-MM-dd}";
                    break;

                case "format":
                    var formatted = now.ToString(args.Format ?? "yyyy-MM-dd HH:mm:ss");
                    result = $"格式化结果: {formatted}";
                    break;

                default:
                    result = $"错误: 不支持的操作 '{args.Action}'";
                    break;
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult($"错误: {ex.Message}");
        }
    }

    private class DateTimeArgs
    {
        public string Action { get; set; } = string.Empty;
        public int Days { get; set; }
        public string? Format { get; set; }
    }
}
