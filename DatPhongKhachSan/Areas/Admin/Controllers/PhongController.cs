using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Models.ViewModels;
using DatPhongKhachSan.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = ChucNang.QuanLyDanhMuc)]
public class PhongController : Controller
{
    private readonly DatPhongKhachSanContext _db;

    public PhongController(DatPhongKhachSanContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var ds = await _db.Phongs
            .Include(p => p.MaLoaiPhongNavigation)
            .OrderBy(p => p.Tang).ThenBy(p => p.SoPhong)
            .ToListAsync();
        return View(ds);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        return View(new PhongFormViewModel
        {
            DanhSachLoaiPhong = await _db.LoaiPhongs.Where(l => l.TrangThai).OrderBy(l => l.TenLoaiPhong).ToListAsync()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PhongFormViewModel vm)
    {
        vm.DanhSachLoaiPhong = await _db.LoaiPhongs.Where(l => l.TrangThai).OrderBy(l => l.TenLoaiPhong).ToListAsync();

        if (await _db.Phongs.AnyAsync(p => p.SoPhong == vm.SoPhong))
            ModelState.AddModelError("SoPhong", "Số phòng đã tồn tại.");

        if (!ModelState.IsValid) return View(vm);

        _db.Phongs.Add(new Phong
        {
            SoPhong     = vm.SoPhong.Trim(),
            Tang        = vm.Tang,
            MaLoaiPhong = vm.MaLoaiPhong,
            TrangThai   = vm.TrangThai,
            GhiChu      = vm.GhiChu
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã thêm phòng {vm.SoPhong}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await _db.Phongs.FindAsync(id);
        if (p == null) return NotFound();
        return View(new PhongFormViewModel
        {
            MaPhong           = p.MaPhong,
            SoPhong           = p.SoPhong,
            Tang              = p.Tang,
            MaLoaiPhong       = p.MaLoaiPhong,
            TrangThai         = p.TrangThai,
            GhiChu            = p.GhiChu,
            DanhSachLoaiPhong = await _db.LoaiPhongs.Where(l => l.TrangThai).OrderBy(l => l.TenLoaiPhong).ToListAsync()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PhongFormViewModel vm)
    {
        vm.DanhSachLoaiPhong = await _db.LoaiPhongs.Where(l => l.TrangThai).OrderBy(l => l.TenLoaiPhong).ToListAsync();

        if (await _db.Phongs.AnyAsync(p => p.SoPhong == vm.SoPhong && p.MaPhong != vm.MaPhong))
            ModelState.AddModelError("SoPhong", "Số phòng đã tồn tại.");

        if (!ModelState.IsValid) return View(vm);

        var p = await _db.Phongs.FindAsync(vm.MaPhong);
        if (p == null) return NotFound();

        p.SoPhong     = vm.SoPhong.Trim();
        p.Tang        = vm.Tang;
        p.MaLoaiPhong = vm.MaLoaiPhong;
        p.TrangThai   = vm.TrangThai;
        p.GhiChu      = vm.GhiChu;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã cập nhật phòng {p.SoPhong}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Phongs.Include(x => x.ChiTietDatPhongs).FirstOrDefaultAsync(x => x.MaPhong == id);
        if (p == null) return NotFound();

        if (p.ChiTietDatPhongs.Any())
        {
            TempData["Error"] = $"Phòng {p.SoPhong} đang có đơn đặt - không thể xóa.";
            return RedirectToAction(nameof(Index));
        }

        _db.Phongs.Remove(p);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã xóa phòng {p.SoPhong}.";
        return RedirectToAction(nameof(Index));
    }
}

