using System.ComponentModel.DataAnnotations;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = ChucNang.QuanLyDanhMuc)]
public class TienNghiController : Controller
{
    private readonly DatPhongKhachSanContext _db;

    public TienNghiController(DatPhongKhachSanContext db) => _db = db;

    public async Task<IActionResult> Index()
        => View(await _db.TienNghis.OrderBy(t => t.TenTienNghi).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string tenTienNghi, string? bieuTuong, string? moTa)
    {
        if (string.IsNullOrWhiteSpace(tenTienNghi))
        {
            TempData["Error"] = "Tên tiện nghi không được để trống.";
            return RedirectToAction(nameof(Index));
        }
        _db.TienNghis.Add(new TienNghi { TenTienNghi = tenTienNghi.Trim(), BieuTuong = bieuTuong, MoTa = moTa });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã thêm tiện nghi '{tenTienNghi}'.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int maTienNghi, string tenTienNghi, string? bieuTuong, string? moTa)
    {
        var t = await _db.TienNghis.FindAsync(maTienNghi);
        if (t == null) return NotFound();
        t.TenTienNghi = tenTienNghi.Trim();
        t.BieuTuong   = bieuTuong;
        t.MoTa        = moTa;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật tiện nghi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await _db.TienNghis.FindAsync(id);
        if (t == null) return NotFound();
        _db.TienNghis.Remove(t);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã xóa tiện nghi '{t.TenTienNghi}'.";
        return RedirectToAction(nameof(Index));
    }
}

