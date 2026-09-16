using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,NhanVien")]
public class HomeController : Controller
{
    private readonly DatPhongKhachSanContext _db;

    public HomeController(DatPhongKhachSanContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var today      = DateOnly.FromDateTime(DateTime.Today);
        var todayStart = DateTime.Today;
        var todayEnd   = DateTime.Today.AddDays(1);

        var vm = new AdminDashboardVm
        {
            CheckInHomNay = await _db.DatPhongs.CountAsync(d =>
                d.NgayNhanPhong == today &&
                (d.TrangThai == "DaXacNhan" || d.TrangThai == "DangO")),

            CheckOutHomNay = await _db.DatPhongs.CountAsync(d =>
                d.NgayTraPhong == today &&
                (d.TrangThai == "DangO" || d.TrangThai == "DaTraPhong")),

            PhongTrong = await _db.Phongs.CountAsync(p => p.TrangThai == "Trong"),

            DonChoXacNhan = await _db.DatPhongs.CountAsync(d => d.TrangThai == "ChoXacNhan"),

            DoanhThuHomNay = await _db.ThanhToans
                .Where(t => t.TrangThai == "ThanhCong"
                    && t.LoaiThanhToan != "HoanTien"
                    && t.NgayThanhToan >= todayStart
                    && t.NgayThanhToan < todayEnd)
                .SumAsync(t => (decimal?)t.SoTien) ?? 0m
        };

        // Biểu đồ: doanh thu 7 ngày gần nhất
        var chartLabels = new List<string>();
        var chartData   = new List<decimal>();
        for (int i = 6; i >= 0; i--)
        {
            var d0 = DateTime.Today.AddDays(-i);
            var d1 = d0.AddDays(1);
            var dt = await _db.ThanhToans
                .Where(t => t.TrangThai == "ThanhCong"
                    && t.LoaiThanhToan != "HoanTien"
                    && t.NgayThanhToan >= d0
                    && t.NgayThanhToan < d1)
                .SumAsync(t => (decimal?)t.SoTien) ?? 0m;
            chartLabels.Add(d0.ToString("dd/MM"));
            chartData.Add(dt);
        }

        ViewBag.ChartLabels = System.Text.Json.JsonSerializer.Serialize(chartLabels);
        ViewBag.ChartData   = System.Text.Json.JsonSerializer.Serialize(chartData);

        return View(vm);
    }
}
