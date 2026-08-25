using System.Text.Json;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatPhongKhachSan.Tests.Security;

/// <summary>
/// Seed dữ liệu canary vào real SQL Server DB trước khi chạy test class.
/// Nếu không kết nối được (CI, không có DB) => IsSeeded=false => live tests vẫn chạy nhưng bỏ qua canary assertions.
/// Cleanup tự động khi test class kết thúc.
/// </summary>
public class CanaryDbFixture : IAsyncLifetime
{
    // ── Canary strings - cố định, dùng trong assertions ─────────────────────
    public const string SdtA   = "0909123456";
    public const string CccdA  = "079999999999";
    public const string SdtB   = "0988765432";
    public const string CccdB  = "079888888888";
    public const string MaDonA = "DP-CANARY-A";
    public const string MaDonB = "DP-CANARY-B";

    /// ID thực tế do SQL Server gán - null nếu chưa seed.
    public int? MaNdA { get; private set; }
    public int? MaNdB { get; private set; }

    public bool IsSeeded { get; private set; }
    public string? SeedError { get; private set; }

    private DatPhongKhachSanContext? _db;

    public async Task InitializeAsync()
    {
        try
        {
            var cs = ReadConnectionString();
            if (string.IsNullOrWhiteSpace(cs))
            {
                SeedError = "Connection string rỗng - kiểm tra appsettings.json";
                Console.WriteLine($"[CANARY SEED FAIL] {SeedError}");
                return;
            }

            var opts = new DbContextOptionsBuilder<DatPhongKhachSanContext>()
                .UseSqlServer(cs)
                .Options;
            _db = new DatPhongKhachSanContext(opts);

            // Dọn dẹp dữ liệu cũ (chạy lại test không bị trùng MaDon)
            await CleanExistingAsync();

            // UserId=null => walk-in guest, không cần FK vào AspNetUsers
            var ndA = new NguoiDung
            {
                HoTen = "__CANARY_USER_A__", UserId = null,
                SoDienThoai = SdtA, CCCD = CccdA, NgayTao = DateTime.Now
            };
            var ndB = new NguoiDung
            {
                HoTen = "__CANARY_USER_B__", UserId = null,
                SoDienThoai = SdtB, CCCD = CccdB, NgayTao = DateTime.Now
            };
            _db.NguoiDungs.AddRange(ndA, ndB);
            await _db.SaveChangesAsync();

            MaNdA = ndA.MaNguoiDung;  // gán bởi EF sau SaveChanges
            MaNdB = ndB.MaNguoiDung;

            var nhan = DateOnly.FromDateTime(DateTime.Today.AddDays(60));
            var tra  = nhan.AddDays(2);
            _db.DatPhongs.AddRange(
                new DatPhong
                {
                    MaNguoiDung = MaNdA.Value, MaDon = MaDonA, NguonDat = "Online",
                    NgayNhanPhong = nhan, NgayTraPhong = tra, SoDem = 2,
                    TrangThai = "DaXacNhan", TongSoNguoiLon = 2, TongSoTreEm = 0,
                    LoaiThanhToan = "Online", NgayDat = DateTime.Now
                },
                new DatPhong
                {
                    MaNguoiDung = MaNdB.Value, MaDon = MaDonB, NguonDat = "Online",
                    NgayNhanPhong = nhan, NgayTraPhong = tra, SoDem = 2,
                    TrangThai = "DaXacNhan", TongSoNguoiLon = 1, TongSoTreEm = 0,
                    LoaiThanhToan = "Online", NgayDat = DateTime.Now
                }
            );
            await _db.SaveChangesAsync();
            IsSeeded = true;
            Console.WriteLine($"[CANARY] seeded - MaNdA={MaNdA}, MaNdB={MaNdB}");
        }
        catch (Exception ex)
        {
            IsSeeded = false;
            SeedError = ex.ToString();
            Console.WriteLine($"[CANARY SEED FAIL] {ex}");
        }
    }

    public async Task DisposeAsync()
    {
        if (_db == null) return;
        try
        {
            await CleanExistingAsync();
        }
        catch { /* best effort */ }
        finally
        {
            await _db.DisposeAsync();
        }
    }

    private async Task CleanExistingAsync()
    {
        if (_db == null) return;
        var dons = await _db.DatPhongs
            .Where(d => d.MaDon == MaDonA || d.MaDon == MaDonB)
            .ToListAsync();
        _db.DatPhongs.RemoveRange(dons);

        var nds = await _db.NguoiDungs
            .Where(n => n.HoTen == "__CANARY_USER_A__" || n.HoTen == "__CANARY_USER_B__")
            .ToListAsync();
        _db.NguoiDungs.RemoveRange(nds);

        await _db.SaveChangesAsync();
    }

    private static string? ReadConnectionString()
    {
        // Từ test binary (net8.0/) lên 4 cấp => solution root => app project
        var path = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                "../../../../DatPhongKhachSan/appsettings.json"));
        if (!File.Exists(path)) return null;

        using var stream = File.OpenRead(path);
        var doc = JsonDocument.Parse(stream);
        if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) &&
            cs.TryGetProperty("DefaultConnection", out var conn))
            return conn.GetString();
        return null;
    }
}
