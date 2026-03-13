using System.Text.Json;

namespace FlowAgent.Core.Tools;

/// <summary>
/// 计算器工具 - 可以执行基本的数学运算
/// 这是一个示例工具，展示如何创建自己的工具
/// </summary>
public class CalculatorTool : ITool
{
    public string Name => "calculator";

    public string Description => "执行基本的数学运算，支持加减乘除。";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""operation"": {
                ""type"": ""string"",
                ""enum"": [""add"", ""subtract"", ""multiply"", ""divide""],
                ""description"": ""要执行的运算类型""
            },
            ""a"": {
                ""type"": ""number"",
                ""description"": ""第一个数字""
            },
            ""b"": {
                ""type"": ""number"",
                ""description"": ""第二个数字""
            }
        },
        ""required"": [""operation"", ""a"", ""b""]
    }";

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            // 解析参数 - 使用不区分大小写的配置
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var args = JsonSerializer.Deserialize<CalculatorArgs>(arguments, options);
            if (args == null)
            {
                return Task.FromResult("错误: 无法解析参数");
            }

            // 执行计算
            double result = args.Operation switch
            {
                "add" => args.A + args.B,
                "subtract" => args.A - args.B,
                "multiply" => args.A * args.B,
                "divide" => args.B != 0 ? args.A / args.B : throw new DivideByZeroException("除数不能为零"),
                _ => throw new ArgumentException($"不支持的运算类型: {args.Operation}")
            };

            return Task.FromResult($"计算结果: {args.A} {GetOperatorSymbol(args.Operation)} {args.B} = {result}");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"错误: {ex.Message}");
        }
    }

    private static string GetOperatorSymbol(string operation)
    {
        return operation switch
        {
            "add" => "+",
            "subtract" => "-",
            "multiply" => "×",
            "divide" => "÷",
            _ => operation
        };
    }

    private class CalculatorArgs
    {
        public string Operation { get; set; } = string.Empty;
        public double A { get; set; }
        public double B { get; set; }
    }
}
