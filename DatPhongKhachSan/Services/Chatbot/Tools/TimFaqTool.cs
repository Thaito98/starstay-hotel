using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DatPhongKhachSan.Services.Chatbot.Tools;

public class TimFaqTool : ITool
{
    private readonly IEmbeddingProvider _embed;
    private readonly ChatbotService _chatbot;
    private readonly ILogger<TimFaqTool> _logger;
    private readonly float _nguongTot;
    private readonly float _nguongTrungBinh;
    private readonly float _retrievalThreshold;

    public TimFaqTool(IEmbeddingProvider embed, ChatbotService chatbot,
        ILogger<TimFaqTool> logger, IOptions<AIConfig> cfg)
    {
        _embed              = embed;
        _chatbot            = chatbot;
        _logger             = logger;
        _nguongTot          = cfg.Value.CragNguongTot;
        _nguongTrungBinh    = cfg.Value.CragNguongTrungBinh;
        // Retrieval lấy thêm các FAQ cận ngưỡng để LLM tự phán xét
        _retrievalThreshold = Math.Max(0f, _nguongTrungBinh - 0.05f);
    }

    public string Ten => "tim_faq";

    public string MoTa =>
        "Tìm câu trả lời trong cơ sở tri thức FAQ của khách sạn " +
        "(chính sách, quy định, tiện ích, giờ check-in/out, wifi, hủy phòng, ...). " +
        "Kết quả trả về kèm confidence (high/medium) và score (0-1). " +
        "Nếu sufficientEvidence=false => KHÔNG có FAQ phù hợp, hãy nói không biết thay vì bịa.";

    public object ThamSoSchema => new
    {
        type = "object",
        properties = new
        {
            cau_hoi = new
            {
                type        = "string",
                description = "Câu hỏi cần tìm trong FAQ (ví dụ: giờ nhận phòng, chính sách hủy phòng)."
            }
        },
        required = new[] { "cau_hoi" }
    };

    public bool CanDangNhap => false;

    public async Task<string> ThucThiAsync(JsonElement thamSo, ToolContext ctx, CancellationToken ct = default)
    {
        string cauHoi = "";
        if (thamSo.TryGetProperty("cau_hoi", out var p) && p.ValueKind == JsonValueKind.String)
            cauHoi = p.GetString()?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(cauHoi))
            return "Thiếu tham số cau_hoi.";

        // EmbedAsync gọi NormalizeText bên trong - giống hệt cách index data
        var embed = await _embed.EmbedAsync(cauHoi);
        if (embed.Length == 0)
        {
            // Embedding service lỗi - degraded mode: tìm LIKE thay vì cosine
            var likeRows = await _chatbot.TimFaqLikeAsync(cauHoi, maxResults: 3);
            if (likeRows.Count == 0)
                return JsonSerializer.Serialize(new
                {
                    sufficientEvidence = false,
                    message = "Embedding service tạm thời không khả dụng, không tìm được FAQ."
                });
            _logger.LogWarning("[FAQ-CRAG] Degraded LIKE mode - {N} kết quả", likeRows.Count);
            var likeKq = likeRows.Select(x => new
            {
                cau_hoi    = x.CauHoi,
                tra_loi    = x.TraLoi,
                score      = 0.0,
                confidence = "degraded"
            });
            return JsonSerializer.Serialize(likeKq, _jsonOpts);
        }

        var top = _chatbot.TimFaqTop(embed, topN: 3, threshold: _retrievalThreshold);
        var topScore = top.Count > 0 ? top[0].Score : 0f;

        _logger.LogInformation("[FAQ-CRAG] topScore={S:F4} | nguongTot={T} | nguongTB={TB} | cauHoi=\"{Q}\"",
            topScore, _nguongTot, _nguongTrungBinh, cauHoi[..Math.Min(60, cauHoi.Length)]);

        // Không đủ bằng chứng - LLM phải nói không biết, KHÔNG bịa
        if (topScore < _nguongTrungBinh || top.Count == 0)
        {
            _logger.LogInformation("[FAQ-CRAG] => sufficientEvidence=false");
            return JsonSerializer.Serialize(new
            {
                sufficientEvidence = false,
                message = "Không tìm thấy FAQ đủ liên quan (score thấp nhất ngưỡng). " +
                          "Hãy nói không biết và mời khách liên hệ lễ tân."
            });
        }

        // Có bằng chứng - trả kèm confidence để LLM tự điều chỉnh ngôn ngữ
        var ketQua = top.Select(x => new
        {
            cau_hoi    = x.Item.CauHoi,
            tra_loi    = x.Item.TraLoi,
            score      = Math.Round(x.Score, 3),
            confidence = x.Score >= _nguongTot ? "high" : "medium"
        });

        return JsonSerializer.Serialize(ketQua, _jsonOpts);
    }

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
