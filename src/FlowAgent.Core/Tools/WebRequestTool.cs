using System.Text;
using System.Text.Json;

namespace FlowAgent.Core.Tools;

/// <summary>
/// 网络请求工具 - 支持 HTTP GET / POST 请求
/// </summary>
public class WebRequestTool : ITool, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    /// <summary>
    /// 使用默认 HttpClient 创建网络请求工具
    /// </summary>
    public WebRequestTool() : this(new HttpClient())
    {
        _ownsHttpClient = true;
        // 只在我们自己管理 HttpClient 时设置超时
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 使用外部提供的 HttpClient 创建网络请求工具（推荐在生产环境使用 IHttpClientFactory）
    /// </summary>
    public WebRequestTool(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _ownsHttpClient = false;
    }

    public string Name => "web_request";

    public string Description => "发送 HTTP 请求以获取网络资源，支持 GET 和 POST 方法。";

    public string ParametersSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""method"": {
                ""type"": ""string"",
                ""enum"": [""GET"", ""POST""],
                ""description"": ""HTTP 请求方法，默认 GET""
            },
            ""url"": {
                ""type"": ""string"",
                ""description"": ""请求的目标 URL""
            },
            ""body"": {
                ""type"": ""string"",
                ""description"": ""POST 请求的请求体（JSON 字符串或普通文本）""
            },
            ""content_type"": {
                ""type"": ""string"",
                ""description"": ""请求体的 Content-Type，默认 application/json""
            }
        },
        ""required"": [""url""]
    }";

    public async Task<string> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var args = JsonSerializer.Deserialize<WebRequestArgs>(arguments, options);
            if (args == null)
                return "错误: 无法解析参数";

            if (string.IsNullOrWhiteSpace(args.Url))
                return "错误: URL 不能为空";

            // 验证 URL 格式，只允许 http/https
            if (!Uri.TryCreate(args.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return $"错误: URL 格式无效或不支持的协议 '{args.Url}'";
            }

            var method = string.IsNullOrEmpty(args.Method) ? "GET" : args.Method.ToUpperInvariant();

            HttpResponseMessage response;
            if (method == "POST")
            {
                var contentType = args.ContentType ?? "application/json";
                var body = args.Body ?? string.Empty;
                var content = new StringContent(body, Encoding.UTF8, contentType);
                response = await _httpClient.PostAsync(uri, content, cancellationToken);
            }
            else
            {
                response = await _httpClient.GetAsync(uri, cancellationToken);
            }

            var statusCode = (int)response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            // 截断过长的响应
            const int maxResponseLength = 2000;
            var truncated = false;
            if (responseBody.Length > maxResponseLength)
            {
                responseBody = responseBody[..maxResponseLength];
                truncated = true;
            }

            var result = $"HTTP {statusCode} {response.ReasonPhrase}\n{responseBody}";
            if (truncated)
                result += $"\n... [响应已截断，只显示前 {maxResponseLength} 个字符]";

            return result;
        }
        catch (TaskCanceledException)
        {
            return "错误: 请求超时";
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

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_ownsHttpClient)
                _httpClient.Dispose();
            _disposed = true;
        }
    }

    private class WebRequestArgs
    {
        public string? Method { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Body { get; set; }
        public string? ContentType { get; set; }
    }
}
