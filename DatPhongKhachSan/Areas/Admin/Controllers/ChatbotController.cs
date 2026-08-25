using CsvHelper;
using CsvHelper.Configuration;
using DatPhongKhachSan.Constants;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

namespace DatPhongKhachSan.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = ChucNang.QuanLyChatbot)]
public class ChatbotController : Controller
{
    private readonly DatPhongKhachSanContext _db;
    private readonly IOllamaService _ollama;       // giữ cho CheckStatusAsync + WarmUp
    private readonly IEmbeddingProvider _embed;
    private readonly ChatbotService _chatbot;
    private readonly AIConfig _aiCfg;
    private readonly ILogger<ChatbotController> _logger;

    public ChatbotController(DatPhongKhachSanContext db, IOllamaService ollama, IEmbeddingProvider embed,
                             ChatbotService chatbot, IOptions<AIConfig> aiCfg,
                             ILogger<ChatbotController> logger)
    {
        _db      = db;
        _ollama  = ollama;
        _embed   = embed;
        _chatbot = chatbot;
        _aiCfg   = aiCfg.Value;
        _logger  = logger;
    }

    // ── GET /Admin/Chatbot ────────────────────────────────────────────────────
    public async Task<IActionResult> Index(string? search, string? chuDe)
    {
        var query = _db.DoanVanBans.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(d => d.CauHoi.Contains(search) || d.TraLoi.Contains(search));
        if (!string.IsNullOrWhiteSpace(chuDe))
            query = query.Where(d => d.ChuDe == chuDe);

        var ds = await query.OrderBy(d => d.ChuDe).ThenBy(d => d.MaDoan).ToListAsync();

        ViewBag.Search    = search;
        ViewBag.ChuDe     = chuDe;
        ViewBag.ChuDes    = await _db.DoanVanBans.Select(d => d.ChuDe).Distinct().OrderBy(x => x).ToListAsync();
        ViewBag.CacheCnt  = _chatbot.CacheCount;
        ViewBag.TongDong  = await _db.DoanVanBans.CountAsync();
        ViewBag.CoEmbedding = await _db.DoanVanBans.CountAsync(d => d.Embedding != null);

        // Cảnh báo lệch model embedding
        var modelHienTai = _aiCfg.EmbeddingModel;
        var soLechModel  = await _db.DoanVanBans.CountAsync(d =>
            d.Embedding != null &&
            (d.ModelEmbedding == null || d.ModelEmbedding != modelHienTai));
        if (soLechModel > 0)
            ViewBag.CanhBaoLechModel = $"{soLechModel} đoạn được sinh bởi model khác, " +
                                       $"cấu hình hiện tại là \"{modelHienTai}\" - cần Re-index.";

        return View(ds);
    }

    // ── POST /Admin/Chatbot/TaoMoi ────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoMoi(string cauHoi, string traLoi, string? chuDe)
    {
        if (string.IsNullOrWhiteSpace(cauHoi) || string.IsNullOrWhiteSpace(traLoi))
        {
            TempData["Error"] = "Câu hỏi và câu trả lời không được để trống.";
            return RedirectToAction(nameof(Index));
        }

        var embed = await _embed.EmbedAsync(cauHoi);

        var item = new DoanVanBan
        {
            CauHoi    = cauHoi.Trim(),
            TraLoi    = traLoi.Trim(),
            ChuDe     = chuDe?.Trim(),
            Embedding      = embed.Length > 0 ? EmbeddingUtils.ToBytes(embed) : null,
            ModelEmbedding = embed.Length > 0 ? _aiCfg.EmbeddingModel : null,
            NgayIndex      = DateTime.Now
        };
        _db.DoanVanBans.Add(item);
        await _db.SaveChangesAsync();

        await _chatbot.LoadCacheAsync();
        TempData["Success"] = embed.Length > 0
            ? "Đã thêm FAQ và tạo embedding."
            : "Đã thêm FAQ (AI provider chưa chạy - embedding trống).";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Admin/Chatbot/CapNhat ───────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhat(int maDoan, string cauHoi, string traLoi, string? chuDe)
    {
        var item = await _db.DoanVanBans.FindAsync(maDoan);
        if (item == null) return NotFound();

        item.CauHoi = cauHoi.Trim();
        item.TraLoi = traLoi.Trim();
        item.ChuDe  = chuDe?.Trim();

        var embed = await _embed.EmbedAsync(cauHoi);
        if (embed.Length > 0)
        {
            item.Embedding      = EmbeddingUtils.ToBytes(embed);
            item.ModelEmbedding = _aiCfg.EmbeddingModel;
        }
        item.NgayIndex = DateTime.Now;

        await _db.SaveChangesAsync();
        await _chatbot.LoadCacheAsync();
        TempData["Success"] = "Đã cập nhật FAQ.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Admin/Chatbot/Xoa ───────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Xoa(int maDoan)
    {
        var item = await _db.DoanVanBans.FindAsync(maDoan);
        if (item != null) { _db.DoanVanBans.Remove(item); await _db.SaveChangesAsync(); }
        await _chatbot.LoadCacheAsync();
        TempData["Success"] = "Đã xóa FAQ.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Admin/Chatbot/ImportCsv ─────────────────────────────────────────
    // Import CSV (cột: ChuDe, CauHoi, TraLoi). Không gen embedding - chạy Re-index sau.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportCsv(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Vui lòng chọn file CSV.";
            return RedirectToAction(nameof(Index));
        }

        // Load toàn bộ câu hỏi đã có để check trùng
        var existingList = await _db.DoanVanBans
            .Select(d => d.CauHoi.Trim().ToLower())
            .ToListAsync();
        var existing = existingList.ToHashSet();

        int them = 0, trung = 0, skip = 0;
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord  = true,
            MissingFieldFound = null,
            BadDataFound     = null
        };
        using var csv = new CsvReader(reader, config);

        await foreach (var row in csv.GetRecordsAsync<FaqCsvRow>())
        {
            if (string.IsNullOrWhiteSpace(row.CauHoi) || string.IsNullOrWhiteSpace(row.TraLoi))
            { skip++; continue; }

            var key = row.CauHoi.Trim().ToLower();
            if (existing.Contains(key)) { trung++; continue; }   // bỏ qua trùng

            _db.DoanVanBans.Add(new DoanVanBan
            {
                CauHoi    = row.CauHoi.Trim(),
                TraLoi    = row.TraLoi.Trim(),
                ChuDe     = row.ChuDe?.Trim(),
                Embedding = null,
                NgayIndex = DateTime.Now
            });
            existing.Add(key);   // tránh trùng ngay trong cùng file
            them++;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Import: +{them} dòng mới, {trung} trùng bỏ qua, {skip} dòng trống. Nhấn Re-index để tạo embedding.";
        return RedirectToAction(nameof(Index));
    }

    // ── GET /Admin/Chatbot/ExportCsv ──────────────────────────────────────────
    public async Task<IActionResult> ExportCsv()
    {
        var ds = await _db.DoanVanBans.OrderBy(d => d.ChuDe).ThenBy(d => d.MaDoan).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("ChuDe,CauHoi,TraLoi");
        foreach (var d in ds)
        {
            var chuDe  = EscapeCsv(d.ChuDe ?? "");
            var cauHoi = EscapeCsv(d.CauHoi);
            var traLoi = EscapeCsv(d.TraLoi);
            sb.AppendLine($"{chuDe},{cauHoi},{traLoi}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"faq_export_{DateTime.Now:yyyyMMdd}.csv");
    }

    // ── POST /Admin/Chatbot/ReIndex ───────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ReIndex()
    {
        if (_chatbot.Progress.IsRunning)
        {
            TempData["Error"] = "Re-index đang chạy, vui lòng đợi cho đến khi hoàn tất.";
            return RedirectToAction(nameof(Index));
        }
        // Set IsRunning = true TRƯỚC khi redirect để trang mới poll thấy ngay
        _chatbot.Progress.IsRunning   = true;
        _chatbot.Progress.Done        = 0;
        _chatbot.Progress.Failed      = 0;
        _chatbot.Progress.Total       = 0;
        _chatbot.Progress.LastMessage = null;
        _ = _chatbot.RunReIndexAsync();
        return RedirectToAction(nameof(Index));
    }

    // ── GET /Admin/Chatbot/TestReIndex ───────────────────────────────────────
    // Embed 5 dòng đầu chưa có model hiện tại, ghi vào DB, trả JSON kết quả.
    // Dùng để xác nhận batch + cosine trước khi chạy full re-index.
    [HttpGet]
    public async Task<IActionResult> TestReIndex()
    {
        var targetModel = _aiCfg.EmbeddingModel;

        var batch = await _db.DoanVanBans
            .Where(d => d.ModelEmbedding == null || d.ModelEmbedding != targetModel)
            .OrderBy(d => d.MaDoan)
            .Take(5)
            .ToListAsync();

        if (batch.Count == 0)
            return Json(new { message = "Tất cả dòng đã được index với model hiện tại." });

        var texts   = batch.Select(d => d.CauHoi).ToList();
        var vectors = await _embed.EmbedBatchAsync(texts);

        if (vectors.Count == 0)
            return Json(new { error = "EmbedBatchAsync trả rỗng - kiểm tra kết nối Cloudflare/ApiKey." });

        int ok = 0;
        for (int i = 0; i < batch.Count; i++)
        {
            var vec = i < vectors.Count ? vectors[i] : Array.Empty<float>();
            if (vec.Length > 0)
            {
                batch[i].Embedding      = EmbeddingUtils.ToBytes(vec);
                batch[i].ModelEmbedding = targetModel;
                batch[i].NgayIndex      = DateTime.Now;
                ok++;
            }
        }
        await _db.SaveChangesAsync();

        // Kiểm cosine: embed lại text đầu tiên, so với vector vừa lưu
        float cosine  = 0;
        int   freshDim = 0;
        if (ok > 0)
        {
            var fresh = await _embed.EmbedAsync(batch[0].CauHoi);
            freshDim  = fresh.Length;
            if (fresh.Length > 0)
                cosine = ChatbotService.CosineSimilarity(vectors[0], fresh);
        }

        return Json(new
        {
            processed  = batch.Count,
            ok,
            model      = targetModel,
            dimensions = freshDim,
            cosine     = Math.Round(cosine, 6),
            sample     = batch[0].CauHoi[..Math.Min(60, batch[0].CauHoi.Length)]
        });
    }

    // ── GET /Admin/Chatbot/AiStatus ───────────────────────────────────────────
    // Kiểm provider thực tế đang cấu hình (Cloudflare, Ollama, ...) bằng cách embed 1 chuỗi test.
    [HttpGet]
    public async Task<IActionResult> AiStatus()
    {
        try
        {
            var vec = await _embed.EmbedAsync("test");
            return Json(new
            {
                ok       = vec.Length > 0,
                provider = _aiCfg.Provider,
                model    = _aiCfg.EmbeddingModel,
                dims     = vec.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ADMIN] AiStatus lỗi");
            return Json(new { ok = false, provider = _aiCfg.Provider, model = _aiCfg.EmbeddingModel, dims = 0 });
        }
    }

    // ── GET /Admin/Chatbot/OllamaStatus ──────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> OllamaStatus()
    {
        var (connected, hasModel) = await _ollama.CheckStatusAsync();
        return Json(new { connected, hasModel });
    }

    // ── GET /Admin/Chatbot/IndexProgress ─────────────────────────────────────
    [HttpGet]
    public IActionResult IndexProgress()
    {
        var p = _chatbot.Progress;
        return Json(new
        {
            isRunning   = p.IsRunning,
            total       = p.Total,
            done        = p.Done,
            failed      = p.Failed,
            percent     = p.Percent,
            lastMessage = p.LastMessage
        });
    }

    // ── POST /Admin/Chatbot/ImportMau ─────────────────────────────────────────
    // Import file faq_template.csv có sẵn trong Data/
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportMau([FromServices] IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "faq_template.csv");
        if (!System.IO.File.Exists(path))
        {
            TempData["Error"] = "Không tìm thấy file Data/faq_template.csv.";
            return RedirectToAction(nameof(Index));
        }

        var existing = (await _db.DoanVanBans
            .Select(d => d.CauHoi.Trim().ToLower()).ToListAsync()).ToHashSet();

        int them = 0, trung = 0;
        using var reader = new StreamReader(path, Encoding.UTF8);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, MissingFieldFound = null };
        using var csv = new CsvReader(reader, config);

        await foreach (var row in csv.GetRecordsAsync<FaqCsvRow>())
        {
            if (string.IsNullOrWhiteSpace(row.CauHoi)) continue;
            var key = row.CauHoi.Trim().ToLower();
            if (existing.Contains(key)) { trung++; continue; }

            _db.DoanVanBans.Add(new DoanVanBan
            {
                CauHoi = row.CauHoi.Trim(), TraLoi = row.TraLoi.Trim(),
                ChuDe  = row.ChuDe?.Trim(), NgayIndex = DateTime.Now
            });
            existing.Add(key);
            them++;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = trung > 0
            ? $"Import: +{them} dòng mới, {trung} trùng bỏ qua. Nhấn Re-index để tạo embedding."
            : $"Đã import {them} FAQ từ file mẫu. Nhấn Re-index để tạo embedding.";
        return RedirectToAction(nameof(Index));
    }

    private static string EscapeCsv(string s)
        => s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}

public class FaqCsvRow
{
    public string ChuDe  { get; set; } = string.Empty;
    public string CauHoi { get; set; } = string.Empty;
    public string TraLoi { get; set; } = string.Empty;
}
