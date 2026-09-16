using System.Security.Claims;
using DatPhongKhachSan.Constants;
using DatPhongKhachSan.Areas.Identity.Data;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = ChucNang.QuanLyNguoiDung)]
public class NguoiDungController : Controller
{
    private readonly DatPhongKhachSanContext _db;
    private readonly UserManager<DatPhongKhachSanUser> _userMgr;

    public NguoiDungController(DatPhongKhachSanContext db, UserManager<DatPhongKhachSanUser> userMgr)
    {
        _db      = db;
        _userMgr = userMgr;
    }

    // ── GET /Admin/NguoiDung ──────────────────────────────────────────────────
    public async Task<IActionResult> Index(string? loc = null)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Lấy tất cả NguoiDung có tài khoản (UserId != null)
        var nguoiDungs = await _db.NguoiDungs
            .Where(n => n.UserId != null)
            .OrderBy(n => n.VaiTro).ThenBy(n => n.HoTen)
            .ToListAsync();

        if (!string.IsNullOrEmpty(loc))
            nguoiDungs = nguoiDungs.Where(n => n.VaiTro == loc).ToList();

        var result = new List<NguoiDungAdminViewModel>();
        foreach (var nd in nguoiDungs)
        {
            var user = await _userMgr.FindByIdAsync(nd.UserId!);
            if (user == null) continue;

            var roles = (await _userMgr.GetRolesAsync(user)).ToList();
            var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

            result.Add(new NguoiDungAdminViewModel
            {
                MaNguoiDung   = nd.MaNguoiDung,
                UserId        = nd.UserId,
                HoTen         = nd.HoTen,
                Email         = nd.Email ?? user.Email,
                SoDienThoai   = nd.SoDienThoai,
                VaiTro        = nd.VaiTro,
                IsLocked      = isLocked,
                PhanLoai      = nd.PhanLoai,
                NgayTao       = nd.NgayTao,
                Roles         = roles,
                IsCurrentUser = nd.UserId == currentUserId
            });
        }

        ViewBag.Loc = loc;
        return View(result);
    }

    // ── POST /Admin/NguoiDung/KhoaTaiKhoan ───────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> KhoaTaiKhoan(string userId)
    {
        var user = await _userMgr.FindByIdAsync(userId);
        if (user == null) return NotFound();

        await _userMgr.SetLockoutEnabledAsync(user, true);
        await _userMgr.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        TempData["Success"] = $"Đã khóa tài khoản {user.Email}.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Admin/NguoiDung/MoTaiKhoan ─────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MoTaiKhoan(string userId)
    {
        var user = await _userMgr.FindByIdAsync(userId);
        if (user == null) return NotFound();

        await _userMgr.SetLockoutEndDateAsync(user, null);
        TempData["Success"] = $"Đã mở khóa tài khoản {user.Email}.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Admin/NguoiDung/DoiRole ─────────────────────────────────────────
    // Đổi role KhachHang ↔ NhanVien (không đổi Admin). Đồng bộ VaiTro trong NguoiDung.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DoiRole(string userId, string roleHienTai)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == currentUserId)
        {
            TempData["Error"] = "Không thể đổi role của chính mình.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userMgr.FindByIdAsync(userId);
        if (user == null) return NotFound();

        // Cho phép: KhachHang↔NhanVien và Admin=>NhanVien/KhachHang
        // KHÔNG cho phép: NhanVien/KhachHang => Admin (phải seed tay)
        if (roleHienTai is not ("KhachHang" or "NhanVien" or "Admin"))
        {
            TempData["Error"] = "Role không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        // Admin tối cao (có GanQuyen) không được downgrade
        var targetUser = await _userMgr.FindByIdAsync(userId);
        if (targetUser == null) return NotFound();
        var targetClaims = await _userMgr.GetClaimsAsync(targetUser);
        if (targetClaims.Any(c => c.Type == "ChucNang" && c.Value == "GanQuyen"))
        {
            TempData["Error"] = "Không thể đổi role của tài khoản admin tối cao.";
            return RedirectToAction(nameof(Index));
        }

        // Quyết định role mới
        string roleNew;
        if (roleHienTai == "Admin")
            roleNew = "NhanVien";               // Admin => NhanVien (hạ cấp)
        else if (roleHienTai == "KhachHang")
            roleNew = "NhanVien";               // Khách => Nhân viên
        else
            roleNew = "KhachHang";              // Nhân viên => Khách

        // Đổi Identity Role
        await _userMgr.RemoveFromRoleAsync(user, roleHienTai);
        await _userMgr.AddToRoleAsync(user, roleNew);

        // Đồng bộ NguoiDung.VaiTro
        var nd = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.UserId == userId);
        if (nd != null)
        {
            nd.VaiTro = roleNew;
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = $"Đã đổi role {user.Email}: {roleHienTai} => {roleNew}.";
        return RedirectToAction(nameof(Index));
    }
}
