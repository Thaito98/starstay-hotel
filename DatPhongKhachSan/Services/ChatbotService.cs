using DatPhongKhachSan.Data;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Services;

public enum ChatIntent { GiaPhong, PhongTrong, DichVu, DonCuaToi, FAQ }

public class ReIndexProgress
{
    public bool IsRunning { get; set; }
    public int Total      { get; set; }
    public int Done       { get; set; }
    public int Failed     { get; set; }
    public string? LastMessage { get; set; }
    public int Percent => Total == 0 ? 0 : (int)(Done * 100.0 / Total);
}

// Singleton - cache embedding FAQ trong memory suốt vòng đời ứng dụng
public class ChatbotService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChatbotService> _logger;

    private List<FaqCacheItem> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ReIndexProgress Progress { get; } = new();

    public ChatbotService(IServiceScopeFactory scopeFactory, ILogger<ChatbotService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    // ── Load/reload cache từ DB ───────────────────────────────────────────────
    // Chỉ load dòng có embedding từ đúng model hiện tại - tránh so cosine giữa 2 không gian.
    public async Task LoadCacheAsync()
    {
        await _lock.WaitAsync();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db          = scope.ServiceProvider.GetRequiredService<DatPhongKhachSanContext>();
            var aiCfg       = scope.ServiceProvider
                                   .GetRequiredService<Microsoft.Extensions.Options.IOptions<AIConfig>>()
                                   .Value;
            var currentModel = aiCfg.EmbeddingModel;

            var rows = await db.DoanVanBans
                .Where(d => d.Embedding != null && d.ModelEmbedding == currentModel)
                .ToListAsync();

            _cache = rows
                .Select(d => new FaqCacheItem
                {
                    MaDoan    = d.MaDoan,
                    CauHoi    = d.CauHoi,
                    TraLoi    = d.TraLoi,
                    ChuDe     = d.ChuDe,
                    Embedding = EmbeddingUtils.ToFloats(d.Embedding)!
                })
                .Where(x => x.Embedding != null)
                .ToList();

            _logger.LogInformation("ChatbotService cache: {Count} FAQ (model={Model}).",
                _cache.Count, currentModel);
        }
        finally { _lock.Release(); }
    }

    public int CacheCount => _cache.Count;

    // ── Background Re-index ───────────────────────────────────────────────────
    // Batch 30 dòng/request, delay 300ms, resume (chỉ dòng chưa có model hiện tại).
    public async Task RunReIndexAsync()
    {
        const int BatchSize = 30;
        const int DelayMs   = 300;

        try
        {
            using var scope   = _scopeFactory.CreateScope();
            var db            = scope.ServiceProvider.GetRequiredService<DatPhongKhachSanContext>();
            var embedProvider = scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>();
            var aiCfg         = scope.ServiceProvider
                                     .GetRequiredService<Microsoft.Extensions.Options.IOptions<AIConfig>>()
                                     .Value;
            var targetModel   = aiCfg.EmbeddingModel;

            // RESUME: bỏ qua dòng đã có đúng model
            var ds = await db.DoanVanBans
                .Where(d => d.ModelEmbedding == null || d.ModelEmbedding != targetModel)
                .OrderBy(d => d.MaDoan)
                .ToListAsync();

            Progress.Total       = ds.Count;
            Progress.LastMessage = $"Bắt đầu: {ds.Count} dòng cần index (model: {targetModel})";
            _logger.LogInformation("[REINDEX] Bắt đầu: {Count} dòng, model={Model}",
                ds.Count, targetModel);

            int totalBatches = (int)Math.Ceiling(ds.Count / (double)BatchSize);

            for (int i = 0; i < ds.Count; i += BatchSize)
            {
                var batch = ds.Skip(i).Take(BatchSize).ToList();
                var texts = batch.Select(d => d.CauHoi).ToList();

                var vectors = await embedProvider.EmbedBatchAsync(texts);

                int ok = 0, fail = 0;
                for (int j = 0; j < batch.Count; j++)
                {
                    var vec = j < vectors.Count ? vectors[j] : Array.Empty<float>();
                    if (vec.Length > 0)
                    {
                        batch[j].Embedding      = EmbeddingUtils.ToBytes(vec);
                        batch[j].ModelEmbedding = targetModel;
                        batch[j].NgayIndex      = DateTime.Now;
                        ok++;
                    }
                    else { fail++; Progress.Failed++; }
                    Progress.Done++;
                }

                await db.SaveChangesAsync();

                int batchNum = i / BatchSize + 1;
                Progress.LastMessage = $"Batch {batchNum}/{totalBatches}: {ok} OK, {fail} lỗi - " +
                                       $"Tổng: {Progress.Done}/{Progress.Total}";
                _logger.LogInformation("[REINDEX] Batch {B}/{TB}: {OK} ok, {Fail} lỗi. {Done}/{Total}",
                    batchNum, totalBatches, ok, fail, Progress.Done, Progress.Total);

                if (i + BatchSize < ds.Count)
                    await Task.Delay(DelayMs);
            }

            await LoadCacheAsync();
            Progress.LastMessage = Progress.Failed == 0
                ? $"Xong: {Progress.Total} embedding thành công ({targetModel})."
                : $"Xong: {Progress.Total - Progress.Failed} thành công, {Progress.Failed} thất bại.";
            _logger.LogInformation("[REINDEX] Hoàn tất: {Done}/{Total}, failed={Failed}",
                Progress.Done, Progress.Total, Progress.Failed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REINDEX] Lỗi");
            Progress.LastMessage = "Lỗi: " + ex.Message;
        }
        finally
        {
            Progress.IsRunning = false;
        }
    }

    // ── Cosine similarity ─────────────────────────────────────────────────────
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        float dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot  += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        float denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denom == 0 ? 0 : dot / denom;
    }

    // ── Phân loại intent câu hỏi (keyword-based) ─────────────────────────────
    // Nguyên tắc: chỉ route sang DB-intent khi câu hỏi RÕ RÀNG về nghiệp vụ đó.
    // Từ khoá mơ hồ (nhận phòng, check in...) để FAQ xử lý qua cosine similarity.
    public static ChatIntent PhanLoaiIntent(string text)
    {
        var t = text.ToLowerInvariant();

        // GiaPhong: hỏi về giá phòng lưu trú - từ khoá phải kèm ngữ cảnh "phòng" hoặc "đêm".
        // Bỏ "giá bao nhiêu" (quá chung - "Dịch vụ giặt ủi giá bao nhiêu" sẽ bị bắt sai).
        if (Has(t, "giá phòng", "bao nhiêu tiền", "phí phòng",
                   "tiền phòng", "mức giá", "giá thuê", "giá 1 đêm", "giá một đêm",
                   "giá cả phòng", "phòng giá bao"))
            return ChatIntent.GiaPhong;

        // PhongTrong: chỉ route khi hỏi tình trạng phòng CÒN HAY HẾT - KHÔNG dùng
        // "nhận phòng" / "check in" vì chúng cũng xuất hiện trong FAQ về quy trình/giờ.
        if (Has(t, "còn phòng", "phòng trống", "có phòng không", "phòng còn",
                   "đang trống", "phòng nào trống", "còn chỗ", "hết phòng",
                   "muốn đặt phòng ngay", "đặt phòng hôm nay", "hôm nay có phòng",
                   "ngày nào còn phòng", "ngày nào trống"))
            return ChatIntent.PhongTrong;

        // DichVu: chỉ route khi câu hỏi rõ ràng về danh sách/tình trạng dịch vụ.
        // KHÔNG dùng từ đơn như "ăn sáng", "hồ bơi", "gym" - chúng thường xuất hiện
        // trong câu hỏi về chính sách/giờ mở cửa nên để FAQ xử lý qua cosine similarity.
        if (Has(t, "dịch vụ", "spa", "đưa đón", "giặt ủi", "giặt là",
                   "laundry", "thuê xe", "xe đưa đón"))
            return ChatIntent.DichVu;

        // DonCuaToi: hỏi về đơn đặt phòng của chính mình
        if (Has(t, "đơn của tôi", "đơn đặt phòng của", "booking của tôi",
                   "lịch sử đặt", "tôi đã đặt", "xem đơn", "đơn hàng của tôi",
                   "phòng tôi đã đặt", "đặt của tôi"))
            return ChatIntent.DonCuaToi;

        return ChatIntent.FAQ;
    }

    private static bool Has(string text, params string[] keywords)
        => keywords.Any(text.Contains);

    // ── Tìm FAQ khớp nhất (dùng ở Phase 11) ─────────────────────────────────
    public (FaqCacheItem? Item, float Score) TimFaqTotNhat(float[] queryEmbed, float threshold = 0.6f)
    {
        if (_cache.Count == 0) return (null, 0);

        FaqCacheItem? best = null;
        float bestScore   = 0;
        foreach (var item in _cache)
        {
            float score = CosineSimilarity(queryEmbed, item.Embedding);
            if (score > bestScore) { bestScore = score; best = item; }
        }
        return bestScore >= threshold ? (best, bestScore) : (null, bestScore);
    }

    // ── LIKE fallback khi embedding service lỗi ──────────────────────────────
    // Degraded mode: tìm theo từ khoá trong CauHoi/TraLoi thay vì cosine similarity.
    // Kết quả kém hơn nhưng chatbot không chết khi Cloudflare embed down.
    public async Task<IReadOnlyList<FaqCacheItem>> TimFaqLikeAsync(string query, int maxResults = 3)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Data.DatPhongKhachSanContext>();

            // Thử tìm toàn cụm trước
            var rows = await db.DoanVanBans
                .Where(d => d.CauHoi.Contains(query) || d.TraLoi.Contains(query))
                .Take(maxResults)
                .ToListAsync();

            // Nếu không có, thử từng từ dài (>2 ký tự)
            if (rows.Count == 0)
            {
                var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                 .Where(w => w.Length > 2).ToList();
                if (words.Count > 0)
                {
                    var word = words[0];
                    rows = await db.DoanVanBans
                        .Where(d => d.CauHoi.Contains(word) || d.TraLoi.Contains(word))
                        .Take(maxResults)
                        .ToListAsync();
                }
            }

            _logger.LogWarning("[FAQ-LIKE] Fallback LIKE '{Q}' => {N} kết quả", query, rows.Count);
            return rows.Select(d => new FaqCacheItem
            {
                MaDoan    = d.MaDoan,
                CauHoi    = d.CauHoi,
                TraLoi    = d.TraLoi,
                ChuDe     = d.ChuDe,
                Embedding = Array.Empty<float>()
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FAQ-LIKE] Lỗi LIKE fallback");
            return Array.Empty<FaqCacheItem>();
        }
    }

    // ── Tìm top N FAQ (dùng bởi TimFaqTool Phase 19) ─────────────────────────
    public IReadOnlyList<(FaqCacheItem Item, float Score)> TimFaqTop(
        float[] queryEmbed, int topN = 3, float threshold = 0.4f)
    {
        if (_cache.Count == 0) return Array.Empty<(FaqCacheItem, float)>();
        return _cache
            .Select(item => (item, Score: CosineSimilarity(queryEmbed, item.Embedding)))
            .Where(x => x.Score >= threshold)
            .OrderByDescending(x => x.Score)
            .Take(topN)
            .ToList();
    }
}

public class FaqCacheItem
{
    public int MaDoan { get; set; }
    public string CauHoi { get; set; } = string.Empty;
    public string TraLoi { get; set; } = string.Empty;
    public string? ChuDe { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
