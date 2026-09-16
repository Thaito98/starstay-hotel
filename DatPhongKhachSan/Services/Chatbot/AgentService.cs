using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DatPhongKhachSan.Services;
using DatPhongKhachSan.Services.Chatbot.Tools;

namespace DatPhongKhachSan.Services.Chatbot;

public class AgentService
{
    // ── PII output filter ─────────────────────────────────────────────────────
    // \b đảm bảo không match số nằm giữa chuỗi alnum (mã đơn, timestamp).
    // Giá tiền có dấu phẩy (1,500,000) không match vì phẩy phá word boundary.
    private static readonly Regex ReCccd = new(@"\b\d{12}\b",    RegexOptions.Compiled);
    private static readonly Regex ReSdt  = new(@"\b0\d{9,10}\b", RegexOptions.Compiled);

    // ── Fast-path: câu đơn giản => gọi tool trực tiếp, không qua LLM ─────────
    // ReFpGiaPhong: khớp khi hỏi giá phòng mà KHÔNG có ngày cụ thể.
    // ReFpCoNgay:   loại trừ câu có ngày tháng hoặc từ "còn trống" => phải dùng agentic.
    // ReFpDichVu:   hỏi danh sách dịch vụ của khách sạn.
    private static readonly Regex ReFpGiaPhong = new(
        @"(bảng\s*giá|giá\s+(các\s+|tất\s+cả\s+)?phòng|(phòng|loại\s+phòng).{0,12}(giá|bao\s+nhiêu)|(giá|bao\s+nhiêu).{0,12}phòng)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReFpCoNgay = new(
        @"\d{1,2}/\d{1,2}|\bngày\b|\bhôm\s*nay\b|\bngày\s*mai\b|\bmai\b|\btuần\b|\btháng\b|còn\s*trống|còn\s*phòng|kiểm\s*tra",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReFpDichVu = new(
        @"(có\s+(những\s+|các\s+)?dịch\s+vụ(\s+(gì|nào))?|dịch\s+vụ\s+(gì|nào|của\s+khách\s+sạn)|(khách\s+sạn|bạn)\s+có\s+(những\s+|các\s+)?dịch\s+vụ)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Che 4 số giữa: "0912345678"  => "0912***678"
    public static string LocPII(string text)
    {
        text = ReCccd.Replace(text, "***");
        text = ReSdt.Replace(text, m =>
        {
            var s = m.Value;
            return s[..4] + "***" + s[^3..];
        });
        return text;
    }

    private const string SystemPrompt =
        "Bạn là trợ lý khách sạn StarStay Đà Nẵng. " +
        "LUÔN trả lời bằng tiếng Việt. Không dùng bất kỳ ngôn ngữ nào khác. " +
        "Bạn KHÔNG biết giá phòng, tình trạng phòng, dịch vụ hay đơn đặt. " +
        "Mọi thông tin đó PHẢI lấy từ tool. " +
        "TUYỆT ĐỐI không tự trả lời các câu về giá/phòng/dịch vụ mà chưa gọi tool. " +
        "Nếu câu hỏi liên quan giá hoặc so sánh giá phòng -> BẮT BUỘC gọi tra_gia_phong trước. " +
        "QUY TẮC NGÀY cho kiem_tra_phong_trong: " +
        "nếu khách cho ngày CÓ NĂM (vd '20/8/2026' hay '2026-08-20') -> gửi YYYY-MM-DD; " +
        "nếu ngày KHÔNG CÓ NĂM (vd '20/8' hay '5/2') -> gửi D/M (vd '20/8', '5/2'), KHÔNG tự thêm năm; " +
        "backend sẽ tự suy năm đúng. KHÔNG hỏi lại năm. " +
        "Khi khách đã cho ngày nhận VÀ ngày trả -> BẮT BUỘC gọi kiem_tra_phong_trong NGAY. " +
        "Nếu khách cung cấp ngày nhận và ngày trả qua nhiều tin nhắn, hãy ghi nhớ và kết hợp lại. " +
        "Khi đã đủ ngày nhận VÀ ngày trả (dù ở các tin nhắn khác nhau), gọi kiem_tra_phong_trong ngay, không hỏi lại thông tin đã có. " +
        "loai_phong là tuỳ chọn - KHÔNG hỏi lại loại phòng. " +
        "Nếu đã có đủ thông tin từ tool, trả lời NGAY, KHÔNG gọi thêm tool. " +
        "Nếu không có thông tin từ tool, nói không biết và mời liên hệ lễ tân - KHÔNG bịa. " +
        "Trả lời NGẮN GỌN: tối đa 3-4 câu hoặc 4-5 gạch đầu dòng, không lặp lại thông tin, không văn vòng vo. " +
        "Trình bày bằng gạch đầu dòng hoặc câu văn - KHÔNG dùng bảng markdown (ký tự |).";

    private readonly IChatProvider _chat;
    private readonly ToolRegistry _registry;
    private readonly ILogger<AgentService> _logger;

    public AgentService(IChatProvider chat, ToolRegistry registry, ILogger<AgentService> logger)
    {
        _chat     = chat;
        _registry = registry;
        _logger   = logger;
    }

    public async Task<string> RunAsync(
        string cauHoi,
        ToolContext ctx,
        IEnumerable<(string VaiTro, string NoiDung)>? lichSu = null,
        CancellationToken ct = default)
    {
        var tools = _registry.BuildToolSchemas();
        var today = ThoiGian.Today;
        var systemContent = SystemPrompt +
            $"\nHôm nay là {today:yyyy-MM-dd}, {ThuTiengViet(today.DayOfWeek)}.";
        var messages = new List<object>
        {
            new { role = "system", content = systemContent }
        };

        // Ghép 3 lượt gần nhất (user + bot xen kẽ) trước câu hỏi hiện tại
        if (lichSu != null)
            foreach (var (vaiTro, noiDung) in lichSu)
                messages.Add(new { role = vaiTro == "User" ? "user" : "assistant", content = noiDung });

        messages.Add(new { role = "user", content = cauHoi });

        var swTotal = Stopwatch.StartNew();
        _logger.LogInformation("[AGENT] Bắt đầu - câu hỏi: {Q}", cauHoi);

        // Fast-path: câu đơn giản rõ ràng => gọi tool + format template, bỏ qua LLM
        var fastResult = await TryFastPathAsync(cauHoi, ctx, swTotal, ct);
        if (fastResult != null)
            return fastResult;

        for (int round = 1; round <= _registry.MaxToolRounds; round++)
        {
            _logger.LogInformation("[AGENT] ── Round {R} ─────────────────────────", round);

            var swLlm = Stopwatch.StartNew();
            var kq = await _chat.ChatWithToolsAsync(
                messages.AsReadOnly(), tools.ToList(), ct);
            swLlm.Stop();

            // Model trả text - lọc PII rồi return
            if (!kq.ToolCalls.Any())
            {
                var reply = LocPII(kq.NoiDung
                    ?? "Xin lỗi, chưa trả lời được, vui lòng liên hệ lễ tân.");
                _logger.LogInformation("[AGENT] Round {R}: text reply len={L} | llm={Llm}ms total={Tot}ms",
                    round, reply.Length, swLlm.ElapsedMilliseconds, swTotal.ElapsedMilliseconds);
                return reply;
            }

            _logger.LogInformation("[AGENT] Round {R}: llm={Llm}ms | gọi {N} tool(s): {Names}",
                round, swLlm.ElapsedMilliseconds, kq.ToolCalls.Count,
                string.Join(", ", kq.ToolCalls.Select(t => t.Name)));

            // 1. Append assistant message chứa tool_calls TRƯỚC
            // content="" thay vì null: một số model (gpt-oss-20b) validate schema chặt, từ chối null.
            messages.Add(new
            {
                role       = "assistant",
                content    = "",
                tool_calls = kq.ToolCalls.Select(tc => new
                {
                    id       = tc.Id,
                    type     = "function",
                    function = new { name = tc.Name, arguments = tc.ArgumentsJson }
                }).ToList()
            });

            // 2. Thực thi từng tool và append kết quả
            foreach (var tc in kq.ToolCalls)
            {
                _logger.LogInformation("[AGENT]    => tool={Name}, args={Args}",
                    tc.Name, tc.ArgumentsJson);

                var tool   = _registry.TimTool(tc.Name);
                string result;

                if (tool == null)
                {
                    result = $"Tool '{tc.Name}' không tồn tại trong registry.";
                    _logger.LogWarning("[AGENT]     tool '{Name}' không tìm thấy", tc.Name);
                }
                else if (tool.CanDangNhap && !ctx.DaDangNhap)
                {
                    result = "Tool này yêu cầu đăng nhập.";
                    _logger.LogInformation("[AGENT]     tool '{Name}' cần đăng nhập", tc.Name);
                }
                else
                {
                    var swTool = Stopwatch.StartNew();
                    try
                    {
                        JsonElement thamSo;
                        try   { thamSo = JsonDocument.Parse(tc.ArgumentsJson).RootElement; }
                        catch { thamSo = JsonDocument.Parse("{}").RootElement; }

                        result = await tool.ThucThiAsync(thamSo, ctx, ct);
                        _logger.LogInformation("[AGENT]     tool '{Name}' {Ms}ms => {Len} ký tự: {Preview}",
                            tc.Name, swTool.ElapsedMilliseconds,
                            result.Length, result[..Math.Min(120, result.Length)]);
                    }
                    catch (Exception ex)
                    {
                        // Log đầy đủ cho developer, trả message an toàn cho model/user - không để ex.Message lọt ra.
                        result = "Không thể thực thi yêu cầu ngay lúc này. Vui lòng thử lại sau.";
                        _logger.LogError(ex, "[AGENT]     tool '{Name}' throw", tc.Name);
                    }
                }

                messages.Add(new
                {
                    role         = "tool",
                    tool_call_id = tc.Id,
                    content      = result
                });
            }
        }

        _logger.LogWarning("[AGENT] Hết {Max} vòng, thử lần gọi tổng hợp cuối", _registry.MaxToolRounds);

        // Lần gọi cuối: không truyền tools - model buộc phải trả text, không gọi tool thêm.
        // Tool_calls trong response bị bỏ qua hoàn toàn (không thực thi, chỉ lấy NoiDung).
        messages.Add(new
        {
            role    = "system",
            content = "Đã đạt giới hạn gọi công cụ. Hãy trả lời dựa trên kết quả đã có. " +
                      "Nếu chưa đủ dữ liệu, nói rõ thông tin nào còn thiếu. Không gọi thêm công cụ."
        });
        try
        {
            var kqCuoi = await _chat.ChatWithToolsAsync(
                messages.AsReadOnly(), Array.Empty<object>(), ct);
            if (!string.IsNullOrWhiteSpace(kqCuoi.NoiDung))
            {
                _logger.LogInformation("[AGENT] Lần tổng hợp cuối: len={L}", kqCuoi.NoiDung.Length);
                return LocPII(kqCuoi.NoiDung);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AGENT] Lần tổng hợp cuối throw");
        }

        _logger.LogWarning("[AGENT] Dùng fallback hardcode");
        return "Xin lỗi, chưa trả lời được, vui lòng liên hệ lễ tân.";
    }

    private static string ThuTiengViet(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday    => "Thứ Hai",
        DayOfWeek.Tuesday   => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday  => "Thứ Năm",
        DayOfWeek.Friday    => "Thứ Sáu",
        DayOfWeek.Saturday  => "Thứ Bảy",
        DayOfWeek.Sunday    => "Chủ Nhật",
        _ => ""
    };

    // ── Fast-path implementation ──────────────────────────────────────────────

    private async Task<string?> TryFastPathAsync(
        string cauHoi, ToolContext ctx, Stopwatch swTotal, CancellationToken ct)
    {
        // Fast-path 1: Giá phòng (không có ngày/từ tình trạng phòng)
        if (ReFpGiaPhong.IsMatch(cauHoi) && !ReFpCoNgay.IsMatch(cauHoi))
        {
            var tool = _registry.TimTool("tra_gia_phong");
            if (tool != null)
            {
                using var argsDoc = JsonDocument.Parse("{}");
                var raw       = await tool.ThucThiAsync(argsDoc.RootElement, ctx, ct);
                var formatted = FormatGiaPhong(raw);
                if (formatted != null)
                {
                    _logger.LogInformation("[AGENT] fast-path=tra_gia_phong {Ms}ms",
                        swTotal.ElapsedMilliseconds);
                    return LocPII(formatted);
                }
            }
        }

        // Fast-path 2: Danh sách dịch vụ
        if (ReFpDichVu.IsMatch(cauHoi))
        {
            var tool = _registry.TimTool("danh_sach_dich_vu");
            if (tool != null)
            {
                using var argsDoc = JsonDocument.Parse("{}");
                var raw       = await tool.ThucThiAsync(argsDoc.RootElement, ctx, ct);
                var formatted = FormatDichVu(raw);
                if (formatted != null)
                {
                    _logger.LogInformation("[AGENT] fast-path=danh_sach_dich_vu {Ms}ms",
                        swTotal.ElapsedMilliseconds);
                    return LocPII(formatted);
                }
            }
        }

        return null; // không khớp fast-path => tiếp tục agentic
    }

    // Định dạng số tiền: 800000 => "800.000"
    private static string FormatTien(decimal gia) =>
        ((long)gia).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                   .Replace(",", ".");

    // Parse JSON từ tra_gia_phong => text dạng bullet list
    // Input: [{"ten":...,"gia_co_ban":...,"suc_chua_nguoi_lon":...,"suc_chua_tre_em":...},...]
    private static string? FormatGiaPhong(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement;
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return null;

            var sb = new StringBuilder("Bảng giá phòng khách sạn StarStay:\n");
            foreach (var r in arr.EnumerateArray())
            {
                var ten = r.GetProperty("ten").GetString() ?? "";
                var gia = r.GetProperty("gia_co_ban").GetDecimal();
                var nl  = r.GetProperty("suc_chua_nguoi_lon").GetInt32();
                var te  = r.GetProperty("suc_chua_tre_em").GetInt32();
                var phu = te > 0 ? $", {te} trẻ em" : "";
                sb.AppendLine($"- {ten}: {FormatTien(gia)} đ/đêm (tối đa {nl} người lớn{phu})");
            }
            return sb.ToString().TrimEnd();
        }
        catch { return null; }
    }

    // Parse JSON từ danh_sach_dich_vu => text dạng bullet list
    // Input: [{"ten":...,"gia":...,"don_vi":...},...]
    private static string? FormatDichVu(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement;
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return null;

            var sb = new StringBuilder("Các dịch vụ của khách sạn:\n");
            foreach (var d in arr.EnumerateArray())
            {
                var ten   = d.GetProperty("ten").GetString() ?? "";
                var gia   = d.GetProperty("gia").GetDecimal();
                var donVi = d.GetProperty("don_vi").GetString() ?? "";
                sb.AppendLine($"- {ten}: {FormatTien(gia)} đ/{donVi}");
            }
            return sb.ToString().TrimEnd();
        }
        catch { return null; }
    }
}
