using System.Collections.Concurrent;
using DatPhongKhachSan.Areas.Identity.Data;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace DatPhongKhachSan.Tests.Integration;

/// <summary>
/// Integration test: 10 request đặt phòng đồng thời, chỉ 1 phòng trống.
/// Yêu cầu SQL Server thật - chạy bằng:
///   dotnet test --filter "ConcurrentBookingTests" -v normal
/// Để override connection string: set TEST_SQL_CONNSTR=... rồi chạy test.
/// </summary>
public class ConcurrentBookingTests
{
    private readonly ITestOutputHelper _out;

    // Connection string từ appsettings.json - override bằng env var TEST_SQL_CONNSTR nếu cần
    private static readonly string ConnStr =
        Environment.GetEnvironmentVariable("TEST_SQL_CONNSTR")
        ?? "Data Source=DESKTOP-JVG8MFT;Initial Catalog=DatPhongKhachSan;"
         + "Integrated Security=True;Trust Server Certificate=True;MultipleActiveResultSets=true";

    public ConcurrentBookingTests(ITestOutputHelper output) => _out = output;

    [Fact]
    [Trait("Category", "RequiresDB")]
    public async Task DatPhong_10DongThoi_ChiChoPhep1ThanhCong()
    {
        // ── Kiểm tra kết nối trước ────────────────────────────────────────────
        try
        {
            await using var probe = MakeDb();
            await probe.Database.OpenConnectionAsync();
        }
        catch (Exception ex)
        {
            _out.WriteLine($"[SKIP] Không kết nối được SQL Server: {ex.Message}");
            _out.WriteLine($"Connection string: {ConnStr}");
            _out.WriteLine("Chạy test trên máy có SQL Server và set TEST_SQL_CONNSTR nếu cần.");
            // Không Assert.Skip (xUnit 2 chưa có) - throw để fail rõ lý do
            throw new InvalidOperationException($"SQL Server không khả dụng: {ex.Message}", ex);
        }

        // ── Seed: 1 LoaiPhong + đúng 1 Phong ─────────────────────────────────
        var testId   = Guid.NewGuid().ToString("N")[..8];
        var ngayNhan = DateOnly.FromDateTime(DateTime.Today.AddDays(90));
        var ngayTra  = DateOnly.FromDateTime(DateTime.Today.AddDays(93));

        int maLoaiPhong, maPhong;

        await using (var db = MakeDb())
        {
            var loai = new LoaiPhong
            {
                TenLoaiPhong    = $"TEST-{testId}",
                GiaCoBan        = 500_000m,
                SucChuaNguoiLon = 2,
                SucChuaTreEm    = 0,
                TrangThai       = true,
                NgayTao         = DateTime.Now
            };
            db.LoaiPhongs.Add(loai);
            await db.SaveChangesAsync();
            maLoaiPhong = loai.MaLoaiPhong;

            var phong = new Phong
            {
                SoPhong     = $"T{testId}",
                Tang        = 99,
                MaLoaiPhong = maLoaiPhong,
                TrangThai   = "Trong",
                NgayTao     = DateTime.Now
            };
            db.Phongs.Add(phong);
            await db.SaveChangesAsync();
            maPhong = phong.MaPhong;
        }

        _out.WriteLine($"Seed xong: LoaiPhong={maLoaiPhong}, Phong={maPhong} (id={testId})");
        _out.WriteLine($"Ngày test: {ngayNhan} => {ngayTra}");
        _out.WriteLine($"Bắn 10 request đồng thời...\n");

        // ── 10 concurrent bookings, mỗi cái dùng DbContext riêng ─────────────
        var results = new ConcurrentBag<(int Id, string? KetQua, Exception? Ex)>();

        var tasks = Enumerable.Range(1, 10).Select(i =>
        {
            int id = i;
            return Task.Run(async () =>
            {
                try
                {
                    var kq = await ThucHienDatPhongAsync(
                        ConnStr, maLoaiPhong, ngayNhan, ngayTra, $"KH{id:D2}-{testId}");
                    results.Add((id, kq, null));
                }
                catch (Exception ex)
                {
                    results.Add((id, null, ex));
                }
            });
        }).ToList();

        await Task.WhenAll(tasks);

        // ── In kết quả từng request ───────────────────────────────────────────
        foreach (var (id, kq, ex) in results.OrderBy(r => r.Id))
        {
            if (ex is not null)
                _out.WriteLine($"  Request {id:D2}: UNHANDLED {ex.GetType().Name}: {ex.Message}");
            else
                _out.WriteLine($"  Request {id:D2}: {kq}");
        }

        int soThanhCong = results.Count(r => r.KetQua == "ThanhCong");
        int soThatBai   = results.Count(r => r.KetQua is "HetPhong" or "TranhChap");
        int soException = results.Count(r => r.Ex is not null);

        _out.WriteLine($"\n--- KẾT QUẢ ---");
        _out.WriteLine($"ThanhCong             : {soThanhCong}");
        _out.WriteLine($"HetPhong + TranhChap  : {soThatBai}");
        _out.WriteLine($"Unhandled Exception   : {soException}");

        // ── Verify DB: không có 2 đơn active cùng phòng trùng ngày ──────────
        await using (var db = MakeDb())
        {
            var chiTietCount = await db.ChiTietDatPhongs
                .Where(ct => ct.MaPhong == maPhong)
                .CountAsync();

            var activeCount = await db.DatPhongs
                .Where(d => d.ChiTietDatPhongs.Any(ct => ct.MaPhong == maPhong)
                         && d.TrangThai != "DaHuy"
                         && d.TrangThai != "DaTraPhong"
                         && d.NgayNhanPhong < ngayTra
                         && d.NgayTraPhong > ngayNhan)
                .CountAsync();

            _out.WriteLine($"\n--- VERIFY DB ---");
            _out.WriteLine($"ChiTietDatPhong cho phòng test : {chiTietCount}");
            _out.WriteLine($"Đơn active trùng ngày          : {activeCount}");
            _out.WriteLine($"(mong đợi: ChiTietDatPhong=1, Đơn active=1)");

            // Cleanup trước khi assert - để DB không bị dơ dù test fail
            await CleanupAsync(db, maLoaiPhong, maPhong);

            Assert.Equal(0, soException);
            Assert.Equal(1, soThanhCong);
            Assert.Equal(9, soThatBai);
            Assert.Equal(1, chiTietCount);
            Assert.Equal(1, activeCount);
        }
    }

    // ── Core logic: mỗi call = 1 DbContext riêng (giống 1 HTTP request scope) ─
    private static async Task<string> ThucHienDatPhongAsync(
        string connStr, int maLoaiPhong, DateOnly ngayNhan, DateOnly ngayTra, string tenKhach)
    {
        var options = new DbContextOptionsBuilder<DatPhongKhachSanContext>()
            .UseSqlServer(connStr,
                sql => sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null))
            .Options;

        await using var db  = new DatPhongKhachSanContext(options);
        var svc             = new DatPhongService(db);
        var ketQua          = KetQuaDat.ThanhCong;

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);
            try
            {
                // Re-check bên trong transaction - cùng pattern với DatPhongController
                var phong = await svc.TimPhongTrong(maLoaiPhong, ngayNhan, ngayTra);
                if (phong == null)
                {
                    ketQua = KetQuaDat.HetPhong;
                    await tx.RollbackAsync();
                    return;
                }

                var nguoiDung = new NguoiDung
                {
                    UserId      = null,
                    HoTen       = tenKhach,
                    SoDienThoai = "0900000000",
                    VaiTro      = "KhachHang",
                    NgayTao     = DateTime.Now
                };
                db.NguoiDungs.Add(nguoiDung);
                await db.SaveChangesAsync();

                var don = new DatPhong
                {
                    MaNguoiDung    = nguoiDung.MaNguoiDung,
                    // Dùng GUID để tránh collision khi 10 request cùng giây
                    MaDon          = "DP" + Guid.NewGuid().ToString("N")[..18],
                    NguonDat       = "Online",
                    NgayNhanPhong  = ngayNhan,
                    NgayTraPhong   = ngayTra,
                    TongSoNguoiLon = 1,
                    TongSoTreEm    = 0,
                    LoaiThanhToan  = "ThanhToanDu",
                    TrangThai      = "ChoXacNhan",
                    NgayDat        = DateTime.Now
                };
                db.DatPhongs.Add(don);
                await db.SaveChangesAsync();

                db.ChiTietDatPhongs.Add(new ChiTietDatPhong
                {
                    MaDatPhong     = don.MaDatPhong,
                    MaPhong        = phong.MaPhong,
                    SoNguoiLon     = 1,
                    SoTreEm        = 0,
                    GiaMotDem      = 500_000m,
                    TenKhachLuuTru = tenKhach
                });
                await db.SaveChangesAsync();

                await tx.CommitAsync();
            }
            catch (Exception ex) when (LaLoiTranhChap(ex))
            {
                try { await tx.RollbackAsync(); } catch { }
                ketQua = KetQuaDat.TranhChap;
            }
            // Lỗi khác (kết nối, timeout network...) không catch - để strategy retry
        });

        return ketQua.ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private enum KetQuaDat { ThanhCong, HetPhong, TranhChap }

    private static bool LaLoiTranhChap(Exception ex) =>
        (ex is Microsoft.EntityFrameworkCore.DbUpdateException due
            && due.InnerException is SqlException s1
            && (s1.Number == 1205 || s1.Number == 1222))
        || (ex is SqlException s2 && (s2.Number == 1205 || s2.Number == 1222));

    private static DatPhongKhachSanContext MakeDb() =>
        new(new DbContextOptionsBuilder<DatPhongKhachSanContext>()
            .UseSqlServer(ConnStr)
            .Options);

    private async Task CleanupAsync(DatPhongKhachSanContext db, int maLoaiPhong, int maPhong)
    {
        var donIds = await db.ChiTietDatPhongs
            .Where(ct => ct.MaPhong == maPhong)
            .Select(ct => ct.MaDatPhong)
            .ToListAsync();

        var chiTietIds = await db.ChiTietDatPhongs
            .Where(ct => ct.MaPhong == maPhong)
            .Select(ct => ct.MaChiTiet)
            .ToListAsync();

        var nguoiDungIds = donIds.Count > 0
            ? await db.DatPhongs
                .Where(d => donIds.Contains(d.MaDatPhong))
                .Select(d => d.MaNguoiDung)
                .ToListAsync()
            : new List<int>();

        await db.ChiTietDichVus
            .Where(dv => chiTietIds.Contains(dv.MaChiTiet))
            .ExecuteDeleteAsync();
        await db.ChiTietDatPhongs
            .Where(ct => ct.MaPhong == maPhong)
            .ExecuteDeleteAsync();
        await db.ThanhToans
            .Where(tt => donIds.Contains(tt.MaDatPhong))
            .ExecuteDeleteAsync();
        await db.DatPhongs
            .Where(d => donIds.Contains(d.MaDatPhong))
            .ExecuteDeleteAsync();
        if (nguoiDungIds.Count > 0)
            await db.NguoiDungs
                .Where(nd => nguoiDungIds.Contains(nd.MaNguoiDung))
                .ExecuteDeleteAsync();
        await db.Phongs
            .Where(p => p.MaPhong == maPhong)
            .ExecuteDeleteAsync();
        await db.LoaiPhongs
            .Where(lp => lp.MaLoaiPhong == maLoaiPhong)
            .ExecuteDeleteAsync();

        _out.WriteLine("Cleanup xong.");
    }
}
