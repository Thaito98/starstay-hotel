using System.Globalization;
using System.Text.Json;
using DatPhongKhachSan.Services;

namespace DatPhongKhachSan.Services.Chatbot.Tools;

public class KiemTraPhongTrongTool : ITool
{
    private readonly DatPhongService _datPhong;
    private readonly ILogger<KiemTraPhongTrongTool> _logger;

    public KiemTraPhongTrongTool(DatPhongService datPhong, ILogger<KiemTraPhongTrongTool> logger)
    {
        _datPhong = datPhong;
        _logger   = logger;
    }

    public string Ten => "kiem_tra_phong_trong";

    public string MoTa =>
        "Kiểm tra số phòng còn trống theo loại phòng trong khoảng ngày cụ thể. " +
        "Chỉ gọi khi có NGÀY nhận và NGÀY trả cụ thể. " +
        "Nếu khách chưa cho ngày, KHÔNG gọi tool này - hãy hỏi lại khách ngày nhận và ngày trả.";

    public object ThamSoSchema => new
    {
        type = "object",
        properties = new
        {
            ngay_nhan = new
            {
                type        = "string",
                description = "Ngày nhận phòng. Dùng YYYY-MM-DD khi biết rõ năm (vd 2026-08-20); dùng D/M khi không có năm (vd 20/8) - backend sẽ tự suy năm. BẮT BUỘC."
            },
            ngay_tra = new
            {
                type        = "string",
                description = "Ngày trả phòng. Dùng YYYY-MM-DD khi biết rõ năm (vd 2026-08-22); dùng D/M khi không có năm (vd 22/8). BẮT BUỘC."
            },
            loai_phong = new
            {
                type        = "string",
                description = "Tên loại phòng cần lọc (ví dụ: Deluxe). Không bắt buộc - bỏ trống để xem tất cả."
            }
        },
        required = new[] { "ngay_nhan", "ngay_tra" }
    };

    public bool CanDangNhap => false;

    // Parses ngày từ chuỗi model gửi vào.
    // Formats hỗ trợ: yyyy-MM-dd (ISO), d/M/yyyy, dd/MM/yyyy (có năm), d/M, dd/MM (thiếu năm).
    // Input có năm rõ -> giữ nguyên năm.
    // Input thiếu năm -> năm gần nhất tương lai (năm nay nếu chưa qua, năm sau nếu đã qua).
    // Returns null khi parse thất bại.
    private static DateOnly? ParseNgay(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = input.Trim();
        var vn = CultureInfo.GetCultureInfo("vi-VN");

        // Formats có năm - giữ nguyên năm đã có
        string[] fullFormats = { "yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy" };
        if (DateTime.TryParseExact(s, fullFormats, vn, DateTimeStyles.None, out var full))
            return DateOnly.FromDateTime(full);

        // Formats thiếu năm - suy năm gần nhất tương lai
        string[] shortFormats = { "d/M", "dd/MM" };
        if (DateTime.TryParseExact(s, shortFormats, vn, DateTimeStyles.None, out var partial))
        {
            var today = DateOnly.FromDateTime(ThoiGian.Today);
            try
            {
                // Rebuild dùng today.Year để không phụ thuộc vào năm TryParseExact tự gán
                var candidate = new DateOnly(today.Year, partial.Month, partial.Day);
                return candidate >= today ? candidate : candidate.AddYears(1);
            }
            catch { return null; } // invalid date (vd 30/2)
        }

        return null;
    }

    public async Task<string> ThucThiAsync(JsonElement thamSo, ToolContext ctx, CancellationToken ct = default)
    {
        var rawNhan = thamSo.TryGetProperty("ngay_nhan", out var pNhan) &&
                      pNhan.ValueKind == JsonValueKind.String
                      ? pNhan.GetString() : null;

        var rawTra = thamSo.TryGetProperty("ngay_tra", out var pTra) &&
                     pTra.ValueKind == JsonValueKind.String
                     ? pTra.GetString() : null;

        var ngayNhan = ParseNgay(rawNhan);
        if (ngayNhan == null)
            return "không hiểu ngày nhận, vui lòng nhập dạng 20/8/2026 hoặc 2026-08-20.";

        var ngayTra = ParseNgay(rawTra);
        if (ngayTra == null)
            return "không hiểu ngày trả, vui lòng nhập dạng 22/8/2026 hoặc 2026-08-22.";

        _logger.LogInformation("[PHONG-TRONG] raw: nhan='{RN}' tra='{RT}' | parsed: nhan={PN} tra={PT}",
            rawNhan, rawTra, ngayNhan, ngayTra);

        var homNay = DateOnly.FromDateTime(ThoiGian.Today);

        if (ngayNhan < homNay)
            return "không đặt được ngày trong quá khứ.";
        if (ngayTra <= ngayNhan)
            return "ngày trả phải sau ngày nhận.";
        if (ngayTra.Value.DayNumber - ngayNhan.Value.DayNumber > 30)
            return "chỉ đặt tối đa 30 đêm.";

        // Optional loai_phong filter
        string? loaiPhong = null;
        if (thamSo.TryGetProperty("loai_phong", out var pLoai) &&
            pLoai.ValueKind == JsonValueKind.String)
        {
            var v = pLoai.GetString();
            if (!string.IsNullOrWhiteSpace(v))
                loaiPhong = v.Trim();
        }

        var ds = await _datPhong.DemPhongTrongTheoLoai(ngayNhan.Value, ngayTra.Value);

        IEnumerable<PhongTrongItem> filtered = ds;
        if (loaiPhong != null)
            filtered = ds.Where(x =>
                x.TenLoai.Contains(loaiPhong, StringComparison.OrdinalIgnoreCase));

        var list = filtered.ToList();
        if (list.Count == 0)
            return loaiPhong == null
                ? "Không có loại phòng nào trong hệ thống."
                : $"Không tìm thấy loại phòng '{loaiPhong}'.";

        var ketQua = list.Select(x => new
        {
            loai_phong     = x.TenLoai,
            gia            = x.Gia,
            so_phong_trong = x.SoPhongTrong,
            trang_thai     = x.SoPhongTrong > 0 ? "còn phòng" : "hết phòng"
        });

        return JsonSerializer.Serialize(ketQua, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
}
