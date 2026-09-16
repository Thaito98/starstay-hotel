using System.Text.Json;
using DatPhongKhachSan.Data;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Services.Chatbot.Tools;

public class DonCuaToiTool : ITool
{
    private readonly DatPhongKhachSanContext _db;
    private readonly ILogger<DonCuaToiTool> _logger;

    public DonCuaToiTool(DatPhongKhachSanContext db, ILogger<DonCuaToiTool> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public string Ten => "don_cua_toi";

    public string MoTa =>
        "Xem danh sách đơn đặt phòng của khách đang đăng nhập. " +
        "Không cần tham số - danh tính lấy từ phiên đăng nhập, không phải từ câu hỏi.";

    // Schema RỖNG: tool không nhận tham số nào từ LLM - mọi tham số bị bỏ qua.
    public object ThamSoSchema => new
    {
        type       = "object",
        properties = new { }
    };

    public bool CanDangNhap => true;

    public async Task<string> ThucThiAsync(JsonElement thamSo, ToolContext ctx, CancellationToken ct = default)
    {
        // Danh tính PHẢI lấy từ ctx - bỏ qua hoàn toàn mọi field trong thamSo
        if (ctx.MaNguoiDung == null)
        {
            _logger.LogWarning("[DON-CUA-TOI] ctx.MaNguoiDung=null, từ chối query");
            return "[]";
        }

        var maNd = ctx.MaNguoiDung.Value;
        _logger.LogInformation("[DON-CUA-TOI] query MaNguoiDung={MaNd}", maNd);

        var dons = await _db.DatPhongs
            .Include(d => d.ChiTietDatPhongs)
                .ThenInclude(ct2 => ct2.MaPhongNavigation)
                    .ThenInclude(p => p.MaLoaiPhongNavigation)
            .Where(d => d.MaNguoiDung == maNd)
            .OrderByDescending(d => d.MaDatPhong)
            .Take(5)
            .ToListAsync(ct);

        if (dons.Count == 0)
            return "[]";

        // Chỉ trả trường nghiệp vụ - KHÔNG trả CCCD, SĐT, email, địa chỉ, số tiền
        var ketQua = dons.Select(d => new
        {
            ma_don     = d.MaDon,
            loai_phong = d.ChiTietDatPhongs
                            .Select(ct2 => ct2.MaPhongNavigation?.MaLoaiPhongNavigation?.TenLoaiPhong)
                            .Where(t => t != null)
                            .Distinct()
                            .FirstOrDefault() ?? "N/A",
            ngay_nhan  = d.NgayNhanPhong.ToString("yyyy-MM-dd"),
            ngay_tra   = d.NgayTraPhong.ToString("yyyy-MM-dd"),
            trang_thai = d.TrangThai
        });

        var json = JsonSerializer.Serialize(ketQua, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        return json.Length > 800 ? json[..800] : json;
    }
}
