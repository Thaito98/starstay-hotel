using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Services;
using DatPhongKhachSan.Services.Chatbot;
using DatPhongKhachSan.Services.Chatbot.Tools;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Claims;

namespace DatPhongKhachSan.Controllers;

[Route("Chatbot")]
public class ChatbotController : Controller
{
    private readonly DatPhongKhachSanContext _db;
    private readonly IOllamaService _ollama;
    private readonly IEmbeddingProvider _embed;
    private readonly IChatProvider _chat;
    private readonly ChatbotService _chatbot;
    private readonly AgentService _agent;
    private readonly ILogger<ChatbotController> _logger;
    private readonly AIConfig _aiCfg;

    private const string SessionKey = "ChatPhien";
    private const string FallbackMsg =
        "Xin lỗi, tôi chưa hiểu rõ câu hỏi của bạn. " +
        "Vui lòng liên hệ lễ tân qua số 0236 999 8888 để được hỗ trợ.";

    // plan.md mục 7.4: cosine > FaqThreshold => trả FAQ; hạ từ 0.6 => 0.5 vì bge-m3 thực tế
    // cho điểm ~0.5 ngay cả với câu khớp gần đúng.
    private const float FaqThreshold     = 0.5f;
    private const float SuggestThreshold = 0.4f;  // score trung bình => gợi ý câu gần nhất

    private const string SystemPrompt =
        "Bạn là trợ lý ảo của StarStay Hotel, khách sạn 4 sao tại Đà Nẵng. " +
        "Chỉ trả lời dựa trên dữ liệu được cung cấp trong [Dữ liệu]. " +
        "Không bịa thêm thông tin, không tiết lộ thông tin của khách khác. " +
        "Trả lời ngắn gọn, lịch sự, bằng tiếng Việt. Không quá 200 từ.";

    public ChatbotController(
        DatPhongKhachSanContext db,
        IOllamaService ollama,
        IEmbeddingProvider embed,
        IChatProvider chat,
        ChatbotService chatbot,
        AgentService agent,
        IOptions<AIConfig> aiCfg,
        ILogger<ChatbotController> logger)
    {
        _db      = db;
        _ollama  = ollama;
        _embed   = embed;
        _chat    = chat;
        _chatbot = chatbot;
        _agent   = agent;
        _aiCfg   = aiCfg.Value;
        _logger  = logger;
    }

    // ── POST /Chatbot/Gui ─────────────────────────────────────────────────────
    [HttpPost("Gui")]
    public async Task<IActionResult> Gui([FromBody] ChatRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.CauHoi))
            return BadRequest(new { error = "Câu hỏi không được để trống." });

        var swTotal = Stopwatch.StartNew();
        var cauHoi  = EmbeddingUtils.NormalizeText(req.CauHoi);
        var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Lấy hoặc tạo phiên chat từ Session
        var phienChat = LayHoacTaoPhien();

        // Lấy 6 tin nhắn gần nhất (3 lượt Q&A) làm lịch sử trước câu hỏi hiện tại
        var lichSu = await _db.TinNhanChats
            .Where(t => t.MaPhienChat == phienChat)
            .OrderByDescending(t => t.MaTinNhan)
            .Take(6)
            .OrderBy(t => t.MaTinNhan)
            .ToListAsync();

        // Phân loại intent
        var intent = ChatbotService.PhanLoaiIntent(cauHoi);
        _logger.LogInformation("[CHATBOT] intent={Intent} | cauHoi=\"{CauHoi}\"", intent, cauHoi);

        string botReply;
        string loaiTraLoi;

        try
        {
            (botReply, loaiTraLoi) = intent switch
            {
                ChatIntent.GiaPhong   => await XuLyGiaPhong(cauHoi, lichSu),
                ChatIntent.PhongTrong => await XuLyPhongTrong(cauHoi, lichSu),
                ChatIntent.DichVu     => await XuLyDichVu(cauHoi, lichSu),
                ChatIntent.DonCuaToi  => await XuLyDonCuaToi(cauHoi, lichSu, userId),
                _                     => await XuLyFaq(cauHoi, lichSu),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CHATBOT] Lỗi xử lý intent={Intent}", intent);
            botReply   = FallbackMsg;
            loaiTraLoi = "Khac";
        }

        // Lưu tin nhắn User + Bot
        var now = ThoiGian.Now;
        _db.TinNhanChats.AddRange(
            new TinNhanChat
            {
                MaPhienChat    = phienChat,
                UserId         = userId,
                VaiTro         = "User",
                NoiDung        = cauHoi,
                LoaiCauTraLoi  = loaiTraLoi,
                NgayTao        = now
            },
            new TinNhanChat
            {
                MaPhienChat    = phienChat,
                UserId         = userId,
                VaiTro         = "Bot",
                NoiDung        = botReply,
                LoaiCauTraLoi  = loaiTraLoi,
                NgayTao        = now.AddMilliseconds(1)
            });
        await _db.SaveChangesAsync();

        _logger.LogInformation("[CHATBOT] tong: {TotalMs}ms", swTotal.ElapsedMilliseconds);

        return Json(new { reply = botReply, loai = loaiTraLoi, phien = phienChat });
    }

    // ── POST /Chatbot/GuiAgent ────────────────────────────────────────────────
    // Endpoint agentic RAG (Phase 19). Giữ /Gui cũ nguyên - dùng song song để A/B test.
    // Danh tính PHẢI lấy từ HttpContext.User - TUYỆT ĐỐI không từ req/body.
    [HttpPost("GuiAgent")]
    public async Task<IActionResult> GuiAgent([FromBody] ChatRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.CauHoi))
            return BadRequest(new { error = "Câu hỏi không được để trống." });

        var cauHoi = EmbeddingUtils.NormalizeText(req.CauHoi);

        // Danh tính từ HttpContext.User => tra DB => MaNguoiDung
        var userId     = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var daDangNhap = !string.IsNullOrEmpty(userId);
        NguoiDung? nguoiDung = null;
        if (daDangNhap)
            nguoiDung = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.UserId == userId);

        var ctx       = new ToolContext(userId, nguoiDung?.MaNguoiDung, daDangNhap);
        var phienChat = LayHoacTaoPhien();

        // Lấy 6 tin nhắn gần nhất (3 lượt Q&A) làm context cho model
        var lichSu = await _db.TinNhanChats
            .Where(t => t.MaPhienChat == phienChat)
            .OrderByDescending(t => t.MaTinNhan)
            .Take(6)
            .OrderBy(t => t.MaTinNhan)
            .Select(t => new { t.VaiTro, t.NoiDung })
            .ToListAsync();

        var reply = await _agent.RunAsync(
            cauHoi, ctx,
            lichSu.Select(t => (t.VaiTro, t.NoiDung)));

        // Lưu lịch sử chat (cùng pattern với /Gui)
        var now       = DateTime.Now;
        _db.TinNhanChats.AddRange(
            new TinNhanChat
            {
                MaPhienChat   = phienChat, UserId = userId,
                VaiTro        = "User",    NoiDung = cauHoi,
                LoaiCauTraLoi = "Agent",   NgayTao = now
            },
            new TinNhanChat
            {
                MaPhienChat   = phienChat, UserId = userId,
                VaiTro        = "Bot",     NoiDung = reply,
                LoaiCauTraLoi = "Agent",   NgayTao = now.AddMilliseconds(1)
            });
        await _db.SaveChangesAsync();

        return Json(new { reply });
    }

    // ── GET /Chatbot/LichSu ──────────────────────────────────────────────────
    // Đã đăng nhập => trả 50 tin theo UserId (bền vững qua reload/đăng nhập lại).
    // Chưa đăng nhập => theo session phiên (mất khi đóng trình duyệt).
    [HttpGet("LichSu")]
    public async Task<IActionResult> LichSu()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(userId))
        {
            var msgs = await _db.TinNhanChats
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.MaTinNhan)
                .Take(50)
                .OrderBy(t => t.MaTinNhan)
                .Select(t => new { t.VaiTro, t.NoiDung, t.LoaiCauTraLoi })
                .ToListAsync();
            return Json(msgs);
        }

        var phienChat = LayPhienHienTai();
        if (phienChat == Guid.Empty)
            return Json(new List<object>());

        var sessionMsgs = await _db.TinNhanChats
            .Where(t => t.MaPhienChat == phienChat && t.UserId == null)
            .OrderByDescending(t => t.MaTinNhan)
            .Take(20)
            .OrderBy(t => t.MaTinNhan)
            .Select(t => new { t.VaiTro, t.NoiDung, t.LoaiCauTraLoi })
            .ToListAsync();
        return Json(sessionMsgs);
    }

    // ── POST /Chatbot/XoaLichSu ───────────────────────────────────────────────
    // Xóa toàn bộ TinNhanChat của user đang đăng nhập.
    // UserId lấy từ ClaimsPrincipal - không nhận tham số từ client.
    [HttpPost("XoaLichSu")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> XoaLichSu()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Forbid();

        _db.TinNhanChats.RemoveRange(
            _db.TinNhanChats.Where(t => t.UserId == userId));
        await _db.SaveChangesAsync();

        return Json(new { ok = true });
    }

    // ── GET /Chatbot/WarmUp ───────────────────────────────────────────────────
    // Gọi từ JS khi user mở khung chat lần đầu.
    // Ollama: ping localhost để nạp model vào RAM.
    // Cloudflare: gọi 1 request nhỏ để model thoát trạng thái cold start (~2-5s)
    //             trước khi user gõ câu hỏi thật, tránh 37s chờ lần đầu.
    [HttpGet("WarmUp")]
    public IActionResult WarmUp()
    {
        if (string.Equals(_aiCfg.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
            _ = _ollama.WarmUpAsync();
        else
            _ = _chat.ChatAsync(new[] { ("user", "hi") });
        return Ok();
    }

    // ── POST /Chatbot/DatLaiPhien ─────────────────────────────────────────────
    [HttpPost("DatLaiPhien")]
    public IActionResult DatLaiPhien()
    {
        HttpContext.Session.Remove(SessionKey);
        return Json(new { ok = true });
    }

    // ── Handlers theo intent ──────────────────────────────────────────────────

    private async Task<(string Reply, string Loai)> XuLyGiaPhong(
        string cauHoi, List<TinNhanChat> lichSu)
    {
        var loaiPhongs = await _db.LoaiPhongs
            .Where(l => l.TrangThai)
            .OrderBy(l => l.GiaCoBan)
            .ToListAsync();

        string data;
        if (loaiPhongs.Count == 0)
        {
            data = "Hiện không có thông tin giá phòng. Vui lòng liên hệ lễ tân.";
        }
        else
        {
            var rows = loaiPhongs.Select(l =>
            {
                var sucChua  = $"sức chứa {l.SucChuaNguoiLon} người lớn" +
                               (l.SucChuaTreEm > 0 ? $" + {l.SucChuaTreEm} trẻ em" : "");
                var giuong   = string.IsNullOrEmpty(l.LoaiGiuong) ? "" : $", giường: {l.LoaiGiuong}";
                var dientich = l.DienTich.HasValue ? $", diện tích: {l.DienTich:N0}m²" : "";
                return $"- {l.TenLoaiPhong}: {l.GiaCoBan:N0} VNĐ/đêm, {sucChua}{giuong}{dientich}";
            });
            data = "[Dữ liệu]\nBảng giá phòng StarStay Hotel:\n" + string.Join("\n", rows);
        }

        var reply = await GenQwen(cauHoi, lichSu, data) ?? FallbackGiaPhong(loaiPhongs);
        return (reply, "GiaPhong");
    }

    private async Task<(string Reply, string Loai)> XuLyPhongTrong(
        string cauHoi, List<TinNhanChat> lichSu)
    {
        // Lấy số phòng trống hiện tại theo từng loại
        var phongTrong = await (
            from p in _db.Phongs
            join lp in _db.LoaiPhongs on p.MaLoaiPhong equals lp.MaLoaiPhong
            where p.TrangThai == "Trống" && lp.TrangThai
            group new { p, lp } by new { lp.TenLoaiPhong, lp.GiaCoBan } into g
            orderby g.Key.GiaCoBan
            select new { g.Key.TenLoaiPhong, g.Key.GiaCoBan, SoPhong = g.Count() }
        ).ToListAsync();

        string data;
        if (phongTrong.Count > 0)
        {
            var rows = phongTrong.Select(r =>
                $"- {r.TenLoaiPhong}: {r.SoPhong} phòng trống, giá từ {r.GiaCoBan:N0} VNĐ/đêm");
            data = "[Dữ liệu]\nPhòng trống hiện tại tại StarStay Hotel:\n" +
                   string.Join("\n", rows) +
                   "\nĐể đặt phòng theo ngày cụ thể, vui lòng sử dụng trang Đặt phòng.";
        }
        else
        {
            data = "[Dữ liệu]\nHiện tại không có phòng trống. " +
                   "Vui lòng liên hệ lễ tân hoặc kiểm tra lại vào ngày khác.";
        }

        var reply = await GenQwen(cauHoi, lichSu, data) ?? data.Replace("[Dữ liệu]\n", "");
        return (reply, "PhongTrong");
    }

    private async Task<(string Reply, string Loai)> XuLyDichVu(
        string cauHoi, List<TinNhanChat> lichSu)
    {
        var dichVus = await _db.DichVus
            .Where(d => d.TrangThai)
            .OrderBy(d => d.TenDichVu)
            .ToListAsync();

        string data;
        if (dichVus.Count == 0)
        {
            data = "[Dữ liệu]\nHiện không có thông tin dịch vụ. Vui lòng liên hệ lễ tân.";
        }
        else
        {
            var rows = dichVus.Select(d =>
            {
                var mo = string.IsNullOrWhiteSpace(d.MoTa) ? "" : $" - {d.MoTa}";
                return $"- {d.TenDichVu}: {d.Gia:N0} VNĐ/{d.DonVi}{mo}";
            });
            data = "[Dữ liệu]\nDịch vụ tại StarStay Hotel:\n" + string.Join("\n", rows);
        }

        var reply = await GenQwen(cauHoi, lichSu, data) ?? data.Replace("[Dữ liệu]\n", "");
        return (reply, "DichVu");
    }

    private async Task<(string Reply, string Loai)> XuLyDonCuaToi(
        string cauHoi, List<TinNhanChat> lichSu, string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return ("Bạn cần đăng nhập để xem thông tin đơn đặt phòng.", "DonCuaToi");

        var nguoiDung = await _db.NguoiDungs
            .FirstOrDefaultAsync(n => n.UserId == userId);

        if (nguoiDung == null)
            return (FallbackMsg, "DonCuaToi");

        var donDatPhong = await _db.DatPhongs
            .Where(d => d.MaNguoiDung == nguoiDung.MaNguoiDung)
            .OrderByDescending(d => d.MaDatPhong)
            .Take(3)
            .ToListAsync();

        string data;
        if (donDatPhong.Count == 0)
        {
            data = "[Dữ liệu]\nBạn chưa có đơn đặt phòng nào.";
        }
        else
        {
            var rows = donDatPhong.Select(d =>
                $"- Mã #{d.MaDatPhong}: nhận phòng {d.NgayNhanPhong:dd/MM/yyyy}, " +
                $"trả phòng {d.NgayTraPhong:dd/MM/yyyy} ({d.SoDem} đêm), " +
                $"trạng thái: {d.TrangThai}");
            data = $"[Dữ liệu]\nĐơn đặt phòng gần nhất của {nguoiDung.HoTen}:\n" +
                   string.Join("\n", rows);
        }

        var reply = await GenQwen(cauHoi, lichSu, data) ?? data.Replace("[Dữ liệu]\n", "");
        return (reply, "DonCuaToi");
    }

    private async Task<(string Reply, string Loai)> XuLyFaq(
        string cauHoi, List<TinNhanChat> lichSu)
    {
        _logger.LogInformation("[CHATBOT] FAQ cache: {CacheCount} dòng", _chatbot.CacheCount);
        if (_chatbot.CacheCount == 0)
            return ("Hệ thống FAQ chưa được index. Vui lòng liên hệ Admin hoặc thử lại sau.", "FAQ");

        var sw = Stopwatch.StartNew();
        var embed = await _embed.EmbedAsync(cauHoi);
        var embedMs = sw.ElapsedMilliseconds;

        if (embed.Length == 0)
        {
            _logger.LogWarning("[CHATBOT] embed: {Ms}ms => empty (provider không phản hồi)", embedMs);
            return (FallbackMsg, "Khac");
        }

        sw.Restart();
        // threshold: 0f => luôn lấy best match để log score thực tế, rồi tự kiểm tra ngưỡng
        var (faq, score) = _chatbot.TimFaqTotNhat(embed, threshold: 0f);
        var cosineMs = sw.ElapsedMilliseconds;

        _logger.LogInformation(
            "[CHATBOT] embed: {EmbedMs}ms | cosine: {CosineMs}ms | score: {Score:F4} (ngưỡng={Threshold}) | faq: \"{FaqMatch}\"",
            embedMs, cosineMs, score, FaqThreshold,
            faq?.CauHoi?.Substring(0, Math.Min(60, faq.CauHoi.Length)) ?? "null");

        // Đủ ngưỡng => trả FAQ qua Qwen
        if (score >= FaqThreshold && faq != null)
        {
            var data  = $"[Dữ liệu]\n{faq.TraLoi}";
            var reply = await GenQwen(cauHoi, lichSu, data) ?? faq.TraLoi;
            return (reply, "FAQ");
        }

        // Score trung bình => gợi ý câu gần nhất thay vì fallback cứng
        if (score >= SuggestThreshold && faq != null)
        {
            var suggest = $"Xin lỗi, tôi chưa hiểu rõ câu hỏi của bạn.\n" +
                          $"Có phải bạn muốn hỏi: \"{faq.CauHoi}\"?";
            return (suggest, "FAQ");
        }

        return (FallbackMsg, "FAQ");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string?> GenQwen(
        string cauHoi, List<TinNhanChat> lichSu, string data)
    {
        // Prepend system prompt rồi nối lịch sử + câu hỏi mới.
        // IChatProvider.ChatAsync nhận full message list (không có tham số systemPrompt riêng).
        var messages = new List<(string Role, string Content)>
        {
            ("system", SystemPrompt)
        };
        foreach (var t in lichSu)
            messages.Add((t.VaiTro == "User" ? "user" : "assistant", t.NoiDung));

        var lastMsg = $"{data}\n\nCâu hỏi: {cauHoi}";
        messages.Add(("user", lastMsg));

        // Đo độ dài prompt (chars) để chẩn đoán tốc độ
        var promptChars = messages.Sum(m => m.Content.Length);

        var sw = Stopwatch.StartNew();
        var result = await _chat.ChatAsync(messages);
        var qwenMs = sw.ElapsedMilliseconds;

        _logger.LogInformation("[CHATBOT] qwen: {QwenMs}ms | prompt_chars: {Chars} | ok: {Ok}",
            qwenMs, promptChars, result.NoiDung != null);

        return result.NoiDung;
    }

    private Guid LayHoacTaoPhien()
    {
        if (HttpContext.Session.TryGetValue(SessionKey, out var bytes) && bytes.Length == 16)
            return new Guid(bytes);

        var newGuid = Guid.NewGuid();
        HttpContext.Session.Set(SessionKey, newGuid.ToByteArray());
        return newGuid;
    }

    private Guid LayPhienHienTai()
    {
        if (HttpContext.Session.TryGetValue(SessionKey, out var bytes) && bytes.Length == 16)
            return new Guid(bytes);
        return Guid.Empty;
    }

    private static string FallbackGiaPhong(List<LoaiPhong> loaiPhongs)
    {
        if (loaiPhongs.Count == 0)
            return "Vui lòng liên hệ lễ tân để biết giá phòng hiện tại.";
        var rows = loaiPhongs.Select(l => $"• {l.TenLoaiPhong}: {l.GiaCoBan:N0} VNĐ/đêm");
        return "Giá phòng tại StarStay Hotel:\n" + string.Join("\n", rows);
    }
}

public class ChatRequest
{
    public string? CauHoi { get; set; }
}
