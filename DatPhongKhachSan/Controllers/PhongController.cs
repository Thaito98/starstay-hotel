using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Controllers;

public class PhongController : Controller
{
    private readonly DatPhongKhachSanContext _db;

    public PhongController(DatPhongKhachSanContext db) => _db = db;

    // GET /Phong
    // GET /Phong?ngayNhan=2025-12-01&ngayTra=2025-12-03&soNguoiLon=2&soTreEm=0
    public async Task<IActionResult> Index(DateTime? ngayNhan, DateTime? ngayTra,
                                           int soNguoiLon = 1, int soTreEm = 0)
    {
        var vm = new TimPhongViewModel
        {
            NgayNhanPhong = ngayNhan ?? DateTime.Today.AddDays(1),
            NgayTraPhong  = ngayTra  ?? DateTime.Today.AddDays(2),
            SoNguoiLon    = soNguoiLon,
            SoTreEm       = soTreEm,
            DaTimKiem     = ngayNhan.HasValue && ngayTra.HasValue
        };

        if (vm.DaTimKiem && vm.NgayNhanPhong >= vm.NgayTraPhong)
        {
            ModelState.AddModelError("", "Ngày trả phòng phải sau ngày nhận phòng");
            vm.DaTimKiem = false;
        }

        if (vm.DaTimKiem)
        {
            var dnhan = DateOnly.FromDateTime(vm.NgayNhanPhong);
            var dtra  = DateOnly.FromDateTime(vm.NgayTraPhong);

            var phongDaBiet = _db.ChiTietDatPhongs
                .Where(ct => ct.MaDatPhongNavigation.TrangThai != "DaHuy"
                          && ct.MaDatPhongNavigation.TrangThai != "DaTraPhong"
                          && ct.MaDatPhongNavigation.NgayNhanPhong < dtra
                          && ct.MaDatPhongNavigation.NgayTraPhong > dnhan)
                .Select(ct => ct.MaPhong);

            var loaiPhongs = await _db.LoaiPhongs
                .Where(lp => lp.TrangThai == true
                          && lp.SucChuaNguoiLon >= soNguoiLon
                          && lp.Phongs.Any(p => p.TrangThai == "Trong"
                                             && !phongDaBiet.Contains(p.MaPhong)))
                .Include(lp => lp.AnhPhongs)
                .Include(lp => lp.MaTienNghis)
                .Include(lp => lp.Phongs)
                .OrderBy(lp => lp.GiaCoBan)
                .ToListAsync();

            vm.KetQua = loaiPhongs.Select(lp => new LoaiPhongCardViewModel
            {
                MaLoaiPhong        = lp.MaLoaiPhong,
                TenLoaiPhong       = lp.TenLoaiPhong,
                MoTa               = lp.MoTa,
                GiaCoBan           = lp.GiaCoBan,
                AnhChinh           = lp.AnhPhongs.FirstOrDefault(a => a.LaAnhChinh)?.DuongDanAnh
                                  ?? lp.AnhPhongs.OrderBy(a => a.ThuTu).FirstOrDefault()?.DuongDanAnh,
                SucChuaNguoiLon    = lp.SucChuaNguoiLon,
                SucChuaTreEm       = lp.SucChuaTreEm,
                LoaiGiuong         = lp.LoaiGiuong,
                DienTich           = lp.DienTich,
                TenTienNghis       = lp.MaTienNghis.Select(t => t.TenTienNghi).ToList(),
                BieuTuongTienNghis = lp.MaTienNghis.Select(t => t.BieuTuong ?? "fa-check").ToList(),
                SoPhongConTrong    = lp.Phongs.Count(p => p.TrangThai == "Trong"
                                                        && !phongDaBiet.Contains(p.MaPhong))
            }).ToList();
        }
        else
        {
            vm.KetQua = await LayTatCaLoaiPhong();
        }

        return View(vm);
    }

    // GET /Phong/ChiTiet/5
    public async Task<IActionResult> ChiTiet(int id,
        DateTime? ngayNhan, DateTime? ngayTra, int soNguoiLon = 1, int soTreEm = 0)
    {
        var loaiPhong = await _db.LoaiPhongs
            .Include(lp => lp.AnhPhongs.OrderBy(a => a.ThuTu))
            .Include(lp => lp.MaTienNghis)
            .FirstOrDefaultAsync(lp => lp.MaLoaiPhong == id && lp.TrangThai == true);

        if (loaiPhong == null) return NotFound();

        var soPhong = await DemPhongConTrong(id, ngayNhan, ngayTra);

        return View(new LoaiPhongDetailViewModel
        {
            LoaiPhong      = loaiPhong,
            Anhs           = loaiPhong.AnhPhongs.ToList(),
            TienNghis      = loaiPhong.MaTienNghis.ToList(),
            SoPhongHienCo  = soPhong,
            NgayNhanPhong  = ngayNhan,
            NgayTraPhong   = ngayTra,
            SoNguoiLon     = soNguoiLon,
            SoTreEm        = soTreEm
        });
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<List<LoaiPhongCardViewModel>> LayTatCaLoaiPhong()
    {
        var loaiPhongs = await _db.LoaiPhongs
            .Where(lp => lp.TrangThai == true)
            .Include(lp => lp.AnhPhongs)
            .Include(lp => lp.MaTienNghis)
            .Include(lp => lp.Phongs)
            .OrderBy(lp => lp.GiaCoBan)
            .ToListAsync();

        return loaiPhongs.Select(lp => new LoaiPhongCardViewModel
        {
            MaLoaiPhong        = lp.MaLoaiPhong,
            TenLoaiPhong       = lp.TenLoaiPhong,
            MoTa               = lp.MoTa,
            GiaCoBan           = lp.GiaCoBan,
            AnhChinh           = lp.AnhPhongs.FirstOrDefault(a => a.LaAnhChinh)?.DuongDanAnh
                              ?? lp.AnhPhongs.OrderBy(a => a.ThuTu).FirstOrDefault()?.DuongDanAnh,
            SucChuaNguoiLon    = lp.SucChuaNguoiLon,
            SucChuaTreEm       = lp.SucChuaTreEm,
            LoaiGiuong         = lp.LoaiGiuong,
            DienTich           = lp.DienTich,
            TenTienNghis       = lp.MaTienNghis.Select(t => t.TenTienNghi).ToList(),
            BieuTuongTienNghis = lp.MaTienNghis.Select(t => t.BieuTuong ?? "fa-check").ToList(),
            SoPhongConTrong    = lp.Phongs.Count(p => p.TrangThai == "Trong")
        }).ToList();
    }

    private async Task<int> DemPhongConTrong(int maLoaiPhong, DateTime? ngayNhan, DateTime? ngayTra)
    {
        if (ngayNhan == null || ngayTra == null)
            return await _db.Phongs.CountAsync(p =>
                p.MaLoaiPhong == maLoaiPhong && p.TrangThai == "Trong");

        var dnhan = DateOnly.FromDateTime(ngayNhan.Value);
        var dtra  = DateOnly.FromDateTime(ngayTra.Value);

        var phongDaBiet = _db.ChiTietDatPhongs
            .Where(ct => ct.MaDatPhongNavigation.TrangThai != "DaHuy"
                      && ct.MaDatPhongNavigation.TrangThai != "DaTraPhong"
                      && ct.MaDatPhongNavigation.NgayNhanPhong < dtra
                      && ct.MaDatPhongNavigation.NgayTraPhong > dnhan)
            .Select(ct => ct.MaPhong);

        return await _db.Phongs.CountAsync(p =>
            p.MaLoaiPhong == maLoaiPhong
            && p.TrangThai == "Trong"
            && !phongDaBiet.Contains(p.MaPhong));
    }
}
