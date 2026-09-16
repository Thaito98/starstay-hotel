using System.Security.Claims;
using DatPhongKhachSan.Areas.Identity.Data;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Models.ViewModels;
using DatPhongKhachSan.Services;
using DatPhongKhachSan.Services.VnPay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace DatPhongKhachSan.Controllers;

// Create: AllowAnonymous (khách đặt nhanh không cần đăng nhập - plan 5.11)
// LichSu / HuyDon: chỉ KhachHang đã đăng nhập mới dùng được
public class DatPhongController : Controller
{
    private readonly DatPhongKhachSanContext _db;
    private readonly DatPhongService _svc;
    private readonly EmailService _email;
    private readonly VnPayConfig _vnpay;
    private readonly UserManager<DatPhongKhachSanUser> _userMgr;

    public DatPhongController(
        DatPhongKhachSanContext db,
        DatPhongService svc,
        EmailService email,
        IOptions<VnPayConfig> vnpay,
        UserManager<DatPhongKhachSanUser> userMgr)
    {
        _db      = db;
        _svc     = svc;
        _email   = email;
        _vnpay   = vnpay.Value;
        _userMgr = userMgr;
    }

    // ── GET /DatPhong/Create ──────────────────────────────────────────────────
    [AllowAnonymous]
    public async Task<IActionResult> Create(int maLoaiPhong, DateTime? ngayNhan, DateTime? ngayTra,
                                            int soNguoiLon = 1, int soTreEm = 0)
    {
        var loaiPhong = await _db.LoaiPhongs
            .Include(lp => lp.AnhPhongs)
            .Include(lp => lp.MaTienNghis)
            .FirstOrDefaultAsync(lp => lp.MaLoaiPhong == maLoaiPhong && lp.TrangThai == true);

        if (loaiPhong == null) return NotFound();

        var dichVus = await _db.DichVus.Where(d => d.TrangThai == true).ToListAsync();

        var vm = new DatPhongViewModel
        {
            MaLoaiPhong   = maLoaiPhong,
            NgayNhanPhong = ngayNhan ?? DateTime.Today.AddDays(1),
            NgayTraPhong  = ngayTra  ?? DateTime.Today.AddDays(2),
            SoNguoiLon    = soNguoiLon,
            SoTreEm       = soTreEm,
            LoaiPhong     = loaiPhong,
            DichVuKhaDung = dichVus,
            DichVuChon    = dichVus.Select(d => new DichVuChonItem
            {
                MaDichVu  = d.MaDichVu,
                TenDichVu = d.TenDichVu,
                Gia       = d.Gia,
                DonVi     = d.DonVi,
                SoLuong   = 0
            }).ToList()
        };

        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userMgr.GetUserAsync(User);
            vm.TenKhachLuuTru = user?.UserName ?? string.Empty;
        }

        if (ngayNhan.HasValue && ngayTra.HasValue)
        {
            var phong = await _svc.TimPhongTrong(maLoaiPhong,
                DateOnly.FromDateTime(ngayNhan.Value), DateOnly.FromDateTime(ngayTra.Value));
            vm.SoPhongConTrong = phong != null ? 1 : 0;
        }
        else
        {
            vm.SoPhongConTrong = await _db.Phongs.CountAsync(p =>
                p.MaLoaiPhong == maLoaiPhong && p.TrangThai == "Trong");
        }

        return View(vm);
    }

    // ── POST /DatPhong/Create ─────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    public async Task<IActionResult> Create(DatPhongViewModel vm)
    {
        vm.LoaiPhong     = await _db.LoaiPhongs
            .Include(lp => lp.AnhPhongs).Include(lp => lp.MaTienNghis)
            .FirstOrDefaultAsync(lp => lp.MaLoaiPhong == vm.MaLoaiPhong);
        vm.DichVuKhaDung = await _db.DichVus.Where(d => d.TrangThai == true).ToListAsync();

        bool isAnonymous = !(User.Identity?.IsAuthenticated == true);

        // ── Validate theo nhánh ────────────────────────────────────────────────
        if (isAnonymous)
        {
            if (string.IsNullOrWhiteSpace(vm.HoTen))
                ModelState.AddModelError("HoTen", "Vui lòng nhập họ tên để đặt nhanh");
            if (string.IsNullOrWhiteSpace(vm.SoDienThoai))
                ModelState.AddModelError("SoDienThoai", "Vui lòng nhập số điện thoại để liên hệ");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(vm.TenKhachLuuTru))
                ModelState.AddModelError("TenKhachLuuTru", "Vui lòng nhập tên khách lưu trú");
        }

        if (!ModelState.IsValid) return View(vm);

        if (vm.NgayNhanPhong >= vm.NgayTraPhong)
        {
            ModelState.AddModelError("", "Ngày trả phòng phải sau ngày nhận phòng");
            return View(vm);
        }
        if (vm.NgayNhanPhong < ThoiGian.Today)
        {
            ModelState.AddModelError("", "Ngày nhận phòng không thể là ngày trong quá khứ");
            return View(vm);
        }

        var dnhan = DateOnly.FromDateTime(vm.NgayNhanPhong);
        var dtra  = DateOnly.FromDateTime(vm.NgayTraPhong);

        // Kiểm tra trước khi mở transaction (cho UX nhanh)
        var phong = await _svc.TimPhongTrong(vm.MaLoaiPhong, dnhan, dtra);
        if (phong == null)
        {
            ModelState.AddModelError("", "Không còn phòng trống trong khoảng ngày đã chọn");
            return View(vm);
        }

        // ── TRANSACTION Serializable: tránh race condition overbooking ─────────
        DatPhong? don = null;
        decimal soTienTT = 0m;
        var ketQua = KetQuaDat.ThanhCong;

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                // Re-check phòng ngay trong transaction - tránh 2 request cùng đặt 1 phòng
                phong = await _svc.TimPhongTrong(vm.MaLoaiPhong, dnhan, dtra);
                if (phong == null)
                {
                    ketQua = KetQuaDat.HetPhong;
                    await tx.RollbackAsync();
                    return;
                }

                // ── Tìm / tạo NguoiDung ────────────────────────────────────────────
                NguoiDung nguoiDung;
                if (isAnonymous)
                {
                    nguoiDung = new NguoiDung
                    {
                        UserId      = null,
                        HoTen       = vm.HoTen!.Trim(),
                        SoDienThoai = vm.SoDienThoai?.Trim(),
                        VaiTro      = "KhachHang"
                    };
                    _db.NguoiDungs.Add(nguoiDung);
                    await _db.SaveChangesAsync();
                    vm.TenKhachLuuTru = nguoiDung.HoTen;
                }
                else
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                    nguoiDung = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.UserId == userId)
                              ?? await TaoNguoiDungTuUserAsync(userId, vm.TenKhachLuuTru);
                }

                // ── Tạo DatPhong ───────────────────────────────────────────────────
                don = new DatPhong
                {
                    MaNguoiDung    = nguoiDung.MaNguoiDung,
                    MaDon          = _svc.SinhMaDon(),
                    NguonDat       = "Online",
                    NgayNhanPhong  = dnhan,
                    NgayTraPhong   = dtra,
                    TongSoNguoiLon = vm.SoNguoiLon,
                    TongSoTreEm    = vm.SoTreEm,
                    LoaiThanhToan  = vm.LoaiThanhToan,
                    TrangThai      = "ChoXacNhan",
                    GhiChu         = vm.GhiChu
                };
                _db.DatPhongs.Add(don);
                await _db.SaveChangesAsync();

                // ── ChiTietDatPhong ────────────────────────────────────────────────
                var chiTiet = new ChiTietDatPhong
                {
                    MaDatPhong     = don.MaDatPhong,
                    MaPhong        = phong.MaPhong,
                    SoNguoiLon     = vm.SoNguoiLon,
                    SoTreEm        = vm.SoTreEm,
                    GiaMotDem      = vm.LoaiPhong!.GiaCoBan,
                    TenKhachLuuTru = vm.TenKhachLuuTru
                };
                _db.ChiTietDatPhongs.Add(chiTiet);
                await _db.SaveChangesAsync();

                // ── ChiTietDichVu ──────────────────────────────────────────────────
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

                // ── Tính tiền + ThanhToan ChoXuLy ─────────────────────────────────
                var tien = await _svc.TinhTienDonAsync(don.MaDatPhong);
                soTienTT = vm.LoaiThanhToan == "Coc30"
                    ? Math.Round(tien.TongTien * 0.3m)
                    : tien.TongTien;

                _db.ThanhToans.Add(new ThanhToan
                {
                    MaDatPhong    = don.MaDatPhong,
                    SoTien        = soTienTT,
                    PhuongThuc    = "VNPay",
                    LoaiThanhToan = vm.LoaiThanhToan == "Coc30" ? "DatCoc" : "ThanhToanDu",
                    TrangThai     = "ChoXuLy"
                });
                await _db.SaveChangesAsync();

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
            ModelState.AddModelError("", "Phòng vừa hết - vui lòng chọn ngày hoặc loại phòng khác.");
            return View(vm);
        }
        if (ketQua == KetQuaDat.TranhChap)
        {
            ModelState.AddModelError("", "Phòng vừa có người khác đặt, vui lòng chọn lại.");
            return View(vm);
        }

        // ── Build URL VNPay (sau khi commit) ──────────────────────────────────
        var vnp = new VnPayLibrary();
        var now = ThoiGian.Now;
        vnp.AddData("vnp_Version",    _vnpay.Version);
        vnp.AddData("vnp_Command",    _vnpay.Command);
        vnp.AddData("vnp_TmnCode",    _vnpay.TmnCode);
        vnp.AddData("vnp_Amount",     ((long)(soTienTT * 100)).ToString());
        vnp.AddData("vnp_CurrCode",   _vnpay.CurrCode);
        vnp.AddData("vnp_TxnRef",     don!.MaDon);
        vnp.AddData("vnp_OrderInfo",  $"Dat phong {don.MaDon}");
        vnp.AddData("vnp_OrderType",  _vnpay.OrderType);
        vnp.AddData("vnp_Locale",     _vnpay.Locale);
        var returnUrl = $"{Request.Scheme}://{Request.Host}/ThanhToan/VnPayReturn";
        vnp.AddData("vnp_ReturnUrl",  returnUrl);
        vnp.AddData("vnp_IpAddr",     HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
        vnp.AddData("vnp_CreateDate", now.ToString("yyyyMMddHHmmss"));
        vnp.AddData("vnp_ExpireDate", now.AddMinutes(_vnpay.TimeoutMinutes).ToString("yyyyMMddHHmmss"));

        return Redirect(vnp.BuildUrl(_vnpay.BaseUrl, _vnpay.HashSecret));
    }

    // ── GET /DatPhong/LichSu - chỉ khách đã đăng nhập ───────────────────────
    [Authorize(Roles = "KhachHang")]
    public async Task<IActionResult> LichSu()
    {
        await _svc.HuyDonHetHanAsync();   // dọn đơn hết hạn trước khi hiển thị

        var userId    = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var nguoiDung = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.UserId == userId);
        if (nguoiDung == null) return View(new List<LichSuDonViewModel>());

        var dons = await _db.DatPhongs
            .Where(d => d.MaNguoiDung == nguoiDung.MaNguoiDung)
            .Include(d => d.ChiTietDatPhongs)
                .ThenInclude(ct => ct.MaPhongNavigation)
                    .ThenInclude(p => p.MaLoaiPhongNavigation)
            .Include(d => d.ChiTietDatPhongs)
                .ThenInclude(ct => ct.ChiTietDichVus)
            .Include(d => d.ThanhToans)
            .OrderByDescending(d => d.NgayDat)
            .ToListAsync();

        var result = new List<LichSuDonViewModel>();
        foreach (var d in dons)
        {
            var tien     = await _svc.TinhTienDonAsync(d.MaDatPhong);
            var ct       = d.ChiTietDatPhongs.FirstOrDefault();
            int tiLeHoan = _svc.TinhTiLeHoan(d.NgayNhanPhong);

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
                TongTien      = tien.TongTien,
                DaTra         = tien.SoTienDaTra,
                ConLai        = tien.SoTienConLai,
                NgayDat       = d.NgayDat,
                CoTheHuy      = d.TrangThai is "ChoXacNhan" or "DaXacNhan",
                TiLeHoan      = tiLeHoan
            });
        }

        return View(result);
    }

    // ── POST /DatPhong/HuyDon - chỉ khách đã đăng nhập ──────────────────────
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "KhachHang")]
    public async Task<IActionResult> HuyDon(int maDatPhong)
    {
        var userId    = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var nguoiDung = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.UserId == userId);
        if (nguoiDung == null) return Forbid();

        var don = await _db.DatPhongs
            .Include(d => d.ThanhToans)
            .FirstOrDefaultAsync(d => d.MaDatPhong == maDatPhong
                                   && d.MaNguoiDung == nguoiDung.MaNguoiDung);
        if (don == null) return NotFound();

        if (don.TrangThai is not ("ChoXacNhan" or "DaXacNhan"))
        {
            TempData["Error"] = "Không thể hủy đơn ở trạng thái này.";
            return RedirectToAction(nameof(LichSu));
        }

        bool daCoPay    = don.ThanhToans.Any(t => t.TrangThai == "ThanhCong");
        don.TrangThai   = daCoPay ? "ChoHoanTien" : "DaHuy";
        don.NgayCapNhat = DateTime.Now;
        don.NgayHuy     = DateTime.Now;
        await _db.SaveChangesAsync();

        TempData["Success"] = daCoPay
            ? "Đơn đã hủy. Chúng tôi sẽ liên hệ hoàn tiền theo chính sách."
            : "Đơn đã hủy thành công.";
        return RedirectToAction(nameof(LichSu));
    }

    // ── Helper: tạo NguoiDung từ tài khoản Identity (khi chưa có) ────────────
    private async Task<NguoiDung> TaoNguoiDungTuUserAsync(string userId, string hoTen)
    {
        var user = await _userMgr.FindByIdAsync(userId);
        var nd = new NguoiDung
        {
            UserId      = userId,
            HoTen       = hoTen,
            Email       = user?.Email,
            SoDienThoai = user?.PhoneNumber,
            VaiTro      = "KhachHang"
        };
        _db.NguoiDungs.Add(nd);
        await _db.SaveChangesAsync();
        return nd;
    }

    private enum KetQuaDat { ThanhCong, HetPhong, TranhChap }

    private static bool LaLoiTranhChap(Exception ex) =>
        (ex is Microsoft.EntityFrameworkCore.DbUpdateException due
            && due.InnerException is SqlException s1
            && (s1.Number == 1205 || s1.Number == 1222))
        || (ex is SqlException s2 && (s2.Number == 1205 || s2.Number == 1222));
}
