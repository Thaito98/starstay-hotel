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
public class LoaiPhongController : Controller
{
    private readonly DatPhongKhachSanContext _db;
    private readonly IWebHostEnvironment _env;

    public LoaiPhongController(DatPhongKhachSanContext db, IWebHostEnvironment env)
    {
        _db  = db;
        _env = env;
    }

    // ── Index ────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var ds = await _db.LoaiPhongs
            .Include(l => l.MaTienNghis)
            .Include(l => l.Phongs)
            .Include(l => l.AnhPhongs)
            .OrderBy(l => l.MaLoaiPhong)
            .ToListAsync();
        return View(ds);
    }

    // ── Create GET ────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        return View(new LoaiPhongFormViewModel
        {
            TatCaTienNghi = await _db.TienNghis.OrderBy(t => t.TenTienNghi).ToListAsync()
        });
    }

    // ── Create POST ───────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LoaiPhongFormViewModel vm)
    {
        vm.TatCaTienNghi = await _db.TienNghis.OrderBy(t => t.TenTienNghi).ToListAsync();
        if (!ModelState.IsValid) return View(vm);

        var entity = new LoaiPhong
        {
            TenLoaiPhong    = vm.TenLoaiPhong,
            MoTa            = vm.MoTa,
            GiaCoBan        = vm.GiaCoBan,
            SucChuaNguoiLon = vm.SucChuaNguoiLon,
            SucChuaTreEm    = vm.SucChuaTreEm,
            LoaiGiuong      = vm.LoaiGiuong,
            DienTich        = vm.DienTich,
            TrangThai       = vm.TrangThai,
            MaTienNghis     = await _db.TienNghis
                .Where(t => vm.MaTienNghiChon.Contains(t.MaTienNghi)).ToListAsync()
        };
        _db.LoaiPhongs.Add(entity);
        await _db.SaveChangesAsync();

        await LuuAnh(entity.MaLoaiPhong, vm.FileAnhs);
        TempData["Success"] = $"Đã thêm loại phòng '{entity.TenLoaiPhong}'.";
        return RedirectToAction(nameof(Index));
    }

    // ── Edit GET ──────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await _db.LoaiPhongs
            .Include(l => l.MaTienNghis)
            .Include(l => l.AnhPhongs.OrderBy(a => a.ThuTu))
            .FirstOrDefaultAsync(l => l.MaLoaiPhong == id);
        if (e == null) return NotFound();

        return View(new LoaiPhongFormViewModel
        {
            MaLoaiPhong     = e.MaLoaiPhong,
            TenLoaiPhong    = e.TenLoaiPhong,
            MoTa            = e.MoTa,
            GiaCoBan        = e.GiaCoBan,
            SucChuaNguoiLon = e.SucChuaNguoiLon,
            SucChuaTreEm    = e.SucChuaTreEm,
            LoaiGiuong      = e.LoaiGiuong,
            DienTich        = e.DienTich,
            TrangThai       = e.TrangThai,
            MaTienNghiChon  = e.MaTienNghis.Select(t => t.MaTienNghi).ToList(),
            TatCaTienNghi   = await _db.TienNghis.OrderBy(t => t.TenTienNghi).ToListAsync(),
            AnhPhongs       = e.AnhPhongs.ToList()
        });
    }

    // ── Edit POST ─────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(LoaiPhongFormViewModel vm)
    {
        vm.TatCaTienNghi = await _db.TienNghis.OrderBy(t => t.TenTienNghi).ToListAsync();
        if (!ModelState.IsValid) return View(vm);

        var e = await _db.LoaiPhongs
            .Include(l => l.MaTienNghis)
            .Include(l => l.AnhPhongs)
            .FirstOrDefaultAsync(l => l.MaLoaiPhong == vm.MaLoaiPhong);
        if (e == null) return NotFound();

        e.TenLoaiPhong    = vm.TenLoaiPhong;
        e.MoTa            = vm.MoTa;
        e.GiaCoBan        = vm.GiaCoBan;
        e.SucChuaNguoiLon = vm.SucChuaNguoiLon;
        e.SucChuaTreEm    = vm.SucChuaTreEm;
        e.LoaiGiuong      = vm.LoaiGiuong;
        e.DienTich        = vm.DienTich;
        e.TrangThai       = vm.TrangThai;

        // Cập nhật TienNghi
        e.MaTienNghis = await _db.TienNghis
            .Where(t => vm.MaTienNghiChon.Contains(t.MaTienNghi)).ToListAsync();

        await _db.SaveChangesAsync();
        await LuuAnh(vm.MaLoaiPhong, vm.FileAnhs);

        vm.AnhPhongs = e.AnhPhongs.OrderBy(a => a.ThuTu).ToList();
        TempData["Success"] = "Đã cập nhật loại phòng.";
        return View(vm);
    }

    // ── Xóa ảnh ──────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> XoaAnh(int maAnh, int maLoaiPhong)
    {
        var anh = await _db.AnhPhongs.FindAsync(maAnh);
        if (anh != null)
        {
            var path = Path.Combine(_env.WebRootPath, anh.DuongDanAnh.TrimStart('/'));
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            _db.AnhPhongs.Remove(anh);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Edit), new { id = maLoaiPhong });
    }

    // ── Đặt ảnh chính ─────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DatAnhChinh(int maAnh, int maLoaiPhong)
    {
        await _db.AnhPhongs.Where(a => a.MaLoaiPhong == maLoaiPhong)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.LaAnhChinh, false));
        var anh = await _db.AnhPhongs.FindAsync(maAnh);
        if (anh != null) { anh.LaAnhChinh = true; await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Edit), new { id = maLoaiPhong });
    }

    // ── Delete POST ───────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await _db.LoaiPhongs.Include(l => l.Phongs).FirstOrDefaultAsync(l => l.MaLoaiPhong == id);
        if (e == null) return NotFound();

        if (e.Phongs.Any())
        {
            TempData["Error"] = $"Không thể xóa: loại phòng '{e.TenLoaiPhong}' còn {e.Phongs.Count} phòng.";
            return RedirectToAction(nameof(Index));
        }

        _db.LoaiPhongs.Remove(e);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã xóa loại phòng '{e.TenLoaiPhong}'.";
        return RedirectToAction(nameof(Index));
    }

    // ── Helper upload ảnh ─────────────────────────────────────────────────────
    private async Task LuuAnh(int maLoaiPhong, List<IFormFile>? files)
    {
        if (files == null || !files.Any()) return;

        var folder = Path.Combine(_env.WebRootPath, "images", "phong", maLoaiPhong.ToString());
        Directory.CreateDirectory(folder);

        int thuTu = await _db.AnhPhongs.Where(a => a.MaLoaiPhong == maLoaiPhong)
                              .Select(a => a.ThuTu).DefaultIfEmpty(0).MaxAsync() + 1;

        bool coAnhChinh = await _db.AnhPhongs.AnyAsync(a => a.MaLoaiPhong == maLoaiPhong && a.LaAnhChinh);

        foreach (var file in files.Where(f => f.Length > 0))
        {
            var ext      = Path.GetExtension(file.FileName).ToLower();
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(folder, fileName);

            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            _db.AnhPhongs.Add(new AnhPhong
            {
                MaLoaiPhong = maLoaiPhong,
                DuongDanAnh = $"/images/phong/{maLoaiPhong}/{fileName}",
                LaAnhChinh  = !coAnhChinh,   // ảnh đầu tiên = ảnh chính
                ThuTu       = thuTu++
            });
            coAnhChinh = true;
        }
        await _db.SaveChangesAsync();
    }
}
