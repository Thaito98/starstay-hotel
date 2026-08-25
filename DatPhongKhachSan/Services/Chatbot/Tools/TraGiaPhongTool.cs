using System.Text.Json;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Services.Chatbot.Tools;

public class TraGiaPhongTool : ITool
{
    private readonly DatPhongKhachSanContext _db;

    public TraGiaPhongTool(DatPhongKhachSanContext db) => _db = db;

    public string Ten => "tra_gia_phong";

    public string MoTa =>
        "Tra cứu giá và thông tin các loại phòng của khách sạn. Dùng khi khách hỏi " +
        "về giá, loại phòng, sức chứa, diện tích.";

    public object ThamSoSchema => new
    {
        type = "object",
        properties = new
        {
            ten_loai_phong = new
            {
                type        = "string",
                description = "Tên loại phòng cần tra cứu. Nếu không cung cấp, trả tất cả loại phòng."
            }
        }
    };

    public bool CanDangNhap => false;

    public async Task<string> ThucThiAsync(JsonElement thamSo, ToolContext ctx, CancellationToken ct = default)
    {
        string? ten = null;
        if (thamSo.TryGetProperty("ten_loai_phong", out var p) &&
            p.ValueKind == JsonValueKind.String)
            ten = p.GetString();

        var ds = await _db.LoaiPhongs
            .Where(l => l.TrangThai)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(ten))
            ds = ds.Where(l => l.TenLoaiPhong.Contains(ten, StringComparison.OrdinalIgnoreCase))
                   .ToList();

        var ketQua = ds.Select(l => new
        {
            ten              = l.TenLoaiPhong,
            gia_co_ban       = l.GiaCoBan,
            suc_chua_nguoi_lon = l.SucChuaNguoiLon,
            suc_chua_tre_em  = l.SucChuaTreEm,
            loai_giuong      = l.LoaiGiuong
        });

        var json = JsonSerializer.Serialize(ketQua, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        return json.Length > 2000 ? json[..2000] : json;
    }
}
