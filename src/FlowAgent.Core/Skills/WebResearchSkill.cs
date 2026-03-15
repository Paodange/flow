using System.Text;
using System.Text.Json;
using FlowAgent.Core.Tools;

namespace FlowAgent.Core.Skills;

/// <summary>
/// 网络研究技能 - 多轮搜索并汇总结果
/// 演示如何组合多个工具完成复杂的多步骤任务
/// </summary>
/// <remarks>
/// 该技能依赖以下工具（至少需要 web_search 或 web_request 之一）：
/// <list type="bullet">
///   <item><description>web_search：使用 DuckDuckGo 搜索网络内容</description></item>
/// </list>
/// </remarks>
public class WebResearchSkill : SkillBase
{
    public override string Name => "web_research";

    public override string Description =>
        "对指定主题进行多维度网络调研，自动执行多次搜索并汇总成结构化报告。适合需要综合多方信息的研究任务。";

    public override string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""topic"": {
                ""type"": ""string"",
                ""description"": ""要研究的主题或问题""
            },
            ""aspects"": {
                ""type"": ""array"",
                ""items"": { ""type"": ""string"" },
                ""description"": ""要调研的具体方面（可选，不填则自动生成 3 个搜索关键词）""
            },
            ""max_searches"": {
                ""type"": ""integer"",
                ""description"": ""最大搜索次数（1-5，默认 3）""
            }
        },
        ""required"": [""topic""]
    }";

    public override async Task<string> ExecuteAsync(
        string arguments,
        IReadOnlyDictionary<string, ITool> tools,
        CancellationToken cancellationToken = default)
    {
        // 检查依赖工具
        var missing = CheckRequiredTools(tools, "web_search");
        if (missing.Count > 0)
            return $"错误: 技能 '{Name}' 缺少必要工具: {string.Join(", ", missing)}";

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var args = JsonSerializer.Deserialize<WebResearchArgs>(arguments, options);
            if (args == null || string.IsNullOrWhiteSpace(args.Topic))
                return "错误: topic 参数不能为空";

            var maxSearches = Math.Clamp(args.MaxSearches ?? 3, 1, 5);

            // 确定搜索关键词列表
            List<string> queries;
            if (args.Aspects is { Count: > 0 })
            {
                queries = args.Aspects.Take(maxSearches).ToList();
            }
            else
            {
                // 自动生成多角度查询词
                queries = GenerateSearchQueries(args.Topic, maxSearches);
            }

            // 执行多次搜索并收集结果
            var sb = new StringBuilder();
            sb.AppendLine($"📊 网络调研报告：{args.Topic}");
            sb.AppendLine(new string('=', 50));
            sb.AppendLine();

            for (int i = 0; i < queries.Count; i++)
            {
                var query = queries[i];
                sb.AppendLine($"## 搜索 {i + 1}/{queries.Count}：{query}");
                sb.AppendLine();

                var searchArgs = JsonSerializer.Serialize(new { query, max_results = 3 });
                var searchResult = await InvokeToolAsync(tools, "web_search", searchArgs, cancellationToken);
                sb.AppendLine(searchResult);
                sb.AppendLine();
            }

            sb.AppendLine(new string('-', 50));
            sb.AppendLine($"✅ 调研完成，共执行 {queries.Count} 次搜索");

            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"错误: 执行技能 '{Name}' 时发生异常 - {ex.Message}";
        }
    }

    /// <summary>
    /// 根据主题自动生成多角度搜索关键词
    /// </summary>
    private static readonly string[] SearchSuffixes =
    [
        "",
        " 介绍 概述",
        " 最新进展",
        " 优缺点 评价",
        " 应用场景",
        " 教程"
    ];

    /// <summary>
    /// 根据主题自动生成多角度搜索关键词
    /// </summary>
    private static List<string> GenerateSearchQueries(string topic, int count)
    {
        // 生成不同维度的搜索词（简单规则，实际项目中可以用 LLM 生成）
        return SearchSuffixes
            .Take(count)
            .Select(s => topic + s)
            .ToList();
    }

    private class WebResearchArgs
    {
        public string Topic { get; set; } = string.Empty;
        public List<string>? Aspects { get; set; }
        public int? MaxSearches { get; set; }
    }
}
