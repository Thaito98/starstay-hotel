using System.Security.Claims;
using DatPhongKhachSan.Areas.Identity.Data;
using DatPhongKhachSan.Constants;
using DatPhongKhachSan.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = ChucNang.GanQuyen)]
public class PhanQuyenController : Controller
{
    private readonly DatPhongKhachSanContext _db;
    private readonly UserManager<DatPhongKhachSanUser> _userMgr;

    public PhanQuyenController(DatPhongKhachSanContext db, UserManager<DatPhongKhachSanUser> userMgr)
    {
        _db      = db;
        _userMgr = userMgr;
    }

    // ── GET /Admin/PhanQuyen?userId=... ───────────────────────────────────────
    public async Task<IActionResult> Index(string? userId)
    {
        var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Chỉ hiện NhanVien, KHÔNG hiện Admin (khoá cứng) và KHÔNG hiện chính mình
        var nhanViens = await _db.NguoiDungs
            .Where(n => n.UserId != null
                     && n.VaiTro == "NhanVien"
                     && n.UserId != myId)
            .OrderBy(n => n.HoTen)
            .ToListAsync();

        ViewBag.DanhSachNV = nhanViens;
        ViewBag.UserId     = userId;

        if (string.IsNullOrEmpty(userId)) return View((List<string>?)null);

        // Bảo vệ server: không cho chọn Admin hoặc chính mình qua URL thủ công
        if (userId == myId) { TempData["Error"] = "Không thể sửa quyền của chính mình."; return RedirectToAction(nameof(Index)); }

        var targetNd = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.UserId == userId);
        if (targetNd?.VaiTro == "Admin") { TempData["Error"] = "Không thể sửa quyền của quản trị viên."; return RedirectToAction(nameof(Index)); }

        var user = await _userMgr.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var claims      = await _userMgr.GetClaimsAsync(user);
        var currentCNs  = claims.Where(c => c.Type == ChucNang.ClaimType)
                                .Select(c => c.Value)
                                .ToList();
        ViewBag.UserHoTen = targetNd?.HoTen ?? user.Email;
        ViewBag.UserEmail = user.Email;

        return View(currentCNs);
    }

    // ── POST /Admin/PhanQuyen/LuuQuyen ────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LuuQuyen(string userId, List<string>? chucNangChon)
    {
        var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Double-check server: không cho sửa chính mình hoặc Admin
        if (userId == myId) return Forbid();

        var targetNd = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.UserId == userId);
        if (targetNd?.VaiTro == "Admin") return Forbid();

        var user = await _userMgr.FindByIdAsync(userId);
        if (user == null) return NotFound();

        chucNangChon ??= new List<string>();

        // Chỉ chấp nhận các claim hợp lệ (chặn inject claim giả)
        chucNangChon = chucNangChon.Where(cn => ChucNang.TatCa.Contains(cn)).ToList();

        var existing = (await _userMgr.GetClaimsAsync(user))
            .Where(c => c.Type == ChucNang.ClaimType).ToList();

        foreach (var c in existing.Where(c => !chucNangChon.Contains(c.Value)))
            await _userMgr.RemoveClaimAsync(user, c);

        var existingValues = existing.Select(c => c.Value).ToList();
        foreach (var cn in chucNangChon.Where(cn => !existingValues.Contains(cn)))
            await _userMgr.AddClaimAsync(user, new Claim(ChucNang.ClaimType, cn));

        TempData["Success"] = $"Đã cập nhật quyền cho {user.Email}.";
        return RedirectToAction(nameof(Index), new { userId });
    }
}
