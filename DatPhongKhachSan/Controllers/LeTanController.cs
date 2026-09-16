using System.Security.Claims;
using DatPhongKhachSan.Constants;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Models.ViewModels;
using DatPhongKhachSan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Controllers;

[Authorize]   // yêu cầu đăng nhập; mỗi action có policy riêng
public class LeTanController : Controller
{
    private readonly DatPhongKhachSanContext _db;
    private readonly DatPhongService _svc;

    public LeTanController(DatPhongKhachSanContext db, DatPhongService svc)
    {
        _db  = db;
        _svc = svc;
    }

    // ── Index => redirect về tra cứu ──────────────────────────────────────────
    [Authorize(Policy = ChucNang.TraCuuDon)]
    public IActionResult Index() => RedirectToAction(nameof(TraCuuDon));

    // ── GET /LeTan/TraCuuDon ─────────────────────────────────────────────────
    [HttpGet, Authorize(Policy = ChucNang.TraCuuDon)]
    public async Task<IActionResult> TraCuuDon()
    {
        await _svc.HuyDonHetHanAsync();

        var dons = await _db.DatPhongs
            .Include(d => d.MaNguoiDungNavigation)
            .Include(d => d.ChiTietDatPhongs)
                .ThenInclude(ct => ct.MaPhongNavigation)
                    .ThenInclude(p => p.MaLoaiPhongNavigation)
            .Include(d => d.ThanhToans)
            .OrderByDescending(d => d.NgayDat)
            .Take(100)
            .ToListAsync();

        var result = new List<DonTomTatViewModel>();
        foreach (var d in dons)
        {
            var tien = await _svc.TinhTienDonAsync(d.MaDatPhong);
            var ct   = d.ChiTietDatPhongs.FirstOrDefault();
            result.Add(new DonTomTatViewModel
            {
                MaDatPhong    = d.MaDatPhong,
                MaDon         = d.MaDon,
                TenKhach      = d.MaNguoiDungNavigation?.HoTen ?? "-",
                SoDienThoai   = d.MaNguoiDungNavigation?.SoDienThoai,
                TenLoaiPhong  = ct?.MaPhongNavigation?.MaLoaiPhongNavigation?.TenLoaiPhong ?? "-",
                SoPhong       = ct?.MaPhongNavigation?.SoPhong ?? "-",
                NgayNhanPhong = d.NgayNhanPhong,
                NgayTraPhong  = d.NgayTraPhong,
                SoDem         = d.SoDem ?? 0,
                TrangThai     = d.TrangThai,
                NguonDat      = d.NguonDat,
                TongTien      = tien.TongTien,
                ConLai        = tien.SoTienConLai
            });
        }

        return View(new TraCuuDonViewModel { KetQua = result, DaTimKiem = true });
    }

    // ── POST /LeTan/TraCuuDon ────────────────────────────────────────────────
    [HttpPost, Authorize(Policy = ChucNang.TraCuuDon)]
    public async Task<IActionResult> TraCuuDon(TraCuuDonViewModel vm)
    {
        await _svc.HuyDonHetHanAsync();   // dọn đơn hết hạn trước khi tra cứu

        var query = _db.DatPhongs
            .Include(d => d.MaNguoiDungNavigation)
            .Include(d => d.ChiTietDatPhongs)
                .ThenInclude(ct => ct.MaPhongNavigation)
                    .ThenInclude(p => p.MaLoaiPhongNavigation)
            .Include(d => d.ThanhToans)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(vm.TuKhoa))
        {
            var kw = vm.TuKhoa.Trim().ToLower();
            query = query.Where(d =>
                d.MaDon.ToLower().Contains(kw) ||
                d.MaNguoiDungNavigation.HoTen.ToLower().Contains(kw) ||
                (d.MaNguoiDungNavigation.SoDienThoai != null &&
                 d.MaNguoiDungNavigation.SoDienThoai.Contains(kw)));
        }

        if (!string.IsNullOrEmpty(vm.LocTrangThai))
            query = query.Where(d => d.TrangThai == vm.LocTrangThai);

        var dons = await query.OrderByDescending(d => d.NgayDat).Take(100).ToListAsync();

        var result = new List<DonTomTatViewModel>();
        foreach (var d in dons)
        {
            var tien = await _svc.TinhTienDonAsync(d.MaDatPhong);
            var ct = d.ChiTietDatPhongs.FirstOrDefault();
            result.Add(new DonTomTatViewModel
            {
                MaDatPhong    = d.MaDatPhong,
                MaDon         = d.MaDon,
                TenKhach      = d.MaNguoiDungNavigation?.HoTen ?? "-",
                SoDienThoai   = d.MaNguoiDungNavigation?.SoDienThoai,
                TenLoaiPhong  = ct?.MaPhongNavigation?.MaLoaiPhongNavigation?.TenLoaiPhong ?? "-",
                SoPhong       = ct?.MaPhongNavigation?.SoPhong ?? "-",
                NgayNhanPhong = d.NgayNhanPhong,
                NgayTraPhong  = d.NgayTraPhong,
                SoDem         = d.SoDem ?? 0,
                TrangThai     = d.TrangThai,
                NguonDat      = d.NguonDat,
                TongTien      = tien.TongTien,
                ConLai        = tien.SoTienConLai
            });
        }

        vm.KetQua    = result;
        vm.DaTimKiem = true;
        return View(vm);
    }

    // ── GET /LeTan/ChiTietDon/5 ──────────────────────────────────────────────
    [Authorize(Policy = ChucNang.TraCuuDon)]
    public async Task<IActionResult> ChiTietDon(int id)
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
        if (don.TrangThai is "ChoHoanTien" or "DaHuy")
        {
            var tiLe = _svc.TinhTiLeHoan(don.NgayNhanPhong, don.NgayHuy);
            ViewBag.TiLeHoan   = tiLe;
            ViewBag.SoTienHoan = tien.SoTienDaTra * tiLe / 100m;
        }
        return View(new ChiTietDonViewModel
        {
            Don       = don,
            KhachHang = don.MaNguoiDungNavigation!,
            ChiTiets  = don.ChiTietDatPhongs.ToList(),
            TongTien  = tien.TongTien,
            DaTra     = tien.SoTienDaTra,
            ConLai    = tien.SoTienConLai,
            SoDem     = tien.SoDem
        });
    }

    // ── GET /LeTan/CheckIn/5 ─────────────────────────────────────────────────
    [Authorize(Policy = ChucNang.CheckInOut)]
    public async Task<IActionResult> CheckIn(int id)
    {
        var don = await _db.DatPhongs.Include(d => d.ThanhToans)
            .FirstOrDefaultAsync(d => d.MaDatPhong == id);
        if (don == null || don.TrangThai != "DaXacNhan")
        {
            TempData["Error"] = "Đơn không hợp lệ để check-in.";
            return RedirectToAction(nameof(ChiTietDon), new { id });
        }

        var tien = await _svc.TinhTienDonAsync(id);

        // Nếu còn tiền cần thu => hiện form ThuTien
        if (tien.SoTienConLai > 0)
        {
            return View("ThuTienCheckIn", new ThuTienCheckInViewModel
            {
                MaDatPhong   = id,
                MaDon        = don.MaDon,
                SoTienConLai = tien.SoTienConLai,
                SoTienThu    = tien.SoTienConLai
            });
        }

        // Không còn tiền => check-in thẳng
        return await ThucHienCheckIn(id);
    }

    // ── POST /LeTan/ThuTienCheckIn ───────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = ChucNang.CheckInOut)]
    public async Task<IActionResult> ThuTienCheckIn(ThuTienCheckInViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        _db.ThanhToans.Add(new ThanhToan
        {
            MaDatPhong    = vm.MaDatPhong,
            SoTien        = vm.SoTienThu,
            PhuongThuc    = vm.PhuongThuc,
            LoaiThanhToan = "ThanhToanDu",
            TrangThai     = "ThanhCong",
            NgayThanhToan = DateTime.Now,
            GhiChu        = vm.GhiChu
        });
        await _db.SaveChangesAsync();

        return await ThucHienCheckIn(vm.MaDatPhong);
    }

    private async Task<IActionResult> ThucHienCheckIn(int maDatPhong)
    {
        var don = await _db.DatPhongs
            .Include(d => d.ChiTietDatPhongs)
            .FirstOrDefaultAsync(d => d.MaDatPhong == maDatPhong);
        if (don == null) return NotFound();

        don.TrangThai   = "DangO";
        don.NgayCapNhat = DateTime.Now;

        // Đổi trạng thái tất cả phòng trong đơn => "DangO"
        foreach (var ct in don.ChiTietDatPhongs)
        {
            var phong = await _db.Phongs.FindAsync(ct.MaPhong);
            if (phong != null) phong.TrangThai = "DangO";
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Check-in thành công đơn {don.MaDon}.";
        return RedirectToAction(nameof(ChiTietDon), new { id = maDatPhong });
    }

    // ── POST /LeTan/CheckOut ─────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = ChucNang.CheckInOut)]
    public async Task<IActionResult> CheckOut(int maDatPhong)
    {
        var don = await _db.DatPhongs
            .Include(d => d.ChiTietDatPhongs)
            .FirstOrDefaultAsync(d => d.MaDatPhong == maDatPhong);
        if (don == null || don.TrangThai != "DangO")
        {
            TempData["Error"] = "Đơn không hợp lệ để check-out.";
            return RedirectToAction(nameof(ChiTietDon), new { id = maDatPhong });
        }

        don.TrangThai   = "DaTraPhong";
        don.NgayCapNhat = DateTime.Now;

        foreach (var ct in don.ChiTietDatPhongs)
        {
            var phong = await _db.Phongs.FindAsync(ct.MaPhong);
            if (phong != null) phong.TrangThai = "DangDonDep";
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Check-out thành công đơn {don.MaDon}. Phòng chuyển sang 'Đang dọn dẹp'.";
        return RedirectToAction(nameof(ChiTietDon), new { id = maDatPhong });
    }

    // ── GET /LeTan/DatWalkIn ─────────────────────────────────────────────────
    [HttpGet, Authorize(Policy = ChucNang.DatWalkIn)]
    public async Task<IActionResult> DatWalkIn(int? maLoaiPhong = null)
    {
        var loaiPhongs = await _db.LoaiPhongs
            .Where(l => l.TrangThai == true).OrderBy(l => l.GiaCoBan).ToListAsync();

        var phongs = await _db.Phongs
            .Where(p => p.TrangThai == "Trong")
            .Include(p => p.MaLoaiPhongNavigation)
            .OrderBy(p => p.MaLoaiPhong).ThenBy(p => p.SoPhong)
            .ToListAsync();

        var dichVus = await _db.DichVus
            .Where(d => d.TrangThai == true).OrderBy(d => d.TenDichVu).ToListAsync();

        var vm = new DatWalkInViewModel
        {
            NgayNhanPhong     = DateTime.Today,
            NgayTraPhong      = DateTime.Today.AddDays(1),
            MaLoaiPhong       = maLoaiPhong ?? 0,
            DanhSachLoaiPhong = loaiPhongs,
            DanhSachPhong     = phongs,
            DichVuChon        = dichVus.Select(d => new DichVuChonItem
            {
                MaDichVu  = d.MaDichVu,
                TenDichVu = d.TenDichVu,
                Gia       = d.Gia,
                DonVi     = d.DonVi,
                SoLuong   = 0
            }).ToList()
        };
        return View(vm);
    }

    // ── POST /LeTan/DatWalkIn ────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = ChucNang.DatWalkIn)]
    public async Task<IActionResult> DatWalkIn(DatWalkInViewModel vm)
    {
        vm.DanhSachLoaiPhong = await _db.LoaiPhongs
            .Where(l => l.TrangThai == true).OrderBy(l => l.GiaCoBan).ToListAsync();
        vm.DanhSachPhong = await _db.Phongs
            .Where(p => p.TrangThai == "Trong")
            .Include(p => p.MaLoaiPhongNavigation)
            .OrderBy(p => p.MaLoaiPhong).ThenBy(p => p.SoPhong).ToListAsync();

        // Bổ sung tên/giá cho DichVuChon nếu thiếu (model binding chỉ giữ SoLuong+MaDichVu)
        if (vm.DichVuChon.Any())
        {
            var dvIds = vm.DichVuChon.Select(d => d.MaDichVu).ToList();
            var dvDb  = await _db.DichVus.Where(d => dvIds.Contains(d.MaDichVu)).ToListAsync();
            foreach (var item in vm.DichVuChon)
            {
                var dv = dvDb.FirstOrDefault(d => d.MaDichVu == item.MaDichVu);
                if (dv != null) { item.TenDichVu = dv.TenDichVu; item.Gia = dv.Gia; item.DonVi = dv.DonVi; }
            }
        }
        else
        {
            // Nếu POST không gửi DichVuChon nào (không có dịch vụ), tải lại danh sách
            var dichVus = await _db.DichVus.Where(d => d.TrangThai == true).OrderBy(d => d.TenDichVu).ToListAsync();
            vm.DichVuChon = dichVus.Select(d => new DichVuChonItem
            {
                MaDichVu = d.MaDichVu, TenDichVu = d.TenDichVu, Gia = d.Gia, DonVi = d.DonVi, SoLuong = 0
            }).ToList();
        }

        if (!ModelState.IsValid) return View(vm);

        if (vm.NgayNhanPhong >= vm.NgayTraPhong)
        {
            ModelState.AddModelError("", "Ngày trả phòng phải sau ngày nhận phòng.");
            return View(vm);
        }

        var dnhan = DateOnly.FromDateTime(vm.NgayNhanPhong);
        var dtra  = DateOnly.FromDateTime(vm.NgayTraPhong);

        // Dùng phòng lễ tân đã chọn cụ thể
        var phong = await _db.Phongs
            .Include(p => p.MaLoaiPhongNavigation)
            .FirstOrDefaultAsync(p => p.MaPhong == vm.MaPhong);

        if (phong == null)
        {
            ModelState.AddModelError("MaPhong", "Phòng không tồn tại.");
            return View(vm);
        }

        // Kiểm tra phòng đó có bị trùng lịch không
        var phongDaBiet = _db.ChiTietDatPhongs
            .Where(ct => ct.MaPhong == vm.MaPhong
                      && ct.MaDatPhongNavigation.TrangThai != "DaHuy"
                      && ct.MaDatPhongNavigation.TrangThai != "DaTraPhong"
                      && ct.MaDatPhongNavigation.NgayNhanPhong < dtra
                      && ct.MaDatPhongNavigation.NgayTraPhong > dnhan);
        if (await phongDaBiet.AnyAsync())
        {
            ModelState.AddModelError("MaPhong", $"Phòng {phong.SoPhong} đã có khách trong khoảng ngày này. Vui lòng chọn phòng khác.");
            return View(vm);
        }

        var loaiPhong = phong.MaLoaiPhongNavigation
                      ?? await _db.LoaiPhongs.FindAsync(phong.MaLoaiPhong);
        if (loaiPhong == null) return NotFound();

        // Đặt MaLoaiPhong theo phòng được chọn (bất kể dropdown type)
        vm.MaLoaiPhong = phong.MaLoaiPhong;

        // ── TRANSACTION Serializable: tránh race condition overbooking ─────────
        DatPhong? don = null;
        var ketQua = KetQuaDat.ThanhCong;

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                // Re-check phòng trong transaction - tránh 2 lễ tân cùng đặt phòng
                bool phongBiTrung = await _db.ChiTietDatPhongs
                    .AnyAsync(ct => ct.MaPhong == vm.MaPhong
                                 && ct.MaDatPhongNavigation.TrangThai != "DaHuy"
                                 && ct.MaDatPhongNavigation.TrangThai != "DaTraPhong"
                                 && ct.MaDatPhongNavigation.NgayNhanPhong < dtra
                                 && ct.MaDatPhongNavigation.NgayTraPhong > dnhan);
                if (phongBiTrung)
                {
                    ketQua = KetQuaDat.HetPhong;
                    await tx.RollbackAsync();
                    return;
                }

                // Tạo NguoiDung cho khách walk-in (UserId = NULL)
                var nguoiDung = new NguoiDung
                {
                    UserId      = null,
                    HoTen       = vm.HoTen,
                    SoDienThoai = vm.SoDienThoai,
                    CCCD        = vm.CCCD,
                    Email       = vm.Email,
                    VaiTro      = "KhachHang"
                };
                _db.NguoiDungs.Add(nguoiDung);
                await _db.SaveChangesAsync();

                // Tạo DatPhong (WalkIn)
                don = new DatPhong
                {
                    MaNguoiDung    = nguoiDung.MaNguoiDung,
                    MaDon          = _svc.SinhMaDon(),
                    NguonDat       = "WalkIn",
                    NgayNhanPhong  = dnhan,
                    NgayTraPhong   = dtra,
                    TongSoNguoiLon = vm.SoNguoiLon,
                    TongSoTreEm    = vm.SoTreEm,
                    LoaiThanhToan  = "ThanhToanDu",
                    TrangThai      = "DaXacNhan",
                    GhiChu         = vm.GhiChu
                };
                _db.DatPhongs.Add(don);
                await _db.SaveChangesAsync();

                // Tạo ChiTietDatPhong (snapshot giá)
                var chiTiet = new ChiTietDatPhong
                {
                    MaDatPhong     = don.MaDatPhong,
                    MaPhong        = phong.MaPhong,
                    SoNguoiLon     = vm.SoNguoiLon,
                    SoTreEm        = vm.SoTreEm,
                    GiaMotDem      = loaiPhong.GiaCoBan,
                    TenKhachLuuTru = vm.HoTen
                };
                _db.ChiTietDatPhongs.Add(chiTiet);
                await _db.SaveChangesAsync();

                // Tạo ChiTietDichVu nếu có dịch vụ chọn
                foreach (var item in vm.DichVuChon.Where(d => d.SoLuong > 0))
                {
                    var dv = await _db.DichVus.FindAsync(item.MaDichVu);
                    if (dv == null) continue;
                    _db.ChiTietDichVus.Add(new ChiTietDichVu
                    {
                        MaChiTiet = chiTiet.MaChiTiet,
                        MaDichVu  = item.MaDichVu,
                        SoLuong   = item.SoLuong,
                        DonGia    = dv.Gia
                    });
                }
                await _db.SaveChangesAsync();

                // Thu tiền nếu có - cap tối đa theo tổng tiền server tính
                if (vm.SoTienThu > 0)
                {
                    var tienDon = await _svc.TinhTienDonAsync(don.MaDatPhong);
                    var soTienThucThu = Math.Min(vm.SoTienThu, tienDon.TongTien);
                    if (soTienThucThu > 0)
                    {
                        _db.ThanhToans.Add(new ThanhToan
                        {
                            MaDatPhong    = don.MaDatPhong,
                            SoTien        = soTienThucThu,
                            PhuongThuc    = vm.PhuongThuc,
                            LoaiThanhToan = "ThanhToanDu",
                            TrangThai     = "ThanhCong",
                            NgayThanhToan = DateTime.Now
                        });
                        await _db.SaveChangesAsync();
                    }
                }

                // Check-in ngay nếu được chọn
                if (vm.CheckInNgay)
                {
                    don.TrangThai   = "DangO";
                    phong.TrangThai = "DangO";
                    don.NgayCapNhat = DateTime.Now;
                    await _db.SaveChangesAsync();
                }

                await tx.CommitAsync();
            }
            catch (Exception ex) when (LaLoiTranhChap(ex))
            {
                try { await tx.RollbackAsync(); } catch { }
                ketQua = KetQuaDat.TranhChap;
            }
        });

        if (ketQua == KetQuaDat.HetPhong)
        {
            ModelState.AddModelError("MaPhong", $"Phòng {phong.SoPhong} vừa được đặt bởi người khác. Vui lòng chọn phòng khác.");
            return View(vm);
        }
        if (ketQua == KetQuaDat.TranhChap)
        {
            ModelState.AddModelError("MaPhong", "Phòng vừa có người khác đặt, vui lòng chọn lại.");
            return View(vm);
        }

        TempData["Success"] = $"Tạo đơn walk-in {don!.MaDon} thành công."
            + (vm.CheckInNgay ? " Khách đã check-in." : "");
        return RedirectToAction(nameof(ChiTietDon), new { id = don.MaDatPhong });
    }

    // ── GET /LeTan/ChuyenPhong/5 ─────────────────────────────────────────────
    [Authorize(Policy = ChucNang.ChuyenGiaHan)]
    // Cho phép chuyển phòng ở mọi trạng thái trước và trong khi ở:
    // ChoXacNhan / DaXacNhan => chuyển phòng trước check-in (kiểm tra theo ngày đặt)
    // DangO                  => chuyển phòng đang ở (kiểm tra TrangThai=Trong)
    public async Task<IActionResult> ChuyenPhong(int id)
    {
        var don = await _db.DatPhongs
            .Include(d => d.ChiTietDatPhongs)
                .ThenInclude(ct => ct.MaPhongNavigation)
                    .ThenInclude(p => p.MaLoaiPhongNavigation)
            .FirstOrDefaultAsync(d => d.MaDatPhong == id);

        if (don == null || don.TrangThai is not ("ChoXacNhan" or "DaXacNhan" or "DangO"))
        {
            TempData["Error"] = "Không thể chuyển phòng ở trạng thái này.";
            return RedirectToAction(nameof(ChiTietDon), new { id });
        }

        var ct = don.ChiTietDatPhongs.First();
        var phongHien = ct.MaPhongNavigation!;

        List<Phong> phongTrong;

        if (don.TrangThai == "DangO")
        {
            // Đang ở => chỉ lấy phòng TrangThai="Trong"
            phongTrong = await _db.Phongs
                .Where(p => p.TrangThai == "Trong" && p.MaPhong != phongHien.MaPhong)
                .Include(p => p.MaLoaiPhongNavigation)
                .OrderBy(p => p.SoPhong)
                .ToListAsync();
        }
        else
        {
            // Trước check-in => kiểm tra theo khoảng ngày đặt
            var phongDaBiet = _db.ChiTietDatPhongs
                .Where(x => x.MaPhong != phongHien.MaPhong
                         && x.MaDatPhong != id
                         && x.MaDatPhongNavigation.TrangThai != "DaHuy"
                         && x.MaDatPhongNavigation.TrangThai != "DaTraPhong"
                         && x.MaDatPhongNavigation.NgayNhanPhong < don.NgayTraPhong
                         && x.MaDatPhongNavigation.NgayTraPhong > don.NgayNhanPhong)
                .Select(x => x.MaPhong);

            phongTrong = await _db.Phongs
                .Where(p => p.TrangThai == "Trong"
                         && p.MaPhong != phongHien.MaPhong
                         && !phongDaBiet.Contains(p.MaPhong))
                .Include(p => p.MaLoaiPhongNavigation)
                .OrderBy(p => p.SoPhong)
                .ToListAsync();
        }

        return View(new ChuyenPhongViewModel
        {
            MaDatPhong         = id,
            MaDon              = don.MaDon,
            PhongHien          = phongHien.SoPhong,
            LoaiPhongHien      = phongHien.MaLoaiPhongNavigation?.TenLoaiPhong ?? "-",
            DanhSachPhongTrong = phongTrong
        });
    }

    // ── POST /LeTan/ChuyenPhong ──────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = ChucNang.ChuyenGiaHan)]
    public async Task<IActionResult> ChuyenPhong(ChuyenPhongViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.DanhSachPhongTrong = await _db.Phongs
                .Where(p => p.TrangThai == "Trong").Include(p => p.MaLoaiPhongNavigation).ToListAsync();
            return View(vm);
        }

        var don = await _db.DatPhongs
            .Include(d => d.ChiTietDatPhongs)
            .FirstOrDefaultAsync(d => d.MaDatPhong == vm.MaDatPhong);
        if (don == null) return NotFound();

        var phongMoi = await _db.Phongs.FindAsync(vm.MaPhongMoi);
        if (phongMoi == null)
        {
            TempData["Error"] = "Phòng không tồn tại.";
            return RedirectToAction(nameof(ChuyenPhong), new { id = vm.MaDatPhong });
        }

        var ct      = don.ChiTietDatPhongs.First();
        var phongCu = await _db.Phongs.FindAsync(ct.MaPhong);

        ct.MaPhong = vm.MaPhongMoi;

        // Chỉ đổi TrangThai phòng khi đang ở (check-in rồi)
        if (don.TrangThai == "DangO")
        {
            if (phongCu != null) phongCu.TrangThai = "DangDonDep";
            phongMoi.TrangThai = "DangO";
        }

        don.GhiChu      = (don.GhiChu ?? "") + $"\n[Chuyển phòng {phongCu?.SoPhong}=>{phongMoi.SoPhong}: {vm.LyDo}]";
        don.NgayCapNhat = DateTime.Now;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Chuyển phòng thành công: {phongCu?.SoPhong} => {phongMoi.SoPhong}.";
        return RedirectToAction(nameof(ChiTietDon), new { id = vm.MaDatPhong });
    }


    // ── GET /LeTan/GiaHan/5 ──────────────────────────────────────────────────
    [Authorize(Policy = ChucNang.ChuyenGiaHan)]
    public async Task<IActionResult> GiaHan(int id)
    {
        var don = await _db.DatPhongs
            .Include(d => d.MaNguoiDungNavigation)
            .Include(d => d.ChiTietDatPhongs)
            .FirstOrDefaultAsync(d => d.MaDatPhong == id);

        if (don == null || don.TrangThai != "DangO")
        {
            TempData["Error"] = "Chỉ có thể gia hạn khi khách đang ở.";
            return RedirectToAction(nameof(ChiTietDon), new { id });
        }

        var tien = await _svc.TinhTienDonAsync(id);
        return View(new GiaHanViewModel
        {
            MaDatPhong          = id,
            MaDon               = don.MaDon,
            TenKhach            = don.MaNguoiDungNavigation?.HoTen ?? "-",
            NgayNhanPhong       = don.NgayNhanPhong,
            NgayTraPhongHien    = don.NgayTraPhong,
            TongTienHien        = tien.TongTien,
            NgayTraPhongMoi     = don.NgayTraPhong.ToDateTime(TimeOnly.MinValue).AddDays(1)
        });
    }

    // ── POST /LeTan/GiaHan ───────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = ChucNang.ChuyenGiaHan)]
    public async Task<IActionResult> GiaHan(GiaHanViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var don = await _db.DatPhongs
            .Include(d => d.ChiTietDatPhongs)
            .FirstOrDefaultAsync(d => d.MaDatPhong == vm.MaDatPhong);
        if (don == null) return NotFound();

        var ngayMoi = DateOnly.FromDateTime(vm.NgayTraPhongMoi);

        if (ngayMoi <= don.NgayTraPhong)
        {
            ModelState.AddModelError("NgayTraPhongMoi", "Ngày trả mới phải sau ngày trả hiện tại.");
            return View(vm);
        }

        // Kiểm tra phòng không bị đơn khác đặt trong khoảng gia hạn
        var ct = don.ChiTietDatPhongs.First();
        var phongDaBiet = _db.ChiTietDatPhongs
            .Where(x => x.MaPhong == ct.MaPhong
                     && x.MaDatPhong != don.MaDatPhong
                     && x.MaDatPhongNavigation.TrangThai != "DaHuy"
                     && x.MaDatPhongNavigation.TrangThai != "DaTraPhong"
                     && x.MaDatPhongNavigation.NgayNhanPhong < ngayMoi
                     && x.MaDatPhongNavigation.NgayTraPhong > don.NgayTraPhong);

        if (await phongDaBiet.AnyAsync())
        {
            ModelState.AddModelError("NgayTraPhongMoi", "Phòng đã có đơn khác trong khoảng gia hạn. Không thể gia hạn.");
            return View(vm);
        }

        don.NgayTraPhong = ngayMoi;
        don.NgayCapNhat  = DateTime.Now;
        await _db.SaveChangesAsync();

        var tienMoi = await _svc.TinhTienDonAsync(don.MaDatPhong);
        TempData["Success"] = $"Gia hạn thành công đến {ngayMoi:dd/MM/yyyy}. Tổng tiền mới: {tienMoi.TongTien:N0} ₫.";
        return RedirectToAction(nameof(ChiTietDon), new { id = vm.MaDatPhong });
    }

    // ── POST /LeTan/XacNhanDon ───────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = ChucNang.TraCuuDon)]
    public async Task<IActionResult> XacNhanDon(int maDatPhong)
    {
        var don = await _db.DatPhongs.FindAsync(maDatPhong);
        if (don != null && don.TrangThai == "ChoXacNhan")
        {
            don.TrangThai   = "DaXacNhan";
            don.NgayCapNhat = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đơn đã được xác nhận.";
        }
        return RedirectToAction(nameof(ChiTietDon), new { id = maDatPhong });
    }

    // ── Helper: lấy MaNguoiDung của nhân viên đang đăng nhập ─────────────────
    private async Task<int?> GetCurrentMaNguoiDungAsync()
    {
        var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        var nd = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.UserId == userId);
        return nd?.MaNguoiDung;
    }

    private enum KetQuaDat { ThanhCong, HetPhong, TranhChap }

    private static bool LaLoiTranhChap(Exception ex) =>
        (ex is Microsoft.EntityFrameworkCore.DbUpdateException due
            && due.InnerException is SqlException s1
            && (s1.Number == 1205 || s1.Number == 1222))
        || (ex is SqlException s2 && (s2.Number == 1205 || s2.Number == 1222));
}
