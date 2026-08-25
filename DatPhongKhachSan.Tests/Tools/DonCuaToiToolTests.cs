using System.Text.Json;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Services.Chatbot.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DatPhongKhachSan.Tests.Tools;

public class DonCuaToiToolTests : IDisposable
{
    private const int    MaNdA   = 1;
    private const int    MaNdB   = 2;
    private const string MaDonA  = "DPA001";
    private const string MaDonB  = "DPB001";

    private readonly DatPhongKhachSanContext _db;
    private readonly DonCuaToiTool _tool;

    public DonCuaToiToolTests()
    {
        var opts = new DbContextOptionsBuilder<DatPhongKhachSanContext>()
            .UseInMemoryDatabase("DonCuaToi_" + Guid.NewGuid())
            .Options;
        _db = new DatPhongKhachSanContext(opts);

        _db.NguoiDungs.AddRange(
            new NguoiDung { MaNguoiDung = MaNdA, HoTen = "User A", UserId = "user-a",
                            SoDienThoai = "0911111111", NgayTao = DateTime.Now },
            new NguoiDung { MaNguoiDung = MaNdB, HoTen = "User B", UserId = "user-b",
                            SoDienThoai = "0922222222", NgayTao = DateTime.Now }
        );

        _db.DatPhongs.AddRange(
            new DatPhong
            {
                MaDatPhong = 10, MaNguoiDung = MaNdA, MaDon = MaDonA,
                NgayNhanPhong = new DateOnly(2026, 9, 1), NgayTraPhong = new DateOnly(2026, 9, 3),
                SoDem = 2, TrangThai = "DaXacNhan", NguonDat = "Online",
                TongSoNguoiLon = 2, TongSoTreEm = 0, LoaiThanhToan = "Online", NgayDat = DateTime.Now
            },
            new DatPhong
            {
                MaDatPhong = 20, MaNguoiDung = MaNdB, MaDon = MaDonB,
                NgayNhanPhong = new DateOnly(2026, 9, 5), NgayTraPhong = new DateOnly(2026, 9, 7),
                SoDem = 2, TrangThai = "DaXacNhan", NguonDat = "Online",
                TongSoNguoiLon = 2, TongSoTreEm = 0, LoaiThanhToan = "Online", NgayDat = DateTime.Now
            }
        );
        _db.SaveChanges();

        _tool = new DonCuaToiTool(_db, NullLogger<DonCuaToiTool>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    // ── 1. ctx.MaNguoiDung = null => không query DB, trả rỗng ─────────────────
    [Fact]
    public async Task ThucThi_CtxNull_TraRong()
    {
        var ctx    = new ToolContext(null, null, false);
        var result = await _tool.ThucThiAsync(Parse("{}"), ctx);

        Assert.Equal("[]", result);
    }

    // ── 2. ctx của A => chỉ trả đơn A, không trả đơn B ───────────────────────
    [Fact]
    public async Task ThucThi_CtxA_ChiTraDonCuaA()
    {
        var ctx    = new ToolContext("user-a", MaNdA, true);
        var result = await _tool.ThucThiAsync(Parse("{}"), ctx);

        Assert.Contains(MaDonA, result);
        Assert.DoesNotContain(MaDonB, result);
    }

    // ── 3. LLM inject ma_nguoi_dung của B nhưng ctx là A => vẫn chỉ đơn A ─────
    // Đây là kiểm tra IDOR: thamSo có thể bị model/attacker inject, tool phải bỏ qua.
    [Fact]
    public async Task ThucThi_ThamSoChuaIdCuaB_CtxLaA_ChiTraDonCuaA()
    {
        var ctxA   = new ToolContext("user-a", MaNdA, true);
        var thamSo = Parse($"{{\"ma_nguoi_dung\": {MaNdB}, \"user_id\": \"user-b\"}}");

        var result = await _tool.ThucThiAsync(thamSo, ctxA);

        Assert.Contains(MaDonA, result);
        Assert.DoesNotContain(MaDonB, result);
    }

    // ── 4. Kết quả không chứa SĐT, PII nhạy cảm ─────────────────────────────
    [Fact]
    public async Task ThucThi_KetQua_KhongChuaPII()
    {
        var ctx    = new ToolContext("user-a", MaNdA, true);
        var result = await _tool.ThucThiAsync(Parse("{}"), ctx);

        Assert.DoesNotContain("0911111111", result);   // SĐT User A
        Assert.DoesNotContain("user-a",     result);   // UserId
        Assert.DoesNotContain("MaNguoiDung", result);  // field nội bộ
    }
}
