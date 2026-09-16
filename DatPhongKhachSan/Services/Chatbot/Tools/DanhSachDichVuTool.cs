using System.Text.Json;
using DatPhongKhachSan.Data;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Services.Chatbot.Tools;

public class DanhSachDichVuTool : ITool
{
    private readonly DatPhongKhachSanContext _db;

    public DanhSachDichVuTool(DatPhongKhachSanContext db) => _db = db;

    public string Ten => "danh_sach_dich_vu";

    public string MoTa =>
        "Tra cứu danh sách dịch vụ của khách sạn (spa, đưa đón sân bay, giặt ủi, ăn uống, ...). " +
        "Dùng khi khách hỏi về dịch vụ, giá dịch vụ, hoặc khách sạn có dịch vụ gì.";

    public object ThamSoSchema => new
    {
        type = "object",
        properties = new
        {
            tu_khoa = new
            {
                type        = "string",
                description = "Từ khóa tìm kiếm dịch vụ (ví dụ: spa, đưa đón, giặt). Không bắt buộc - bỏ trống để lấy tất cả."
            }
        }
    };

    public bool CanDangNhap => false;

    public async Task<string> ThucThiAsync(JsonElement thamSo, ToolContext ctx, CancellationToken ct = default)
    {
        string? tuKhoa = null;
        if (thamSo.TryGetProperty("tu_khoa", out var p) && p.ValueKind == JsonValueKind.String)
        {
            var v = p.GetString();
            if (!string.IsNullOrWhiteSpace(v))
                tuKhoa = v.Trim();
        }

        var ds = await _db.DichVus
            .Where(d => d.TrangThai)
            .ToListAsync(ct);

        if (tuKhoa != null)
            ds = ds.Where(d =>
                    d.TenDichVu.Contains(tuKhoa, StringComparison.OrdinalIgnoreCase) ||
                    (d.MoTa != null && d.MoTa.Contains(tuKhoa, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        if (ds.Count == 0)
            return tuKhoa == null
                ? "[]"
                : $"Không tìm thấy dịch vụ nào liên quan đến \"{tuKhoa}\".";

        var ketQua = ds.Select(d => new
        {
            ten    = d.TenDichVu,
            gia    = d.Gia,
            don_vi = d.DonVi
        });

        var json = JsonSerializer.Serialize(ketQua, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        return json.Length > 2000 ? json[..2000] : json;
    }
}
