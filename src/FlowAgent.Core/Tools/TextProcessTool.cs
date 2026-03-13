using System.Text.Json;

namespace FlowAgent.Core.Tools;

/// <summary>
/// 文本处理工具 - 提供各种文本处理功能
/// </summary>
public class TextProcessTool : ITool
{
    public string Name => "text_process";

    public string Description => "处理文本，支持转大写、转小写、反转、计算长度等操作。";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""operation"": {
                ""type"": ""string"",
                ""enum"": [""upper"", ""lower"", ""reverse"", ""length"", ""words_count""],
                ""description"": ""要执行的操作""
            },
            ""text"": {
                ""type"": ""string"",
                ""description"": ""要处理的文本""
            }
        },
        ""required"": [""operation"", ""text""]
    }";

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var args = JsonSerializer.Deserialize<TextProcessArgs>(arguments, options);
            if (args == null || string.IsNullOrEmpty(args.Text))
            {
                return Task.FromResult("错误: 无法解析参数或文本为空");
            }

            string result;

            switch (args.Operation)
            {
                case "upper":
                    result = $"转大写结果: {args.Text.ToUpper()}";
                    break;

                case "lower":
                    result = $"转小写结果: {args.Text.ToLower()}";
                    break;

                case "reverse":
                    var reversed = new string(args.Text.Reverse().ToArray());
                    result = $"反转结果: {reversed}";
                    break;

                case "length":
                    result = $"文本长度: {args.Text.Length} 个字符";
                    break;

                case "words_count":
                    var words = args.Text.Split(new[] { ' ', '\t', '\n', '\r' },
                        StringSplitOptions.RemoveEmptyEntries);
                    result = $"单词数量: {words.Length} 个";
                    break;

                default:
                    result = $"错误: 不支持的操作 '{args.Operation}'";
                    break;
            }

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult($"错误: {ex.Message}");
        }
    }

    private class TextProcessArgs
    {
        public string Operation { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
