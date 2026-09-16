using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Services;

public class TinhTienResult
{
    public decimal TongTien { get; set; }
    public decimal SoTienDaTra { get; set; }
    public decimal SoTienConLai { get; set; }
    public int SoDem { get; set; }
}

public class DatPhongService
{
    private readonly DatPhongKhachSanContext _db;

    public DatPhongService(DatPhongKhachSanContext db) => _db = db;

    // ── Tính tiền đơn (CONTEXT 5.4) ──────────────────────────────────────────
    // Không lưu cột tiền => tính bằng code mỗi khi cần.
    public async Task<TinhTienResult> TinhTienDonAsync(int maDatPhong)
    {
        var don = await _db.DatPhongs
            .Include(d => d.ChiTietDatPhongs)
                .ThenInclude(ct => ct.ChiTietDichVus)
            .Include(d => d.ThanhToans)
            .FirstOrDefaultAsync(d => d.MaDatPhong == maDatPhong);

        if (don == null) return new TinhTienResult();

        int sodem = don.SoDem ?? 0;

        // Tiền phòng: Tổng tiền(GiaMotDem × SoDem)
        decimal tienPhong = don.ChiTietDatPhongs.Sum(ct => ct.GiaMotDem * sodem);

        // Tiền dịch vụ: Tổng tiền(ThanhTien từ ChiTietDichVu)
        decimal tienDV = don.ChiTietDatPhongs
            .SelectMany(ct => ct.ChiTietDichVus)
            .Sum(dv => dv.ThanhTien ?? 0);

        decimal tongTien = tienPhong + tienDV;

        // Đã trả: Tổng tiền thanh toán ThanhCong, không phải HoanTien
        decimal daTra = don.ThanhToans
            .Where(t => t.TrangThai == "ThanhCong" && t.LoaiThanhToan != "HoanTien")
            .Sum(t => t.SoTien);

        return new TinhTienResult
        {
            TongTien      = tongTien,
            SoTienDaTra   = daTra,
            SoTienConLai  = tongTien - daTra,
            SoDem         = sodem
        };
    }

    // ── Sinh mã đơn duy nhất ──────────────────────────────────────────────────
    public string SinhMaDon()
    {
        return "DP" + ThoiGian.Now.ToString("yyyyMMddHHmmss") +
               new Random().Next(100, 999).ToString();
    }

    // ── Chính sách hoàn tiền (CONTEXT 5.6) ──────────────────────────────────
    public const int NgayHoan100 = 7;   // > NgayHoan100 ngày trước nhận phòng => 100%
    public const int NgayHoan50  = 3;   // >= NgayHoan50  ngày trước nhận phòng => 50%
                                        // < NgayHoan50  ngày => 0%

    // ngayHuy: ngày thực tế hủy đơn; null => dùng hôm nay (ước tính)
    public int TinhTiLeHoan(DateOnly ngayNhanPhong, DateTime? ngayHuy = null)
    {
        var refDate   = DateOnly.FromDateTime(ngayHuy ?? ThoiGian.Today);
        int soNgayCon = ngayNhanPhong.DayNumber - refDate.DayNumber;
        if (soNgayCon > NgayHoan100) return 100;
        if (soNgayCon >= NgayHoan50) return 50;
        return 0;
    }

    // ── Tự hủy đơn ChoXacNhan hết hạn (lazy cleanup) ────────────────────────
    // Gọi trước mỗi query phòng trống / danh sách đơn.
    // Idempotent: đơn đã DaHuy không bị chạm lại.
    private const int PhanHuyPhutChoXacNhan = 15;

    public async Task HuyDonHetHanAsync()
    {
        var nguong = ThoiGian.Now.AddMinutes(-PhanHuyPhutChoXacNhan);

        var donHetHan = await _db.DatPhongs
            .Include(d => d.ThanhToans)
            .Where(d => d.TrangThai == "ChoXacNhan"
                     && d.NgayDat < nguong
                     && !d.ThanhToans.Any(t => t.TrangThai == "ThanhCong"))
            .ToListAsync();

        if (donHetHan.Count == 0) return;

        var now = ThoiGian.Now;
        foreach (var don in donHetHan)
        {
            don.TrangThai   = "DaHuy";
            don.NgayCapNhat = now;
            don.NgayHuy     = now;
            foreach (var tt in don.ThanhToans.Where(t => t.TrangThai == "ChoXuLy"))
                tt.TrangThai = "ThatBai";
        }
        await _db.SaveChangesAsync();
    }

    // ── Helper: MaPhong đang bị đặt trong khoảng ngày ───────────────────────
    // Dùng chung bởi TimPhongTrong và DemPhongTrongTheoLoai - không copy khối này.
    private IQueryable<int> MaPhongDangBan(DateOnly nhan, DateOnly tra)
        => _db.ChiTietDatPhongs
            .Where(ct => ct.MaDatPhongNavigation.TrangThai != "DaHuy"
                      && ct.MaDatPhongNavigation.TrangThai != "DaTraPhong"
                      && ct.MaDatPhongNavigation.NgayNhanPhong < tra
                      && ct.MaDatPhongNavigation.NgayTraPhong > nhan)
            .Select(ct => ct.MaPhong);

    // ── Tìm phòng trống cho 1 loại phòng trong khoảng ngày ───────────────────
    public async Task<Phong?> TimPhongTrong(int maLoaiPhong, DateOnly ngayNhan, DateOnly ngayTra)
    {
        // Dọn đơn hết hạn trước khi tìm - bỏ qua nếu đang trong transaction
        // (tránh write conflict với Serializable tx ở DatPhongController/LeTanController)
        if (_db.Database.CurrentTransaction == null)
            await HuyDonHetHanAsync();

        var biet = MaPhongDangBan(ngayNhan, ngayTra);
        return await _db.Phongs
            .Where(p => p.MaLoaiPhong == maLoaiPhong
                     && p.TrangThai == "Trong"
                     && !biet.Contains(p.MaPhong))
            .FirstOrDefaultAsync();
    }

    // ── Đếm phòng trống theo từng loại phòng (dùng bởi KiemTraPhongTrongTool) ─
    public async Task<IReadOnlyList<PhongTrongItem>> DemPhongTrongTheoLoai(DateOnly nhan, DateOnly tra)
    {
        if (_db.Database.CurrentTransaction == null)
            await HuyDonHetHanAsync();

        var biet = MaPhongDangBan(nhan, tra);

        // 2 query: đếm phòng trống per loại, rồi join loại phòng
        var counts = await _db.Phongs
            .Where(p => p.TrangThai == "Trong" && !biet.Contains(p.MaPhong))
            .GroupBy(p => p.MaLoaiPhong)
            .Select(g => new { MaLoai = g.Key, SoTrong = g.Count() })
            .ToListAsync();

        var loais = await _db.LoaiPhongs
            .Where(lp => lp.TrangThai)
            .OrderBy(lp => lp.GiaCoBan)
            .ToListAsync();

        return loais.Select(lp => new PhongTrongItem(
            lp.TenLoaiPhong,
            lp.GiaCoBan,
            counts.FirstOrDefault(c => c.MaLoai == lp.MaLoaiPhong)?.SoTrong ?? 0
        )).ToList();
    }
}

public record PhongTrongItem(string TenLoai, decimal Gia, int SoPhongTrong);
