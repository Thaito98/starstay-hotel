using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = ChucNang.QuanLyDanhMuc)]
public class DichVuController : Controller
{
    private readonly DatPhongKhachSanContext _db;

    public DichVuController(DatPhongKhachSanContext db) => _db = db;

    public async Task<IActionResult> Index()
        => View(await _db.DichVus.OrderBy(d => d.TenDichVu).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View(new DichVu { TrangThai = true });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DichVu vm)
    {
        if (!ModelState.IsValid) return View(vm);
        _db.DichVus.Add(vm);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã thêm dịch vụ '{vm.TenDichVu}'.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var d = await _db.DichVus.FindAsync(id);
        if (d == null) return NotFound();
        return View(d);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DichVu vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var d = await _db.DichVus.FindAsync(vm.MaDichVu);
        if (d == null) return NotFound();
        d.TenDichVu = vm.TenDichVu; d.MoTa = vm.MoTa; d.Gia = vm.Gia;
        d.DonVi = vm.DonVi; d.DuongDanAnh = vm.DuongDanAnh; d.TrangThai = vm.TrangThai;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật dịch vụ.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var d = await _db.DichVus.Include(x => x.ChiTietDichVus).FirstOrDefaultAsync(x => x.MaDichVu == id);
        if (d == null) return NotFound();
        if (d.ChiTietDichVus.Any())
        {
            TempData["Error"] = $"Dịch vụ '{d.TenDichVu}' đang có trong đơn đặt - không thể xóa.";
            return RedirectToAction(nameof(Index));
        }
        _db.DichVus.Remove(d);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã xóa dịch vụ '{d.TenDichVu}'.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DoiTrangThai(int id)
    {
        var d = await _db.DichVus.FindAsync(id);
        if (d != null) { d.TrangThai = !d.TrangThai; await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }
}

