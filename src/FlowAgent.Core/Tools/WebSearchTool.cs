using System.Text;
using System.Text.Json;

namespace FlowAgent.Core.Tools;

/// <summary>
/// 网页搜索工具 - 使用 DuckDuckGo 即时答案 API 搜索网络内容
/// </summary>
public class WebSearchTool : ITool, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    // DuckDuckGo Instant Answer API endpoint
    private const string DdgApiUrl = "https://api.duckduckgo.com/";

    /// <summary>
    /// 使用默认 HttpClient 创建网页搜索工具
    /// </summary>
    public WebSearchTool() : this(new HttpClient())
    {
        _ownsHttpClient = true;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; FlowAgent/1.0)");
    }

    /// <summary>
    /// 使用外部提供的 HttpClient 创建网页搜索工具（推荐在生产环境使用 IHttpClientFactory）
    /// </summary>
    public WebSearchTool(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _ownsHttpClient = false;
    }

    public string Name => "web_search";

    public string Description => "使用 DuckDuckGo 搜索网络内容，返回相关摘要和链接。";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""query"": {
                ""type"": ""string"",
                ""description"": ""搜索关键词或问题""
            },
            ""max_results"": {
                ""type"": ""integer"",
                ""description"": ""返回结果的最大数量，默认 5，最大 20""
            }
        },
        ""required"": [""query""]
    }";

    public async Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var args = JsonSerializer.Deserialize<SearchArgs>(arguments, options);
            if (args == null)
                return "错误: 无法解析参数";

            if (string.IsNullOrWhiteSpace(args.Query))
                return "错误: 搜索关键词不能为空";

            var maxResults = Math.Clamp(args.MaxResults ?? 5, 1, 20);

            return await SearchDuckDuckGoAsync(args.Query, maxResults, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return "错误: 搜索请求超时";
        }
        catch (HttpRequestException ex)
        {
            return $"错误: 网络请求失败 - {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"错误: {ex.Message}";
        }
    }

    private async Task<string> SearchDuckDuckGoAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        // DuckDuckGo Instant Answer API: returns JSON with abstract, topics, and related links
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"{DdgApiUrl}?q={encodedQuery}&format=json&no_redirect=1&no_html=1&skip_disambig=1";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseDuckDuckGoResponse(json, query, maxResults);
    }

    private static string ParseDuckDuckGoResponse(string json, string query, int maxResults)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var sb = new StringBuilder($"搜索结果 — \"{query}\":\n\n");
        var resultCount = 0;

        // 1. Abstract (summary from Wikipedia or other sources)
        var abstractText = root.TryGetProperty("Abstract", out var absEl) ? absEl.GetString() : null;
        var abstractUrl = root.TryGetProperty("AbstractURL", out var absUrlEl) ? absUrlEl.GetString() : null;
        var abstractSource = root.TryGetProperty("AbstractSource", out var absSrcEl) ? absSrcEl.GetString() : null;

        if (!string.IsNullOrWhiteSpace(abstractText))
        {
            sb.AppendLine($"📖 摘要 ({abstractSource}):");
            sb.AppendLine($"   {abstractText}");
            if (!string.IsNullOrWhiteSpace(abstractUrl))
                sb.AppendLine($"   来源: {abstractUrl}");
            sb.AppendLine();
            resultCount++;
        }

        // 2. Answer (direct answer for factual queries)
        var answer = root.TryGetProperty("Answer", out var ansEl) ? ansEl.GetString() : null;
        var answerType = root.TryGetProperty("AnswerType", out var ansTypeEl) ? ansTypeEl.GetString() : null;
        if (!string.IsNullOrWhiteSpace(answer))
        {
            sb.AppendLine($"✅ 直接答案{(string.IsNullOrWhiteSpace(answerType) ? "" : $" ({answerType})")}:");
            sb.AppendLine($"   {answer}");
            sb.AppendLine();
            resultCount++;
        }

        // 3. Definition
        var definition = root.TryGetProperty("Definition", out var defEl) ? defEl.GetString() : null;
        var definitionUrl = root.TryGetProperty("DefinitionURL", out var defUrlEl) ? defUrlEl.GetString() : null;
        if (!string.IsNullOrWhiteSpace(definition))
        {
            sb.AppendLine("📚 定义:");
            sb.AppendLine($"   {definition}");
            if (!string.IsNullOrWhiteSpace(definitionUrl))
                sb.AppendLine($"   来源: {definitionUrl}");
            sb.AppendLine();
            resultCount++;
        }

        // 4. Related Topics
        if (root.TryGetProperty("RelatedTopics", out var topicsEl) &&
            topicsEl.ValueKind == JsonValueKind.Array)
        {
            var topicResults = new List<(string text, string url)>();

            foreach (var topic in topicsEl.EnumerateArray())
            {
                if (topicResults.Count >= maxResults)
                    break;

                if (topic.ValueKind != JsonValueKind.Object)
                    continue;

                // Top-level topic
                if (topic.TryGetProperty("Text", out var textEl) &&
                    topic.TryGetProperty("FirstURL", out var urlEl))
                {
                    var text = textEl.GetString();
                    var url = urlEl.GetString();
                    if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(url))
                        topicResults.Add((text, url));
                }

                // Nested topics in a group
                if (topic.TryGetProperty("Topics", out var nestedTopics) &&
                    nestedTopics.ValueKind == JsonValueKind.Array)
                {
                    foreach (var nested in nestedTopics.EnumerateArray())
                    {
                        if (topicResults.Count >= maxResults)
                            break;

                        if (nested.TryGetProperty("Text", out var nTextEl) &&
                            nested.TryGetProperty("FirstURL", out var nUrlEl))
                        {
                            var text = nTextEl.GetString();
                            var url = nUrlEl.GetString();
                            if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(url))
                                topicResults.Add((text, url));
                        }
                    }
                }
            }

            if (topicResults.Count > 0)
            {
                sb.AppendLine("🔗 相关结果:");
                for (int i = 0; i < topicResults.Count; i++)
                {
                    var (text, url) = topicResults[i];
                    sb.AppendLine($"   [{i + 1}] {text}");
                    sb.AppendLine($"       {url}");
                }
                sb.AppendLine();
                resultCount += topicResults.Count;
            }
        }

        // 5. Infobox (structured key-value info)
        if (root.TryGetProperty("Infobox", out var infoboxEl) &&
            infoboxEl.ValueKind == JsonValueKind.Object &&
            infoboxEl.TryGetProperty("content", out var infoContentEl) &&
            infoContentEl.ValueKind == JsonValueKind.Array)
        {
            var infoItems = new List<string>();
            foreach (var item in infoContentEl.EnumerateArray())
            {
                if (item.TryGetProperty("label", out var labelEl) &&
                    item.TryGetProperty("value", out var valueEl))
                {
                    var label = labelEl.GetString();
                    var value = valueEl.GetString();
                    if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(value))
                        infoItems.Add($"   {label}: {value}");
                }

                if (infoItems.Count >= 10) // limit infobox items
                    break;
            }

            if (infoItems.Count > 0)
            {
                sb.AppendLine("ℹ️  信息摘要:");
                foreach (var item in infoItems)
                    sb.AppendLine(item);
                sb.AppendLine();
                resultCount++;
            }
        }

        if (resultCount == 0)
        {
            sb.AppendLine("未找到相关结果。");
            sb.AppendLine("建议:");
            sb.AppendLine("  • 尝试使用不同的关键词");
            sb.AppendLine("  • 使用 web_request 工具直接访问搜索引擎");
        }

        return sb.ToString().TrimEnd();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_ownsHttpClient)
                _httpClient.Dispose();
            _disposed = true;
        }
    }

    private class SearchArgs
    {
        public string Query { get; set; } = string.Empty;
        public int? MaxResults { get; set; }
    }
}
