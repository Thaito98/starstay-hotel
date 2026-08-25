using System.Text.Json;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Services.Chatbot.Tools;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatPhongKhachSan.Tests.Tools;

public class TraGiaPhongToolTests : IDisposable
{
    private readonly DatPhongKhachSanContext _db;
    private readonly TraGiaPhongTool _tool;
    private readonly ToolContext _ctx = new(UserId: null, MaNguoiDung: null, DaDangNhap: false);

    public TraGiaPhongToolTests()
    {
        var opts = new DbContextOptionsBuilder<DatPhongKhachSanContext>()
            .UseInMemoryDatabase("TraGiaPhong_" + Guid.NewGuid())
            .Options;
        _db = new DatPhongKhachSanContext(opts);

        _db.LoaiPhongs.AddRange(
            new LoaiPhong { MaLoaiPhong = 1, TenLoaiPhong = "Standard",    GiaCoBan = 800_000,   SucChuaNguoiLon = 2, SucChuaTreEm = 1, LoaiGiuong = "Đôi",   TrangThai = true,  NgayTao = DateTime.Now },
            new LoaiPhong { MaLoaiPhong = 2, TenLoaiPhong = "Deluxe",      GiaCoBan = 1_500_000, SucChuaNguoiLon = 2, SucChuaTreEm = 2, LoaiGiuong = "King",   TrangThai = true,  NgayTao = DateTime.Now },
            new LoaiPhong { MaLoaiPhong = 3, TenLoaiPhong = "Deluxe Twin", GiaCoBan = 1_600_000, SucChuaNguoiLon = 2, SucChuaTreEm = 2, LoaiGiuong = "Twin",   TrangThai = true,  NgayTao = DateTime.Now },
            new LoaiPhong { MaLoaiPhong = 4, TenLoaiPhong = "Suite",       GiaCoBan = 3_500_000, SucChuaNguoiLon = 3, SucChuaTreEm = 2, LoaiGiuong = "King",   TrangThai = true,  NgayTao = DateTime.Now },
            new LoaiPhong { MaLoaiPhong = 5, TenLoaiPhong = "Suite Ẩn",   GiaCoBan = 4_000_000, SucChuaNguoiLon = 3, SucChuaTreEm = 2, LoaiGiuong = "King",   TrangThai = false, NgayTao = DateTime.Now }
        );
        _db.SaveChanges();

        _tool = new TraGiaPhongTool(_db);
    }

    public void Dispose() => _db.Dispose();

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement;

    // ── 1. Không có tham số => trả TẤT CẢ loại phòng đang hoạt động ──────────
    [Fact]
    public async Task ThamSoRong_TraTatCaLoaiPhongHoatDong()
    {
        var result = await _tool.ThucThiAsync(Parse("{}"), _ctx);

        var arr = JsonDocument.Parse(result).RootElement;
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);

        // phải đủ 4 phòng TrangThai==true (loại trừ "Suite Ẩn" TrangThai==false)
        Assert.Equal(4, arr.GetArrayLength());

        // kiểm tra có đủ các tên
        var names = arr.EnumerateArray()
                       .Select(e => e.GetProperty("ten").GetString())
                       .ToList();
        Assert.Contains("Standard",    names);
        Assert.Contains("Deluxe",      names);
        Assert.Contains("Deluxe Twin", names);
        Assert.Contains("Suite",       names);

        // kiểm tra giá đúng
        var deluxe = arr.EnumerateArray().First(e => e.GetProperty("ten").GetString() == "Deluxe");
        Assert.Equal(1_500_000m, deluxe.GetProperty("gia_co_ban").GetDecimal());
    }

    // ── 2. Lọc theo ten_loai_phong="Deluxe" => chỉ trả Deluxe (partial, case-insensitive) ──
    [Fact]
    public async Task TenLoaiPhong_Deluxe_ChiTraLoaiDeluxe()
    {
        var result = await _tool.ThucThiAsync(
            Parse("{\"ten_loai_phong\":\"Deluxe\"}"), _ctx);

        var arr = JsonDocument.Parse(result).RootElement;
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(2, arr.GetArrayLength()); // "Deluxe" và "Deluxe Twin"

        var names = arr.EnumerateArray()
                       .Select(e => e.GetProperty("ten").GetString())
                       .ToList();
        Assert.Contains("Deluxe",      names);
        Assert.Contains("Deluxe Twin", names);
        Assert.DoesNotContain("Standard", names);
        Assert.DoesNotContain("Suite",    names);
    }

    // ── 3. ten_loai_phong="deluxe" lowercase => case-insensitive vẫn ra ────────
    [Fact]
    public async Task TenLoaiPhong_Lowercase_CaseInsensitive()
    {
        var result = await _tool.ThucThiAsync(
            Parse("{\"ten_loai_phong\":\"deluxe\"}"), _ctx);

        var arr = JsonDocument.Parse(result).RootElement;
        Assert.Equal(2, arr.GetArrayLength());
    }

    // ── 4. ten_loai_phong không tồn tại => mảng rỗng, không crash ─────────────
    [Fact]
    public async Task TenLoaiPhong_KhongTonTai_TraMangRong()
    {
        var result = await _tool.ThucThiAsync(
            Parse("{\"ten_loai_phong\":\"khong_ton_tai\"}"), _ctx);

        var arr = JsonDocument.Parse(result).RootElement;
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(0, arr.GetArrayLength());
    }

    // ── 5. Phòng TrangThai==false KHÔNG được lọt ra dù không lọc tên ─────────
    [Fact]
    public async Task PhongKhongHoatDong_KhongXuatHien()
    {
        var result = await _tool.ThucThiAsync(Parse("{}"), _ctx);

        var names = JsonDocument.Parse(result).RootElement
                                .EnumerateArray()
                                .Select(e => e.GetProperty("ten").GetString())
                                .ToList();
        Assert.DoesNotContain("Suite Ẩn", names);
    }

    // ── 6. Kiểm tra cấu trúc field JSON output ────────────────────────────────
    [Fact]
    public async Task Output_CoDay_5_Fields()
    {
        var result = await _tool.ThucThiAsync(
            Parse("{\"ten_loai_phong\":\"Standard\"}"), _ctx);

        var item = JsonDocument.Parse(result).RootElement[0];
        Assert.True(item.TryGetProperty("ten",                out _));
        Assert.True(item.TryGetProperty("gia_co_ban",         out _));
        Assert.True(item.TryGetProperty("suc_chua_nguoi_lon", out _));
        Assert.True(item.TryGetProperty("suc_chua_tre_em",    out _));
        Assert.True(item.TryGetProperty("loai_giuong",        out _));
    }
}
