using DatPhongKhachSan.Constants;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.ViewModels;
using DatPhongKhachSan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = ChucNang.QuanLyDon)]
public class DonDatPhongController : Controller
{
    private readonly DatPhongKhachSanContext _db;
    private readonly DatPhongService _svc;

    public DonDatPhongController(DatPhongKhachSanContext db, DatPhongService svc)
    {
        _db  = db;
        _svc = svc;
    }

    // ── GET /Admin/DonDatPhong ────────────────────────────────────────────────
    public async Task<IActionResult> Index(
        string? tuKhoa,
        string? trangThai,
        string? nguonDat,
        DateTime? tuNgay,
        DateTime? denNgay,
        string? nhomHoan)
    {
        var query = _db.DatPhongs
            .Include(d => d.MaNguoiDungNavigation)
            .Include(d => d.ChiTietDatPhongs)
                .ThenInclude(ct => ct.MaPhongNavigation)
                    .ThenInclude(p => p.MaLoaiPhongNavigation)
            .Include(d => d.ThanhToans)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var kw = tuKhoa.Trim().ToLower();
            query = query.Where(d =>
                d.MaDon.ToLower().Contains(kw) ||
                d.MaNguoiDungNavigation.HoTen.ToLower().Contains(kw) ||
                (d.MaNguoiDungNavigation.SoDienThoai != null && d.MaNguoiDungNavigation.SoDienThoai.Contains(kw)));
        }

        if (!string.IsNullOrEmpty(trangThai)) query = query.Where(d => d.TrangThai == trangThai);
        if (!string.IsNullOrEmpty(nguonDat))  query = query.Where(d => d.NguonDat == nguonDat);
        if (tuNgay.HasValue)  query = query.Where(d => d.NgayDat >= tuNgay.Value);
        if (denNgay.HasValue) query = query.Where(d => d.NgayDat < denNgay.Value.AddDays(1));

        var dons = await query.OrderByDescending(d => d.NgayDat).Take(500).ToListAsync();

        var result = new List<LichSuDonViewModel>();
        foreach (var d in dons)
        {
            var tien    = await _svc.TinhTienDonAsync(d.MaDatPhong);
            var ct      = d.ChiTietDatPhongs.FirstOrDefault();
            var tiLe    = _svc.TinhTiLeHoan(d.NgayNhanPhong, d.NgayHuy);
            result.Add(new LichSuDonViewModel
            {
                MaDatPhong    = d.MaDatPhong,
                MaDon         = d.MaDon,
                TenLoaiPhong  = ct?.MaPhongNavigation?.MaLoaiPhongNavigation?.TenLoaiPhong ?? "-",
                SoPhong       = ct?.MaPhongNavigation?.SoPhong ?? "-",
                NgayNhanPhong = d.NgayNhanPhong,
                NgayTraPhong  = d.NgayTraPhong,
                SoDem         = d.SoDem ?? 0,
                TrangThai     = d.TrangThai,
                LoaiThanhToan = d.LoaiThanhToan,
                NguonDat      = d.NguonDat,
                TongTien      = tien.TongTien,
                DaTra         = tien.SoTienDaTra,
                ConLai        = tien.SoTienConLai,
                NgayDat       = d.NgayDat,
                NgayHuy       = d.NgayHuy,
                TiLeHoan      = tiLe,
                SoTienHoan    = tien.SoTienDaTra * tiLe / 100m,
                CoTheHuy      = false
            });
        }

        // Lọc nhóm hoàn tiền
        if (!string.IsNullOrEmpty(nhomHoan))
        {
            result = nhomHoan switch
            {
                "tren7"   => result.Where(r => r.TiLeHoan == 100).ToList(),
                "tu3den7" => result.Where(r => r.TiLeHoan == 50).ToList(),
                "duoi3"   => result.Where(r => r.TiLeHoan == 0 && r.TrangThai is "ChoHoanTien" or "DaHuy").ToList(),
                _         => result
            };
        }

        ViewBag.TuKhoa     = tuKhoa;
        ViewBag.TrangThai  = trangThai;
        ViewBag.NguonDat   = nguonDat;
        ViewBag.TuNgay     = tuNgay?.ToString("yyyy-MM-dd");
        ViewBag.DenNgay    = denNgay?.ToString("yyyy-MM-dd");
        ViewBag.NhomHoan   = nhomHoan;
        ViewBag.TongSoDon  = result.Count;
        // Thống kê nhanh
        ViewBag.TongDoanhThu = result.Where(r => r.TrangThai is "DaXacNhan" or "DangO" or "DaTraPhong")
                                     .Sum(r => r.DaTra);
        return View(result);
    }

    // ── GET /Admin/DonDatPhong/ChiTiet/5 ─────────────────────────────────────
    public async Task<IActionResult> ChiTiet(int id)
    {
        var don = await _db.DatPhongs
            .Include(d => d.MaNguoiDungNavigation)
            .Include(d => d.ChiTietDatPhongs)
                .ThenInclude(ct => ct.MaPhongNavigation)
                    .ThenInclude(p => p.MaLoaiPhongNavigation)
            .Include(d => d.ChiTietDatPhongs)
                .ThenInclude(ct => ct.ChiTietDichVus)
                    .ThenInclude(dv => dv.MaDichVuNavigation)
            .Include(d => d.ThanhToans)
            .FirstOrDefaultAsync(d => d.MaDatPhong == id);

        if (don == null) return NotFound();

        var tien = await _svc.TinhTienDonAsync(id);
        ViewBag.TinhTien = tien;
        if (don.TrangThai is "ChoHoanTien" or "DaHuy")
        {
            var tiLe = _svc.TinhTiLeHoan(don.NgayNhanPhong, don.NgayHuy);
            ViewBag.TiLeHoan   = tiLe;
            ViewBag.SoTienHoan = tien.SoTienDaTra * tiLe / 100m;
        }
        return View(don);
    }

    // ── POST /Admin/DonDatPhong/DaHoanTien ───────────────────────────────────
    // CONTEXT 5.7: web chỉ ghi nhận trạng thái, tiền hoàn do con người làm ngoài app
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DaHoanTien(int maDatPhong)
    {
        var don = await _db.DatPhongs.FindAsync(maDatPhong);
        if (don != null && don.TrangThai == "ChoHoanTien")
        {
            don.TrangThai   = "DaHuy";
            don.NgayCapNhat = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Đã đánh dấu hoàn tiền xong cho đơn {don.MaDon}.";
        }
        return RedirectToAction(nameof(ChiTiet), new { id = maDatPhong });
    }

    // ── POST /Admin/DonDatPhong/XacNhan ──────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> XacNhan(int maDatPhong)
    {
        var don = await _db.DatPhongs.FindAsync(maDatPhong);
        if (don != null && don.TrangThai == "ChoXacNhan")
        {
            don.TrangThai   = "DaXacNhan";
            don.NgayCapNhat = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Đơn {don.MaDon} đã được xác nhận.";
        }
        return RedirectToAction(nameof(ChiTiet), new { id = maDatPhong });
    }

    // ── POST /Admin/DonDatPhong/Huy ───────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Huy(int maDatPhong)
    {
        var don = await _db.DatPhongs.Include(d => d.ThanhToans).FirstOrDefaultAsync(d => d.MaDatPhong == maDatPhong);
        if (don != null && don.TrangThai is "ChoXacNhan" or "DaXacNhan")
        {
            bool daCoPay    = don.ThanhToans.Any(t => t.TrangThai == "ThanhCong");
            don.TrangThai   = daCoPay ? "ChoHoanTien" : "DaHuy";
            don.NgayCapNhat = DateTime.Now;
            don.NgayHuy     = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = daCoPay ? $"Đơn {don.MaDon} chờ hoàn tiền." : $"Đã hủy đơn {don.MaDon}.";
        }
        return RedirectToAction(nameof(ChiTiet), new { id = maDatPhong });
    }
}
