using DatPhongKhachSan.Services;
using DatPhongKhachSan.Services.Chatbot;
using DatPhongKhachSan.Services.Chatbot.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DatPhongKhachSan.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly IEmbeddingProvider _embed;
    private readonly AIConfig _cfg;
    private readonly IWebHostEnvironment _env;
    private readonly ChatbotService _chatbot;

    public HealthController(IEmbeddingProvider embed, IOptions<AIConfig> cfg,
        IWebHostEnvironment env, ChatbotService chatbot)
    {
        _embed   = embed;
        _cfg     = cfg.Value;
        _env     = env;
        _chatbot = chatbot;
    }

    // GET /health/ai - embed 1 chuỗi ngắn, trả 200 nếu OK, 503 nếu lỗi.
    [HttpGet("ai")]
    public async Task<IActionResult> Ai()
    {
        var vector = await _embed.EmbedAsync("test");
        if (vector.Length == 0)
            return StatusCode(503, new { error = "AI provider không phản hồi hoặc trả về embedding rỗng." });

        return Ok(new
        {
            provider   = _cfg.Provider,
            model      = _embed.ModelId,
            dimensions = _embed.Dimensions,
            status     = "ok"
        });
    }

    // GET /health/agent?q=... - test AgentService với 1 câu hỏi, trả kết quả + log chi tiết
    [HttpGet("agent")]
    public async Task<IActionResult> Agent(
        [FromServices] AgentService agent,
        string? q = "giá phòng Deluxe bao nhiêu?")
    {
        var ctx   = new ToolContext(null, null, false);
        var reply = await agent.RunAsync(q!, ctx);
        return Ok(new { question = q, reply });
    }

    // GET /health/faq-scores - đo cosine score cho tập câu mẫu, dùng để chỉnh ngưỡng CRAG.
    // Development only.
    [HttpGet("faq-scores")]
    public async Task<IActionResult> FaqScores()
    {
        if (!_env.IsDevelopment()) return NotFound();
        var queries = new[]
        {
            // Câu khớp tốt (mong đợi score > 0.6)
            "giờ nhận phòng là mấy giờ",
            "chính sách hủy phòng hoàn tiền",
            "wifi mật khẩu",
            "bữa sáng có tính phí không",
            "giờ trả phòng check out",
            // Câu khớp trung bình (mong đợi 0.4-0.6)
            "phòng có cửa sổ nhìn ra biển không",
            "dịch vụ giặt ủi quần áo",
            "thang máy phòng cho người khuyết tật",
            // Câu KHÔNG có trong FAQ (mong đợi < 0.4)
            "khách sạn có sân golf không",
            "thời tiết Đà Nẵng tháng 8",
            "visa du lịch cần giấy tờ gì",
            "giá vé máy bay đi Hà Nội",
        };
        var results = new List<object>();
        foreach (var q in queries)
        {
            var vec  = await _embed.EmbedAsync(q);
            var top  = _chatbot.TimFaqTop(vec, topN: 1, threshold: 0f);
            var best = top.FirstOrDefault();
            results.Add(new
            {
                query    = q,
                topScore = best.Score == 0 ? (double?)null : (double)Math.Round(best.Score, 4),
                matched  = best.Item?.CauHoi?[..Math.Min(50, best.Item.CauHoi.Length)]
            });
        }
        return Ok(results);
    }

    // GET /health/agent-auth?q=...&maNguoiDung=N - TEST ONLY: giả lập đăng nhập với MaNguoiDung cụ thể.
    // CHỈ hoạt động ở Development - trả 404 ở Staging/Production.
    [HttpGet("agent-auth")]
    public async Task<IActionResult> AgentAuth(
        [FromServices] AgentService agent,
        string? q = "đơn của tôi?",
        int? maNguoiDung = null)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var ctx = maNguoiDung.HasValue
            ? new ToolContext("test-user-" + maNguoiDung, maNguoiDung.Value, true)
            : new ToolContext(null, null, false);
        var reply = await agent.RunAsync(q!, ctx);
        return Ok(new { question = q, maNguoiDung, daDangNhap = ctx.DaDangNhap, reply });
    }
}
