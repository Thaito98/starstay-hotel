using DatPhongKhachSan.Constants;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = ChucNang.ChamCong)]
public class ChamCongController : Controller
{
    private readonly DatPhongKhachSanContext _db;

    public ChamCongController(DatPhongKhachSanContext db) => _db = db;

    // ── GET /Admin/ChamCong - trang chính với 2 tab ──────────────────────────
    // ?tab=chamcong&ngayCong=2025-06-05   => chấm công ngày
    // ?tab=tonghop&thang=6&nam=2025       => tổng hợp tháng
    public async Task<IActionResult> Index(
        string? tab,
        string? ngayCong,
        int? thang,
        int? nam)
    {
        tab ??= "chamcong";
        ViewBag.Tab = tab;

        if (tab == "tonghop")
        {
            var vm = await LayTongHop(thang ?? DateTime.Today.Month, nam ?? DateTime.Today.Year);
            return View(("tonghop", (object)vm));
        }
        else
        {
            var ngay = DateOnly.TryParse(ngayCong, out var d) ? d : DateOnly.FromDateTime(DateTime.Today);
            var vm   = await LayDanhSachNgay(ngay);
            return View(("chamcong", (object)vm));
        }
    }

    // ── POST /Admin/ChamCong/Luu - lưu/cập nhật chấm công cả bảng ───────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Luu(DateOnly ngayCong, List<ChamCongNhanVienItem> danhSach)
    {
        foreach (var item in danhSach)
        {
            decimal soCong = item.TrangThai == "Vang" ? 0m : 1.0m;
            int soPhutTre  = item.TrangThai == "Tre" ? Math.Max(0, item.SoPhutTre) : 0;

            var existing = await _db.ChamCongs
                .FirstOrDefaultAsync(c => c.MaNguoiDung == item.MaNguoiDung && c.NgayCong == ngayCong);

            if (existing != null)
            {
                existing.TrangThai = item.TrangThai;
                existing.SoCong    = soCong;
                existing.SoPhutTre = soPhutTre;
            }
            else
            {
                _db.ChamCongs.Add(new ChamCong
                {
                    MaNguoiDung = item.MaNguoiDung,
                    NgayCong    = ngayCong,
                    TrangThai   = item.TrangThai,
                    SoCong      = soCong,
                    SoPhutTre   = soPhutTre
                });
            }
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã lưu chấm công ngày {ngayCong:dd/MM/yyyy}.";
        return RedirectToAction(nameof(Index), new { tab = "chamcong", ngayCong = ngayCong.ToString("yyyy-MM-dd") });
    }

    // ── Helper: danh sách nhân viên theo ca ngày đó ──────────────────────────
    private async Task<ChamCongNgayViewModel> LayDanhSachNgay(DateOnly ngay)
    {
        // Lấy phân ca ngày này (join CaLamViec + NguoiDung)
        var phanCas = await _db.PhanCas
            .Include(p => p.MaNguoiDungNavigation)
            .Include(p => p.MaCaNavigation)
            .Where(p => p.NgayLam == ngay)
            .OrderBy(p => p.MaCaNavigation.GioBatDau)
                .ThenBy(p => p.MaNguoiDungNavigation.HoTen)
            .ToListAsync();

        var vm = new ChamCongNgayViewModel { NgayCong = ngay };

        if (!phanCas.Any())
        {
            vm.ChuaXepCa = true;
            return vm;
        }

        // Lấy bản ghi chấm công đã có (nếu có)
        var maNVs    = phanCas.Select(p => p.MaNguoiDung).Distinct().ToList();
        var chamCongs = await _db.ChamCongs
            .Where(c => maNVs.Contains(c.MaNguoiDung) && c.NgayCong == ngay)
            .ToListAsync();

        foreach (var pc in phanCas)
        {
            var cc = chamCongs.FirstOrDefault(c => c.MaNguoiDung == pc.MaNguoiDung);
            vm.DanhSach.Add(new ChamCongNhanVienItem
            {
                MaNguoiDung  = pc.MaNguoiDung,
                HoTen        = pc.MaNguoiDungNavigation?.HoTen ?? "-",
                TenCa        = pc.MaCaNavigation?.TenCa ?? "-",
                GioLamViec   = pc.MaCaNavigation != null
                    ? $"{pc.MaCaNavigation.GioBatDau:HH\\:mm}-{pc.MaCaNavigation.GioKetThuc:HH\\:mm}"
                    : "",
                TrangThai    = cc?.TrangThai ?? "DiLam",
                SoPhutTre    = cc?.SoPhutTre ?? 0,
                DaChamCong   = cc != null,
                MaChamCong   = cc?.MaChamCong
            });
        }

        return vm;
    }

    // ── Helper: tổng hợp tháng ────────────────────────────────────────────────
    private async Task<TongHopThangViewModel> LayTongHop(int thang, int nam)
    {
        var chamCongs = await _db.ChamCongs
            .Include(c => c.MaNguoiDungNavigation)
            .Where(c => c.NgayCong.Month == thang && c.NgayCong.Year == nam)
            .ToListAsync();

        var grouped = chamCongs
            .GroupBy(c => c.MaNguoiDung)
            .Select(g => new TongHopNhanVienItem
            {
                HoTen        = g.First().MaNguoiDungNavigation?.HoTen ?? "-",
                TongCong     = g.Sum(c => c.SoCong),
                SoNgayDiLam  = g.Count(c => c.TrangThai == "DiLam"),
                SoLanTre     = g.Count(c => c.TrangThai == "Tre"),
                TongPhutTre  = g.Sum(c => c.SoPhutTre),
                SoNgayVang   = g.Count(c => c.TrangThai == "Vang")
            })
            .OrderBy(x => x.HoTen)
            .ToList();

        return new TongHopThangViewModel { Thang = thang, Nam = nam, DanhSach = grouped };
    }
}
