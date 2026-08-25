using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Services;
using DatPhongKhachSan.Services.VnPay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DatPhongKhachSan.Controllers;

public class ThanhToanController : Controller
{
    private readonly DatPhongKhachSanContext _db;
    private readonly DatPhongService _svc;
    private readonly EmailService _email;
    private readonly VnPayConfig _vnpay;

    public ThanhToanController(
        DatPhongKhachSanContext db,
        DatPhongService svc,
        EmailService email,
        IOptions<VnPayConfig> vnpay)
    {
        _db    = db;
        _svc   = svc;
        _email = email;
        _vnpay = vnpay.Value;
    }

    // ── GET /ThanhToan/VnPayReturn (callback từ VNPay) ────────────────────────
    public async Task<IActionResult> VnPayReturn()
    {
        // Xác minh chữ ký
        bool valid = VnPayLibrary.VerifySignature(Request.Query, _vnpay.HashSecret, out var responseCode);

        var maDon = Request.Query["vnp_TxnRef"].ToString();
        var maGiaoDich = Request.Query["vnp_TransactionNo"].ToString();

        var don = await _db.DatPhongs
            .Include(d => d.ThanhToans)
            .Include(d => d.MaNguoiDungNavigation)
            .FirstOrDefaultAsync(d => d.MaDon == maDon);

        if (don == null) return RedirectToAction("ThatBai", new { msg = "Không tìm thấy đơn." });

        // Tìm ThanhToan đang chờ xử lý
        var thanhToan = don.ThanhToans.FirstOrDefault(t => t.TrangThai == "ChoXuLy");
        if (thanhToan == null)
        {
            // Có thể callback đến 2 lần - nếu đã xử lý thì redirect thẳng
            bool daThanhCong = don.ThanhToans.Any(t => t.TrangThai == "ThanhCong");
            return daThanhCong
                ? RedirectToAction("ThanhCong", new { maDon })
                : RedirectToAction("ThatBai", new { msg = "Không tìm thấy giao dịch chờ xử lý." });
        }

        if (valid && responseCode == "00")
        {
            var now = DateTime.Now;
            thanhToan.TrangThai     = "ThanhCong";
            thanhToan.MaGiaoDich    = maGiaoDich;
            thanhToan.NgayThanhToan = now;

            // Đơn có thể đã bị auto-hủy (khách đóng tab quá 15 phút)
            if (don.TrangThai == "DaHuy")
            {
                don.TrangThai   = "ChoHoanTien";
                don.NgayCapNhat = now;
                await _db.SaveChangesAsync();
                return RedirectToAction("ThatBai",
                    new { msg = "Đơn đã hết hạn giữ phòng, tiền sẽ được hoàn lại cho bạn." });
            }

            // Thanh toán thành công, đơn còn hợp lệ
            don.TrangThai   = "DaXacNhan";
            don.NgayCapNhat = now;

            await _db.SaveChangesAsync();

            // Gửi email xác nhận
            var nguoiDung = don.MaNguoiDungNavigation;
            var tien      = await _svc.TinhTienDonAsync(don.MaDatPhong);
            var toEmail   = nguoiDung.Email ?? string.Empty;
            var toName    = nguoiDung.HoTen;
            if (!string.IsNullOrEmpty(toEmail))
                await _email.GuiXacNhanDatPhongAsync(toEmail, toName, don, tien.TongTien);

            return RedirectToAction("ThanhCong", new { maDon });
        }
        else
        {
            // Thanh toán thất bại
            thanhToan.TrangThai  = "ThatBai";
            thanhToan.GhiChu     = $"ResponseCode={responseCode}";
            don.TrangThai        = "DaHuy";
            don.NgayCapNhat      = DateTime.Now;

            await _db.SaveChangesAsync();

            return RedirectToAction("ThatBai", new { maDon, responseCode });
        }
    }

    // ── GET /ThanhToan/VnPayIPN (server-to-server callback từ VNPay) ─────────
    // VNPay gọi URL này dù trình duyệt khách có hoạt động hay không.
    // Phải trả về JSON {"RspCode":"00","Message":"Confirm Success"} khi thành công.
    [AllowAnonymous]
    public async Task<IActionResult> VnPayIPN()
    {
        bool valid = VnPayLibrary.VerifySignature(Request.Query, _vnpay.HashSecret, out var responseCode);
        if (!valid)
            return Json(new { RspCode = "97", Message = "Invalid signature" });

        var maDon      = Request.Query["vnp_TxnRef"].ToString();
        var maGiaoDich = Request.Query["vnp_TransactionNo"].ToString();

        if (!long.TryParse(Request.Query["vnp_Amount"].ToString(), out long vnpAmount))
            return Json(new { RspCode = "99", Message = "Invalid amount format" });

        var don = await _db.DatPhongs
            .Include(d => d.ThanhToans)
            .FirstOrDefaultAsync(d => d.MaDon == maDon);

        if (don == null)
            return Json(new { RspCode = "01", Message = "Order not found" });

        var thanhToan = don.ThanhToans.FirstOrDefault(t => t.TrangThai == "ChoXuLy");
        if (thanhToan == null)
        {
            bool daThanhCong = don.ThanhToans.Any(t => t.TrangThai == "ThanhCong");
            return Json(new { RspCode = daThanhCong ? "02" : "01",
                              Message = daThanhCong ? "Order already confirmed" : "No pending payment" });
        }

        // Xác minh số tiền khớp (VNPay gửi amount × 100)
        long expectedAmount = (long)(thanhToan.SoTien * 100);
        if (vnpAmount != expectedAmount)
            return Json(new { RspCode = "04", Message = "Invalid amount" });

        var now = DateTime.Now;
        if (responseCode == "00")
        {
            thanhToan.TrangThai     = "ThanhCong";
            thanhToan.MaGiaoDich    = maGiaoDich;
            thanhToan.NgayThanhToan = now;

            // Đơn có thể đã bị auto-hủy trước khi IPN đến
            don.TrangThai   = don.TrangThai == "DaHuy" ? "ChoHoanTien" : "DaXacNhan";
            don.NgayCapNhat = now;
        }
        else
        {
            thanhToan.TrangThai = "ThatBai";
            thanhToan.GhiChu    = $"IPN ResponseCode={responseCode}";
            don.TrangThai       = "DaHuy";
            don.NgayCapNhat     = now;
        }

        await _db.SaveChangesAsync();
        return Json(new { RspCode = "00", Message = "Confirm Success" });
    }

    // ── GET /ThanhToan/ThanhCong ──────────────────────────────────────────────
    public async Task<IActionResult> ThanhCong(string maDon)
    {
        var don = await _db.DatPhongs
            .Include(d => d.ChiTietDatPhongs)
                .ThenInclude(ct => ct.MaPhongNavigation)
                    .ThenInclude(p => p.MaLoaiPhongNavigation)
            .Include(d => d.ChiTietDatPhongs)
                .ThenInclude(ct => ct.ChiTietDichVus)
            .Include(d => d.ThanhToans)
            .FirstOrDefaultAsync(d => d.MaDon == maDon);

        if (don == null) return NotFound();

        var tien = await _svc.TinhTienDonAsync(don.MaDatPhong);
        ViewBag.TinhTien = tien;
        return View(don);
    }

    // ── GET /ThanhToan/ThatBai ────────────────────────────────────────────────
    public IActionResult ThatBai(string? maDon, string? msg, string? responseCode)
    {
        ViewBag.MaDon        = maDon;
        ViewBag.Msg          = msg;
        ViewBag.ResponseCode = responseCode;
        return View();
    }
}
