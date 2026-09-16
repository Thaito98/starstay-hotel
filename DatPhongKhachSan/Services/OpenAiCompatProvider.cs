using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace DatPhongKhachSan.Services;

// OpenAI-compatible provider: hỗ trợ Ollama (local, không cần key)
// và Cloudflare Workers AI (cloud, cần Bearer token).
// Header Authorization: Bearer {ApiKey} được thêm per-request khi ApiKey khác rỗng.
public class OpenAiCompatProvider : IEmbeddingProvider, IChatProvider
{
    private readonly HttpClient _embedHttp;
    private readonly HttpClient _chatHttp;
    private readonly ILogger<OpenAiCompatProvider> _logger;
    private readonly AIConfig _cfg;
    private readonly EmbeddingCache _cache;

    public string ModelId    => _cfg.EmbeddingModel;
    public int    Dimensions => _cfg.EmbeddingDimensions;

    // Tạo POST request; chỉ gắn Authorization header khi provider KHÔNG phải Ollama.
    // Dùng Provider thay vì kiểm ApiKey rỗng - tránh gửi credential Cloudflare sang Ollama
    // khi User Secrets còn token từ lần chạy Cloudflare trước.
    private HttpRequestMessage BuildPost(string url, string jsonBody)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        var needsAuth = !string.Equals(_cfg.Provider, "Ollama", StringComparison.OrdinalIgnoreCase);
        if (needsAuth && !string.IsNullOrEmpty(_cfg.ApiKey))
            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _cfg.ApiKey);
        return req;
    }

    public OpenAiCompatProvider(
        IHttpClientFactory factory,
        IOptions<AIConfig> cfg,
        ILogger<OpenAiCompatProvider> logger,
        EmbeddingCache cache)
    {
        _embedHttp = factory.CreateClient("ollama");   // timeout 30s
        _chatHttp  = factory.CreateClient("qwen");     // timeout 60s
        _cfg       = cfg.Value;
        _logger    = logger;
        _cache     = cache;
    }

    // ── Retry helper ─────────────────────────────────────────────────────────
    // Retry 2 lần (tổng 3 attempt), backoff 1s / 2s.
    // Retry: 5xx, 429, lỗi mạng (HttpRequestException).
    // KHÔNG retry: 4xx khác (401/403/404/410 - lỗi cố định), timeout (đã chờ đủ lâu).
    private static readonly int[] RetryDelaysMs = { 1000, 2000 };

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient client, string url, string body, CancellationToken ct)
    {
        HttpResponseMessage? last = null;
        for (int attempt = 0; attempt <= RetryDelaysMs.Length; attempt++)
        {
            if (attempt > 0)
            {
                _logger.LogWarning("[RETRY] Lần {A}/{Max} sau {D}ms - url={Url}",
                    attempt + 1, RetryDelaysMs.Length + 1, RetryDelaysMs[attempt - 1],
                    url.Split('/')[^1]);
                await Task.Delay(RetryDelaysMs[attempt - 1], ct);
            }

            try
            {
                last?.Dispose();
                last = await client.SendAsync(BuildPost(url, body), ct);

                var code = (int)last.StatusCode;
                if (code < 500 && code != 429) return last;       // success hoặc 4xx cố định
                if (attempt == RetryDelaysMs.Length) return last;  // hết lần retry

                _logger.LogWarning("[RETRY] Status={Code}, sẽ retry", last.StatusCode);
            }
            catch (HttpRequestException ex)
            {
                if (attempt == RetryDelaysMs.Length) throw;
                _logger.LogWarning("[RETRY] NetworkError lần {A}: {Ex}", attempt + 1, ex.Message);
            }
            // TaskCanceledException (timeout) - KHÔNG retry, để caller xử lý
        }
        return last!;
    }

    // ── IEmbeddingProvider ────────────────────────────────────────────────────

    public async Task<float[]> EmbedAsync(string text)
    {
        var normalized = EmbeddingUtils.NormalizeText(text);
        if (string.IsNullOrEmpty(normalized)) return Array.Empty<float>();

        // Cache check - tránh gọi Cloudflare lặp lại cho cùng query
        if (_cache.TryGet(normalized, out var cached))
        {
            _logger.LogDebug("[EMBED] cache HIT (hits={H}, misses={M})", _cache.Hits, _cache.Misses);
            return cached;
        }

        try
        {
            var body = JsonSerializer.Serialize(new { model = _cfg.EmbeddingModel, input = normalized });
            using var resp = await SendWithRetryAsync(_embedHttp, _cfg.BaseUrl + "/embeddings", body, CancellationToken.None);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[EMBED-v1] {Code}", resp.StatusCode);
                return Array.Empty<float>();
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var vec = doc.RootElement
                .GetProperty("data")[0]
                .GetProperty("embedding")
                .EnumerateArray()
                .Select(e => e.GetSingle())
                .ToArray();

            _cache.Set(normalized, vec);
            _logger.LogDebug("[EMBED] cache MISS => stored (count={C})", _cache.Count);
            return vec;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EMBED-v1] Lỗi gọi {Model}", _cfg.EmbeddingModel);
            return Array.Empty<float>();
        }
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts)
    {
        var list = texts.ToList();
        if (list.Count == 0) return Array.Empty<float[]>();

        var normalized = list.Select(EmbeddingUtils.NormalizeText).ToList();

        try
        {
            var body = JsonSerializer.Serialize(new { model = _cfg.EmbeddingModel, input = normalized });
            using var resp = await SendWithRetryAsync(_embedHttp, _cfg.BaseUrl + "/embeddings", body, CancellationToken.None);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[EMBED-BATCH-v1] {Code}", resp.StatusCode);
                return Array.Empty<float[]>();
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("data")
                .EnumerateArray()
                .OrderBy(el => el.GetProperty("index").GetInt32())
                .Select(el => el.GetProperty("embedding")
                    .EnumerateArray()
                    .Select(e => e.GetSingle())
                    .ToArray())
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EMBED-BATCH-v1] Lỗi batch {Model}", _cfg.EmbeddingModel);
            return Array.Empty<float[]>();
        }
    }

    // ── IChatProvider - simple (ChatbotController/GenQwen path) ─────────────

    public async Task<ChatKetQua> ChatAsync(
        IEnumerable<(string Role, string Content)> messages,
        IEnumerable<object>? tools = null,
        CancellationToken ct = default)
    {
        try
        {
            var msgs = messages
                .Select(m => new { role = m.Role, content = m.Content })
                .ToList();

            var body = JsonSerializer.Serialize(new
            {
                model       = _cfg.ChatModel,
                messages    = msgs,
                stream      = false,
                temperature = 0.3,
                max_tokens  = 1024
            });

            using var resp = await SendWithRetryAsync(_chatHttp, _cfg.BaseUrl + "/chat/completions", body, ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[CHAT-v1] {Code}", resp.StatusCode);
                return new ChatKetQua(null, Array.Empty<ToolCall>());
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim();

            return new ChatKetQua(content, Array.Empty<ToolCall>());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[CHAT-v1] Lỗi gọi {Model}", _cfg.ChatModel);
            return new ChatKetQua(null, Array.Empty<ToolCall>());
        }
    }

    // ── IChatProvider - structured messages cho AgentService ─────────────────
    // Hỗ trợ đầy đủ tool_calls: gửi "tools" trong body, parse "tool_calls" từ response.
    // Fallback chain: main Cloudflare => Groq (nếu có config) => null (AgentService dùng hardcode).

    public async Task<ChatKetQua> ChatWithToolsAsync(
        IReadOnlyList<object> messages,
        IReadOnlyList<object> tools,
        CancellationToken ct = default)
    {
        var result = await TryChatEndpointAsync(
            _cfg.BaseUrl, _cfg.ChatModel, apiKey: null, messages, tools, ct);

        if (result != null) return result;

        // ── Fallback sang provider phụ (Groq...) nếu đã cấu hình ─────────────
        if (!string.IsNullOrEmpty(_cfg.FallbackChatBaseUrl))
        {
            _logger.LogWarning("[FALLBACK] Provider chính lỗi - thử {Url} model={Model}",
                _cfg.FallbackChatBaseUrl, _cfg.FallbackChatModel);
            var fb = await TryChatEndpointAsync(
                _cfg.FallbackChatBaseUrl, _cfg.FallbackChatModel,
                apiKey: _cfg.FallbackChatApiKey, messages, tools, ct);
            if (fb != null)
            {
                _logger.LogInformation("[FALLBACK] Fallback thành công");
                return fb;
            }
            _logger.LogWarning("[FALLBACK] Fallback cũng lỗi");
        }

        return new ChatKetQua(null, Array.Empty<ToolCall>());
    }

    // Gọi một endpoint chat+tools, trả null nếu bất kỳ lỗi nào.
    // apiKey=null => dùng BuildPost thông thường (provider-aware); apiKey có giá trị => Bearer tường minh.
    private async Task<ChatKetQua?> TryChatEndpointAsync(
        string baseUrl, string model, string? apiKey,
        IReadOnlyList<object> messages, IReadOnlyList<object> tools,
        CancellationToken ct)
    {
        try
        {
            object bodyObj = tools.Count > 0
                ? new { model, messages, tools, stream = false, temperature = 0.1, max_tokens = 1024 }
                : new { model, messages,        stream = false, temperature = 0.1, max_tokens = 1024 };

            var body = JsonSerializer.Serialize(bodyObj,
                new JsonSerializerOptions { PropertyNamingPolicy = null });

            var url = baseUrl.TrimEnd('/') + "/chat/completions";

            // Main provider: SendWithRetryAsync tự tạo request mỗi attempt qua BuildPost
            // Fallback provider (apiKey != null): gửi 1 lần, dùng BuildExplicitPost
            HttpResponseMessage resp = apiKey != null
                ? await _chatHttp.SendAsync(BuildExplicitPost(url, body, apiKey), ct)
                : await SendWithRetryAsync(_chatHttp, url, body, ct);

            using (resp)
            {
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("[CHAT-TOOLS] {Code}: {Err}", resp.StatusCode,
                        err[..Math.Min(300, err.Length)]);
                    return null;
                }

                var json = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogDebug("[CHAT-TOOLS] raw response: {Json}", json[..Math.Min(800, json.Length)]);

                using var doc = JsonDocument.Parse(json);
                var choice  = doc.RootElement.GetProperty("choices")[0];
                var message = choice.GetProperty("message");

                var finishReason = choice.TryGetProperty("finish_reason", out var fr)
                    ? fr.GetString() : null;

                if (finishReason == "tool_calls" &&
                    message.TryGetProperty("tool_calls", out var tcArr))
                {
                    var toolCalls = tcArr.EnumerateArray().Select(tc =>
                    {
                        var id   = tc.TryGetProperty("id",   out var idProp)   ? idProp.GetString()   ?? "" : "";
                        var func = tc.GetProperty("function");
                        var name = func.TryGetProperty("name", out var nProp)  ? nProp.GetString()    ?? "" : "";
                        var args = func.TryGetProperty("arguments", out var aProp) ? aProp.GetString() ?? "{}" : "{}";
                        return new ToolCall(id, name, args);
                    }).ToList();

                    _logger.LogInformation("[CHAT-TOOLS] finish_reason=tool_calls, {Count} tool call(s)",
                        toolCalls.Count);
                    return new ChatKetQua(null, toolCalls);
                }

                var content = message.TryGetProperty("content", out var cProp)
                    ? cProp.GetString()?.Trim() : null;
                return new ChatKetQua(content, Array.Empty<ToolCall>());
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[CHAT-TOOLS] Lỗi endpoint {Url} model={Model}", baseUrl, model);
            return null;
        }
    }

    // BuildPost với ApiKey tường minh (không phụ thuộc vào _cfg.Provider)
    private HttpRequestMessage BuildExplicitPost(string url, string jsonBody, string apiKey)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(apiKey))
            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        return req;
    }
}
