using System.ComponentModel.DataAnnotations;
using DatPhongKhachSan.Models.Entities;

namespace DatPhongKhachSan.Models.ViewModels;

public class DatPhongViewModel
{
    // ── Từ URL params ─────────────────────────────────────────────────────────
    public int MaLoaiPhong { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime NgayNhanPhong { get; set; } = DateTime.Today.AddDays(1);

    [Required]
    [DataType(DataType.Date)]
    public DateTime NgayTraPhong { get; set; } = DateTime.Today.AddDays(2);

    [Range(1, 10)]
    public int SoNguoiLon { get; set; } = 1;

    [Range(0, 5)]
    public int SoTreEm { get; set; } = 0;

    // ── Khách điền ────────────────────────────────────────────────────────────

    // Cho khách CHƯA đăng nhập ("đặt nhanh") - validation thực hiện trong controller
    [StringLength(150)]
    public string? HoTen { get; set; }

    [StringLength(20)]
    public string? SoDienThoai { get; set; }

    // Cho khách đã đăng nhập - không [Required] vì anonymous không điền cái này
    [StringLength(150)]
    public string TenKhachLuuTru { get; set; } = string.Empty;

    public string LoaiThanhToan { get; set; } = "ThanhToanDu";

    [StringLength(500)]
    public string? GhiChu { get; set; }

    public List<DichVuChonItem> DichVuChon { get; set; } = new();

    // ── Hiển thị (load từ DB) ─────────────────────────────────────────────────
    public LoaiPhong? LoaiPhong { get; set; }
    public List<DichVu> DichVuKhaDung { get; set; } = new();
    public int SoPhongConTrong { get; set; }
}

public class DichVuChonItem
{
    public int MaDichVu { get; set; }
    public string TenDichVu { get; set; } = string.Empty;
    public decimal Gia { get; set; }
    public string? DonVi { get; set; }

    [Range(0, 99)]
    public int SoLuong { get; set; } = 0;
}
