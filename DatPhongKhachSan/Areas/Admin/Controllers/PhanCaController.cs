using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Models.ViewModels;
using DatPhongKhachSan.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = ChucNang.XepCa)]
public class PhanCaController : Controller
{
    private readonly DatPhongKhachSanContext _db;

    public PhanCaController(DatPhongKhachSanContext db) => _db = db;

    // ── GET /Admin/PhanCa?tuNgay=yyyy-MM-dd ──────────────────────────────────
    public async Task<IActionResult> Index(string? tuNgay)
    {
        // Mặc định: Thứ Hai tuần hiện tại
        var today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly monday;
        if (!DateOnly.TryParse(tuNgay, out monday))
        {
            int dow = (int)today.DayOfWeek;
            monday = today.AddDays(dow == 0 ? -6 : 1 - dow);
        }

        var ngays   = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).ToList();
        var denNgay = monday.AddDays(6);

        var nhanViens = await _db.NguoiDungs
            .Where(n => n.VaiTro == "NhanVien" || n.VaiTro == "Admin")
            .OrderBy(n => n.HoTen)
            .ToListAsync();

        var phanCas = await _db.PhanCas
            .Include(p => p.MaCaNavigation)
            .Include(p => p.MaNguoiDungNavigation)
            .Where(p => p.NgayLam >= monday && p.NgayLam <= denNgay)
            .ToListAsync();

        var cacCa = await _db.CaLamViecs.OrderBy(c => c.GioBatDau).ToListAsync();

        ViewBag.CacCa     = cacCa;
        ViewBag.NhanViens = nhanViens;

        return View(new LichCaViewModel
        {
            TuNgay       = monday,
            DenNgay      = denNgay,
            Ngays        = ngays,
            NhanViens    = nhanViens,
            TatCaPhanCa  = phanCas,
            CacCa        = cacCa
        });
    }

    // ── POST /Admin/PhanCa/GanCa ──────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GanCa(int maNguoiDung, string ngayLam, int maCa,
                                            string? ghiChu, string? tuNgay)
    {
        if (!DateOnly.TryParse(ngayLam, out var ngay))
        {
            TempData["Error"] = "Ngày không hợp lệ.";
            return RedirectToAction(nameof(Index), new { tuNgay });
        }

        // Không cho gán ca ngày đã qua
        if (ngay < DateOnly.FromDateTime(DateTime.Today))
        {
            TempData["Error"] = $"Không thể gán ca cho ngày {ngay:dd/MM/yyyy} - ngày đã qua. Chỉ gán từ hôm nay ({DateOnly.FromDateTime(DateTime.Today):dd/MM/yyyy}) trở đi.";
            return RedirectToAction(nameof(Index), new { tuNgay });
        }

        // Kiểm tra trùng (PK tổ hợp sẽ báo lỗi nếu thêm trùng)
        bool tonTai = await _db.PhanCas.AnyAsync(p =>
            p.MaNguoiDung == maNguoiDung && p.NgayLam == ngay && p.MaCa == maCa);
        if (tonTai)
        {
            TempData["Error"] = "Nhân viên đã được gán ca này trong ngày đó.";
            return RedirectToAction(nameof(Index), new { tuNgay });
        }

        _db.PhanCas.Add(new PhanCa
        {
            MaNguoiDung = maNguoiDung,
            NgayLam     = ngay,
            MaCa        = maCa,
            GhiChu      = ghiChu
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = "Đã gán ca thành công.";
        return RedirectToAction(nameof(Index), new { tuNgay });
    }

    // ── POST /Admin/PhanCa/XoaPhanCa ─────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> XoaPhanCa(int maNguoiDung, string ngayLam, int maCa,
                                                string? tuNgay)
    {
        if (!DateOnly.TryParse(ngayLam, out var ngay)) return BadRequest();

        var pc = await _db.PhanCas.FindAsync(maNguoiDung, ngay, maCa);
        if (pc != null)
        {
            _db.PhanCas.Remove(pc);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã xóa phân ca.";
        }
        return RedirectToAction(nameof(Index), new { tuNgay });
    }

    // ── POST /Admin/PhanCa/TaoCa ──────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoCa(string tenCa, string gioBatDau, string gioKetThuc)
    {
        if (string.IsNullOrWhiteSpace(tenCa) ||
            !TimeOnly.TryParse(gioBatDau, out var tBD) ||
            !TimeOnly.TryParse(gioKetThuc, out var tKT))
        {
            TempData["Error"] = "Thông tin ca không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        _db.CaLamViecs.Add(new CaLamViec
        {
            TenCa      = tenCa.Trim(),
            GioBatDau  = tBD,
            GioKetThuc = tKT
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã thêm ca '{tenCa}'.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST /Admin/PhanCa/XoaCa ──────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> XoaCa(int maCa)
    {
        var ca = await _db.CaLamViecs.FindAsync(maCa);
        if (ca == null) return NotFound();

        bool coLichSu = await _db.PhanCas.AnyAsync(p => p.MaCa == maCa);
        if (coLichSu)
        {
            TempData["Error"] = "Ca đã được phân công. Không thể xóa.";
            return RedirectToAction(nameof(Index));
        }

        _db.CaLamViecs.Remove(ca);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã xóa ca.";
        return RedirectToAction(nameof(Index));
    }
}
