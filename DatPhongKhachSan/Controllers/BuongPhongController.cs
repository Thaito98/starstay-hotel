using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Controllers;

[Authorize(Policy = ChucNang.BuongPhong)]
public class BuongPhongController : Controller
{
    private readonly DatPhongKhachSanContext _db;

    public BuongPhongController(DatPhongKhachSanContext db) => _db = db;

    // ── GET /BuongPhong ───────────────────────────────────────────────────────
    public async Task<IActionResult> Index(
        string? loc = null,
        string? soPhong = null,
        DateTime? tuNgay = null,
        DateTime? denNgay = null)
    {
        // Phòng đang cần xử lý (DangDonDep)
        var phongCanXuLy = await _db.Phongs
            .Include(p => p.MaLoaiPhongNavigation)
            .Include(p => p.DonGiaoViecs.OrderByDescending(d => d.NgayGiao).Take(1))
            .Where(p => p.TrangThai == "DangDonDep")
            .OrderBy(p => p.Tang).ThenBy(p => p.SoPhong)
            .ToListAsync();

        // Lịch sử đơn giao việc - có bộ lọc
        var query = _db.DonGiaoViecs
            .Include(d => d.MaPhongNavigation)
                .ThenInclude(p => p.MaLoaiPhongNavigation)
            .AsQueryable();

        if (!string.IsNullOrEmpty(loc) && loc != "TatCa")
            query = query.Where(d => d.TrangThai == loc);

        if (!string.IsNullOrWhiteSpace(soPhong))
            query = query.Where(d => d.MaPhongNavigation.SoPhong.Contains(soPhong.Trim()));

        if (tuNgay.HasValue)
            query = query.Where(d => d.NgayGiao >= tuNgay.Value);

        if (denNgay.HasValue)
            query = query.Where(d => d.NgayGiao < denNgay.Value.AddDays(1));

        var dons = await query.OrderByDescending(d => d.NgayGiao).Take(200).ToListAsync();

        ViewBag.Loc          = loc ?? "TatCa";
        ViewBag.SoPhong      = soPhong;
        ViewBag.TuNgay       = tuNgay?.ToString("yyyy-MM-dd");
        ViewBag.DenNgay      = denNgay?.ToString("yyyy-MM-dd");
        ViewBag.PhongCanXuLy = phongCanXuLy;
        ViewBag.DaLoc        = !string.IsNullOrWhiteSpace(soPhong) || tuNgay.HasValue || denNgay.HasValue;
        return View(dons);
    }

    // ── GET /BuongPhong/GiaoViec/{maPhong} ───────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GiaoViec(int maPhong)
    {
        var phong = await _db.Phongs
            .Include(p => p.MaLoaiPhongNavigation)
            .FirstOrDefaultAsync(p => p.MaPhong == maPhong);

        if (phong == null) return NotFound();

        if (phong.TrangThai != "DangDonDep")
        {
            TempData["Error"] = $"Phòng {phong.SoPhong} không ở trạng thái Đang dọn dẹp.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Phong = phong;
        return View();
    }

    // ── POST /BuongPhong/GiaoViec ─────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GiaoViec(int maPhong, string noiDung)
    {
        var phong = await _db.Phongs.Include(p => p.MaLoaiPhongNavigation)
                              .FirstOrDefaultAsync(p => p.MaPhong == maPhong);
        if (phong == null) return NotFound();

        if (string.IsNullOrWhiteSpace(noiDung))
        {
            ModelState.AddModelError("noiDung", "Vui lòng nhập nội dung công việc.");
            ViewBag.Phong = phong;
            return View();
        }

        _db.DonGiaoViecs.Add(new DonGiaoViec
        {
            MaPhong   = maPhong,
            NoiDung   = noiDung.Trim(),
            TrangThai = "ChoLam"
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã lập đơn giao việc cho phòng {phong.SoPhong}.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /BuongPhong/NghiemThu ────────────────────────────────────────────
    // Nghiệm thu xong => đơn HoanThanh => phòng về Trong
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NghiemThu(int maDonViec, string? ghiChu)
    {
        var don = await _db.DonGiaoViecs
            .Include(d => d.MaPhongNavigation)
            .FirstOrDefaultAsync(d => d.MaDonViec == maDonViec);
        if (don == null) return NotFound();

        don.TrangThai       = "HoanThanh";
        don.NgayHoanThanh   = DateTime.Now;
        don.GhiChuNghiemThu = ghiChu?.Trim();

        // Khi nghiệm thu xong => phòng về "Trong" (sẵn sàng đặt lại)
        var phong = don.MaPhongNavigation;
        if (phong != null) phong.TrangThai = "Trong";

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Nghiệm thu xong - phòng {phong?.SoPhong} về trạng thái Trống.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /BuongPhong/CapNhatPhong ─────────────────────────────────────────
    // Cập nhật thủ công Trong <=> DangDonDep. Không cho sửa phòng "DangO" (đang có khách).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatPhong(int maPhong, string trangThai)
    {
        var phong = await _db.Phongs.FindAsync(maPhong);
        if (phong == null) return NotFound();

        if (phong.TrangThai == "DangO")
        {
            TempData["Error"] = $"Không thể sửa trạng thái phòng {phong.SoPhong} đang có khách.";
            return RedirectToAction(nameof(Index));
        }

        // Chỉ cho phép Trong <=> DangDonDep (không có BaoTri riêng - gộp vào DangDonDep)
        if (trangThai is not ("Trong" or "DangDonDep"))
            return BadRequest();

        phong.TrangThai = trangThai;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Phòng {phong.SoPhong} => {trangThai}.";
        return RedirectToAction(nameof(Index));
    }
}
