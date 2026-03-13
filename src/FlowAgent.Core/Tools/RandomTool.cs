using System.Text.Json;

namespace FlowAgent.Core.Tools;

/// <summary>
/// 随机数工具 - 生成随机数或随机选择
/// </summary>
public class RandomTool : ITool
{
    private readonly Random _random = new();

    public string Name => "random";

    public string Description => "生成随机数或从选项中随机选择。";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""type"": {
                ""type"": ""string"",
                ""enum"": [""number"", ""choice""],
                ""description"": ""随机类型：number-生成随机数，choice-随机选择""
            },
            ""min"": {
                ""type"": ""integer"",
                ""description"": ""最小值（包含）""
            },
            ""max"": {
                ""type"": ""integer"",
                ""description"": ""最大值（不包含）""
            },
            ""choices"": {
                ""type"": ""array"",
                ""items"": {
                    ""type"": ""string""
                },
                ""description"": ""可选择的选项列表""
            }
        },
        ""required"": [""type""]
    }";

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var args = JsonSerializer.Deserialize<RandomArgs>(arguments, options);
            if (args == null)
            {
                return Task.FromResult("错误: 无法解析参数");
            }

            string result;

            switch (args.Type)
            {
                case "number":
                    var min = args.Min ?? 0;
                    var max = args.Max ?? 100;
                    var randomNumber = _random.Next(min, max);
                    result = $"随机数: {randomNumber} (范围: {min} 到 {max})";
                    break;

                case "choice":
                    if (args.Choices == null || args.Choices.Length == 0)
                    {
                        return Task.FromResult("错误: 没有提供选项");
                    }
                    var index = _random.Next(args.Choices.Length);
                    var chosen = args.Choices[index];
                    result = $"随机选择: {chosen} (从 {args.Choices.Length} 个选项中)";
                    break;

                default:
                    result = $"错误: 不支持的类型 '{args.Type}'";
                    break;
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult($"错误: {ex.Message}");
        }
    }

    private class RandomArgs
    {
        public string Type { get; set; } = string.Empty;
        public int? Min { get; set; }
        public int? Max { get; set; }
        public string[]? Choices { get; set; }
    }
}
