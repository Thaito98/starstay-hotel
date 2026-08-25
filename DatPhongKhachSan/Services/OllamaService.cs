using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DatPhongKhachSan.Services;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _http;      // timeout 30s - cho bge-m3 embedding
    private readonly HttpClient _qwenHttp;  // timeout 60s - cho qwen2.5:3b generation
    private readonly ILogger<OllamaService> _logger;

    private const string EmbedEndpoint = "http://localhost:11434/api/embeddings";
    private const string ChatEndpoint  = "http://localhost:11434/api/chat";
    private const string EmbedModel    = "qllama/bge-m3:q8_0";
    private const string QwenModel     = "qwen2.5:3b";

    // keep_alive="60m": giữ model nóng 60 phút sau lần gọi cuối.
    // Không dùng chuỗi "-1" - Ollama yêu cầu đơn vị thời gian (s/m/h).
    private const string KeepAlive = "60m";

    public OllamaService(IHttpClientFactory factory, ILogger<OllamaService> logger)
    {
        _http     = factory.CreateClient("ollama");
        _qwenHttp = factory.CreateClient("qwen");
        _logger   = logger;
    }

    // ── Kiểm tra trạng thái Ollama + model bge-m3 ────────────────────────────
    public async Task<(bool Connected, bool HasModel)> CheckStatusAsync()
    {
        try
        {
            using var resp = await _http.GetAsync("http://localhost:11434/api/tags");
            if (!resp.IsSuccessStatusCode) return (false, false);
            var json = await resp.Content.ReadAsStringAsync();
            return (true, json.Contains("\"" + EmbedModel));
        }
        catch { return (false, false); }
    }

    // ── Embedding (bge-m3) - gọi 1 lần trực tiếp, không retry CPU/GPU ────────
    // CPU/GPU do Ollama quyết định qua biến môi trường OLLAMA_NUM_GPU=0.
    public async Task<float[]?> GetEmbeddingAsync(string text)
    {
        var normalized = EmbeddingUtils.NormalizeText(text);
        if (string.IsNullOrEmpty(normalized)) return null;

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                model      = EmbedModel,
                prompt     = normalized,
                keep_alive = KeepAlive
            });

            using var resp = await _http.PostAsync(EmbedEndpoint,
                new StringContent(body, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[EMBED] {Code}", resp.StatusCode);
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement.GetProperty("embedding").EnumerateArray()
                         .Select(e => e.GetSingle()).ToArray();
            return arr.Length > 0 ? arr : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EMBED] Lỗi gọi bge-m3");
            return null;
        }
    }

    // ── Text generation (qwen2.5:3b) - gọi 1 lần trực tiếp ─────────────────
    public async Task<string?> GenerateWithQwenAsync(string systemPrompt,
        IEnumerable<(string Role, string Content)> messages)
    {
        try
        {
            var allMsgs = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };
            foreach (var (role, content) in messages)
                allMsgs.Add(new { role, content });

            var body = JsonSerializer.Serialize(new
            {
                model      = QwenModel,
                messages   = allMsgs,
                stream     = false,
                keep_alive = KeepAlive,
                options    = new { temperature = 0.3, num_predict = 512 }
            });

            using var resp = await _qwenHttp.PostAsync(ChatEndpoint,
                new StringContent(body, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("[QWEN] {Code}", resp.StatusCode);
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[QWEN] Lỗi gọi qwen2.5:3b");
            return null;
        }
    }

    // ── Warm-up: kích Ollama load cả 2 model vào RAM ─────────────────────────
    // Gọi khi user mở khung chat (fire-and-forget từ controller).
    public async Task WarmUpAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var embedBody = JsonSerializer.Serialize(new
            {
                model      = EmbedModel,
                prompt     = "warm",
                keep_alive = KeepAlive
            });
            using var r1 = await _http.PostAsync(EmbedEndpoint,
                new StringContent(embedBody, Encoding.UTF8, "application/json"));
            _logger.LogInformation("[WARMUP] bge-m3: {Code} {Ms}ms", r1.StatusCode, sw.ElapsedMilliseconds);

            sw.Restart();
            var qwenBody = JsonSerializer.Serialize(new
            {
                model      = QwenModel,
                messages   = new[] { new { role = "user", content = "hi" } },
                stream     = false,
                keep_alive = KeepAlive,
                options    = new { num_predict = 1 }
            });
            using var r2 = await _qwenHttp.PostAsync(ChatEndpoint,
                new StringContent(qwenBody, Encoding.UTF8, "application/json"));
            _logger.LogInformation("[WARMUP] qwen: {Code} {Ms}ms", r2.StatusCode, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[WARMUP] Lỗi - Ollama chưa chạy?");
        }
    }

}
