using ClosedXML.Excel;
using DatPhongKhachSan.Constants;
using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = ChucNang.XemBaoCao)]
public class BaoCaoController : Controller
{
    private readonly DatPhongKhachSanContext _db;

    public BaoCaoController(DatPhongKhachSanContext db) => _db = db;

    // ── GET /Admin/BaoCao?thang=6&nam=2025 ───────────────────────────────────
    public async Task<IActionResult> Index(int? thang, int? nam)
    {
        var t = thang ?? DateTime.Today.Month;
        var n = nam   ?? DateTime.Today.Year;
        return View(await TinhBaoCao(t, n));
    }

    // ── GET /Admin/BaoCao/ExportExcel ─────────────────────────────────────────
    public async Task<IActionResult> ExportExcel(int? thang, int? nam)
    {
        var t  = thang ?? DateTime.Today.Month;
        var n  = nam   ?? DateTime.Today.Year;
        var vm = await TinhBaoCao(t, n);

        using var wb = new XLWorkbook();

        // Sheet 1: Chi tiết ngày
        var ws1 = wb.Worksheets.Add($"Doanh thu T{t}-{n}");
        ws1.Cell(1, 1).Value = $"BÁO CÁO DOANH THU THÁNG {t}/{n}";
        ws1.Range("A1:F1").Merge().Style.Font.Bold = true;
        ws1.Range("A1:F1").Style.Font.FontSize = 14;

        int row = 3;
        string[] headers = ["Ngày", "Doanh thu (₫)", "Hoàn tiền (₫)", "Thực thu (₫)", "Phòng có khách", "Lấp đầy (%)"];
        for (int c = 0; c < headers.Length; c++)
            ws1.Cell(row, c + 1).Value = headers[c];
        ws1.Row(row).Style.Font.Bold = true;
        ws1.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
        ws1.Row(row).Style.Font.FontColor = XLColor.White;
        row++;

        foreach (var item in vm.ChiTietNgay)
        {
            ws1.Cell(row, 1).Value = item.Ngay.ToString("dd/MM/yyyy");
            ws1.Cell(row, 2).Value = (double)item.DoanhThu;
            ws1.Cell(row, 3).Value = (double)item.HoanTien;
            ws1.Cell(row, 4).Value = (double)(item.DoanhThu - item.HoanTien);
            ws1.Cell(row, 5).Value = item.PhongBan;
            ws1.Cell(row, 6).Value = item.TiLeCapDay;
            row++;
        }

        // Tổng
        ws1.Cell(row, 1).Value = "TỔNG / TRUNG BÌNH";
        ws1.Cell(row, 2).Value = (double)vm.TongDoanhThu;
        ws1.Cell(row, 3).Value = (double)vm.TongHoanTien;
        ws1.Cell(row, 4).Value = (double)vm.DoanhThuThuan;
        ws1.Cell(row, 6).Value = vm.TiLeCapDayTrungBinh;
        ws1.Row(row).Style.Font.Bold = true;
        ws1.Row(row).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws1.Columns(2, 4).Style.NumberFormat.Format = "#,##0";
        ws1.Columns().AdjustToContents();

        // Sheet 2: Tóm tắt
        var ws2 = wb.Worksheets.Add("Tóm tắt");
        var summary = new (string k, object v)[]
        {
            ("Tháng/Năm",              $"{t}/{n}"),
            ("Tổng doanh thu (₫)",     (double)vm.TongDoanhThu),
            ("Tổng hoàn tiền (₫)",     (double)vm.TongHoanTien),
            ("Thực thu (₫)",           (double)vm.DoanhThuThuan),
            ("Lấp đầy TB (%)",         vm.TiLeCapDayTrungBinh),
            ("Đơn đã xác nhận",        vm.SoDonDaXacNhan),
            ("Đơn đang ở",             vm.SoDonDangO),
            ("Đơn đã trả phòng",       vm.SoDonDaTraPhong),
            ("Đơn hủy/hoàn tiền",      vm.SoDonDaHuy),
            ("Tổng số phòng",          vm.TongSoPhong),
        };
        ws2.Cell(1, 1).Value = "Chỉ số"; ws2.Cell(1, 2).Value = "Giá trị";
        ws2.Row(1).Style.Font.Bold = true;
        for (int i = 0; i < summary.Length; i++)
        {
            ws2.Cell(i + 2, 1).Value = summary[i].k;
            ws2.Cell(i + 2, 2).Value = summary[i].v?.ToString();
        }
        ws2.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"BaoCao_T{t}_{n}.xlsx");
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    private async Task<BaoCaoViewModel> TinhBaoCao(int thang, int nam)
    {
        var vm = new BaoCaoViewModel { Thang = thang, Nam = nam };

        // Thanh toán trong tháng
        var thanhToans = await _db.ThanhToans
            .Where(t => t.TrangThai == "ThanhCong"
                     && t.NgayThanhToan.HasValue
                     && t.NgayThanhToan.Value.Month == thang
                     && t.NgayThanhToan.Value.Year  == nam)
            .ToListAsync();

        vm.TongDoanhThu = thanhToans.Where(t => t.LoaiThanhToan != "HoanTien").Sum(t => t.SoTien);
        vm.TongHoanTien = thanhToans.Where(t => t.LoaiThanhToan == "HoanTien").Sum(t => t.SoTien);

        var doanhByDay = thanhToans.Where(t => t.LoaiThanhToan != "HoanTien")
            .GroupBy(t => DateOnly.FromDateTime(t.NgayThanhToan!.Value))
            .ToDictionary(g => g.Key, g => g.Sum(t => t.SoTien));
        var hoanByDay = thanhToans.Where(t => t.LoaiThanhToan == "HoanTien")
            .GroupBy(t => DateOnly.FromDateTime(t.NgayThanhToan!.Value))
            .ToDictionary(g => g.Key, g => g.Sum(t => t.SoTien));

        // Thống kê đơn tạo trong tháng
        var donQuery = _db.DatPhongs
            .Where(d => d.NgayDat.Month == thang && d.NgayDat.Year == nam);
        vm.SoDonDaXacNhan  = await donQuery.CountAsync(d => d.TrangThai == "DaXacNhan");
        vm.SoDonDangO      = await donQuery.CountAsync(d => d.TrangThai == "DangO");
        vm.SoDonDaTraPhong = await donQuery.CountAsync(d => d.TrangThai == "DaTraPhong");
        vm.SoDonDaHuy      = await donQuery.CountAsync(d => d.TrangThai == "DaHuy" || d.TrangThai == "ChoHoanTien");

        // Lấp đầy từng ngày
        vm.TongSoPhong = await _db.Phongs.CountAsync();
        int soNgay = DateTime.DaysInMonth(nam, thang);
        double tongTiLe = 0;

        for (int day = 1; day <= soNgay; day++)
        {
            var ngay = new DateOnly(nam, thang, day);

            int phongBan = await _db.ChiTietDatPhongs
                .Where(ct => ct.MaDatPhongNavigation.TrangThai != "DaHuy"
                          && ct.MaDatPhongNavigation.NgayNhanPhong <= ngay
                          && ct.MaDatPhongNavigation.NgayTraPhong  >  ngay)
                .CountAsync();

            double tiLe = vm.TongSoPhong > 0 ? Math.Round((double)phongBan / vm.TongSoPhong * 100, 1) : 0;
            tongTiLe += tiLe;

            vm.ChiTietNgay.Add(new BaoCaoNgayItem
            {
                Ngay       = ngay,
                DoanhThu   = doanhByDay.GetValueOrDefault(ngay, 0),
                HoanTien   = hoanByDay.GetValueOrDefault(ngay, 0),
                PhongBan   = phongBan,
                TiLeCapDay = tiLe
            });
        }

        vm.TiLeCapDayTrungBinh = soNgay > 0 ? Math.Round(tongTiLe / soNgay, 1) : 0;
        return vm;
    }
}
