using System.Text.Json;
using FlowAgent.Core.Tools;

namespace FlowAgent.SamplePlugin;

/// <summary>
/// 示例插件工具：单位换算
/// 演示如何将 ITool 实现打包为可热加载的插件 DLL
/// </summary>
public class UnitConverterTool : ITool
{
    public string Name => "unit_converter";

    public string Description => "单位换算工具，支持长度（米/英尺/英寸）、重量（千克/磅/盎司）和温度（摄氏/华氏/开尔文）之间的换算。";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""category"": {
                ""type"": ""string"",
                ""enum"": [""length"", ""weight"", ""temperature""],
                ""description"": ""换算类别：length（长度）、weight（重量）、temperature（温度）""
            },
            ""value"": {
                ""type"": ""number"",
                ""description"": ""要换算的数值""
            },
            ""from"": {
                ""type"": ""string"",
                ""description"": ""来源单位（长度: m/ft/in；重量: kg/lb/oz；温度: C/F/K）""
            },
            ""to"": {
                ""type"": ""string"",
                ""description"": ""目标单位（同上）""
            }
        },
        ""required"": [""category"", ""value"", ""from"", ""to""]
    }";

    public Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            var category = root.GetProperty("category").GetString() ?? "";
            var value = root.GetProperty("value").GetDouble();
            var from = root.GetProperty("from").GetString()?.ToLower() ?? "";
            var to = root.GetProperty("to").GetString()?.ToLower() ?? "";

            double result = category switch
            {
                "length" => ConvertLength(value, from, to),
                "weight" => ConvertWeight(value, from, to),
                "temperature" => ConvertTemperature(value, from, to),
                _ => throw new ArgumentException($"不支持的换算类别: {category}")
            };

            return Task.FromResult(
                $"{value} {from.ToUpper()} = {result:F4} {to.ToUpper()}"
            );
        }
        catch (Exception ex)
        {
            return Task.FromResult($"换算失败: {ex.Message}");
        }
    }

    private static double ConvertLength(double value, string from, string to)
    {
        // 转换为米
        double meters = from switch
        {
            "m" => value,
            "ft" => value * 0.3048,
            "in" => value * 0.0254,
            _ => throw new ArgumentException($"不支持的长度单位: {from}")
        };

        return to switch
        {
            "m" => meters,
            "ft" => meters / 0.3048,
            "in" => meters / 0.0254,
            _ => throw new ArgumentException($"不支持的长度单位: {to}")
        };
    }

    private static double ConvertWeight(double value, string from, string to)
    {
        // 转换为千克
        double kg = from switch
        {
            "kg" => value,
            "lb" => value * 0.453592,
            "oz" => value * 0.0283495,
            _ => throw new ArgumentException($"不支持的重量单位: {from}")
        };

        return to switch
        {
            "kg" => kg,
            "lb" => kg / 0.453592,
            "oz" => kg / 0.0283495,
            _ => throw new ArgumentException($"不支持的重量单位: {to}")
        };
    }

    private static double ConvertTemperature(double value, string from, string to)
    {
        // 转换为摄氏度
        double celsius = from switch
        {
            "c" => value,
            "f" => (value - 32) * 5 / 9,
            "k" => value - 273.15,
            _ => throw new ArgumentException($"不支持的温度单位: {from}")
        };

        return to switch
        {
            "c" => celsius,
            "f" => celsius * 9 / 5 + 32,
            "k" => celsius + 273.15,
            _ => throw new ArgumentException($"不支持的温度单位: {to}")
        };
    }
}
