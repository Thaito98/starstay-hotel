using System.Diagnostics;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models;
using DatPhongKhachSan.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Controllers;

public class HomeController : Controller
{
    private readonly DatPhongKhachSanContext _db;
    private readonly ILogger<HomeController> _logger;

    public HomeController(DatPhongKhachSanContext db, ILogger<HomeController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var loaiPhongs = await _db.LoaiPhongs
            .Where(lp => lp.TrangThai == true)
            .Include(lp => lp.AnhPhongs)
            .Include(lp => lp.MaTienNghis)
            .Include(lp => lp.Phongs)
            .OrderBy(lp => lp.GiaCoBan)
            .ToListAsync();

        var vm = loaiPhongs.Select(lp => new LoaiPhongCardViewModel
        {
            MaLoaiPhong = lp.MaLoaiPhong,
            TenLoaiPhong = lp.TenLoaiPhong,
            MoTa = lp.MoTa,
            GiaCoBan = lp.GiaCoBan,
            AnhChinh = lp.AnhPhongs.FirstOrDefault(a => a.LaAnhChinh)?.DuongDanAnh
                    ?? lp.AnhPhongs.OrderBy(a => a.ThuTu).FirstOrDefault()?.DuongDanAnh,
            SucChuaNguoiLon = lp.SucChuaNguoiLon,
            SucChuaTreEm = lp.SucChuaTreEm,
            LoaiGiuong = lp.LoaiGiuong,
            DienTich = lp.DienTich,
            TenTienNghis = lp.MaTienNghis.Select(t => t.TenTienNghi).ToList(),
            BieuTuongTienNghis = lp.MaTienNghis.Select(t => t.BieuTuong ?? "fa-check").ToList(),
            SoPhongConTrong = lp.Phongs.Count(p => p.TrangThai == "Trong")
        }).ToList();

        ViewBag.DichVus = await _db.DichVus
            .Where(d => d.TrangThai)
            .OrderBy(d => d.MaDichVu)
            .Take(6)
            .ToListAsync();

        return View(vm);
    }

    public IActionResult Privacy() => View();

    [Route("Home/Error/{statusCode?}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode)
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = statusCode ?? 500
        });
    }
}
