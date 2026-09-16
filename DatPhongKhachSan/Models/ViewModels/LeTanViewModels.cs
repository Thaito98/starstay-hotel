using System.ComponentModel.DataAnnotations;
using DatPhongKhachSan.Models.Entities;

namespace DatPhongKhachSan.Models.ViewModels;

// ── Tra cứu đơn ──────────────────────────────────────────────────────────────
public class TraCuuDonViewModel
{
    public string? TuKhoa { get; set; }
    public string? LocTrangThai { get; set; }
    public List<DonTomTatViewModel> KetQua { get; set; } = new();
    public bool DaTimKiem { get; set; }
}

public class DonTomTatViewModel
{
    public int MaDatPhong { get; set; }
    public string MaDon { get; set; } = string.Empty;
    public string TenKhach { get; set; } = string.Empty;
    public string? SoDienThoai { get; set; }
    public string TenLoaiPhong { get; set; } = string.Empty;
    public string SoPhong { get; set; } = string.Empty;
    public DateOnly NgayNhanPhong { get; set; }
    public DateOnly NgayTraPhong { get; set; }
    public int SoDem { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public string NguonDat { get; set; } = string.Empty;
    public decimal TongTien { get; set; }
    public decimal ConLai { get; set; }
}

// ── Chi tiết đơn + thao tác ───────────────────────────────────────────────────
public class ChiTietDonViewModel
{
    public DatPhong Don { get; set; } = null!;
    public NguoiDung KhachHang { get; set; } = null!;
    public List<ChiTietDatPhong> ChiTiets { get; set; } = new();
    public decimal TongTien { get; set; }
    public decimal DaTra { get; set; }
    public decimal ConLai { get; set; }
    public int SoDem { get; set; }
}

// ── Thu tiền khi check-in ─────────────────────────────────────────────────────
public class ThuTienCheckInViewModel
{
    public int MaDatPhong { get; set; }
    public string MaDon { get; set; } = string.Empty;
    public decimal SoTienConLai { get; set; }

    [Required]
    [Range(1, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0")]
    public decimal SoTienThu { get; set; }

    public string PhuongThuc { get; set; } = "TienMat";
    public string? GhiChu { get; set; }
}

// ── Đặt phòng Walk-in ─────────────────────────────────────────────────────────
public class DatWalkInViewModel
{
    // Thông tin khách
    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [StringLength(150)]
    public string HoTen { get; set; } = string.Empty;

    [StringLength(20)]
    public string? SoDienThoai { get; set; }

    [StringLength(20)]
    public string? CCCD { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    // Thông tin đặt phòng
    [Required]
    [DataType(DataType.Date)]
    public DateTime NgayNhanPhong { get; set; } = DateTime.Today;

    [Required]
    [DataType(DataType.Date)]
    public DateTime NgayTraPhong { get; set; } = DateTime.Today.AddDays(1);

    [Range(1, 10)]
    public int SoNguoiLon { get; set; } = 1;

    [Range(0, 5)]
    public int SoTreEm { get; set; } = 0;

    [Required(ErrorMessage = "Vui lòng chọn loại phòng")]
    public int MaLoaiPhong { get; set; }

    public string? GhiChu { get; set; }

    // Thanh toán tại quầy
    [Range(0, double.MaxValue)]
    public decimal SoTienThu { get; set; }

    public string PhuongThuc { get; set; } = "TienMat";

    public bool CheckInNgay { get; set; } = true;

    // Chọn phòng cụ thể
    [Required(ErrorMessage = "Vui lòng chọn số phòng")]
    public int MaPhong { get; set; }

    // Dịch vụ thêm
    public List<DichVuChonItem> DichVuChon { get; set; } = new();

    // Display
    public List<LoaiPhong> DanhSachLoaiPhong { get; set; } = new();
    public List<Phong> DanhSachPhong { get; set; } = new();   // tất cả phòng không BaoTri
}

// ── Chuyển phòng ──────────────────────────────────────────────────────────────
public class ChuyenPhongViewModel
{
    public int MaDatPhong { get; set; }
    public string MaDon { get; set; } = string.Empty;
    public string PhongHien { get; set; } = string.Empty;
    public string LoaiPhongHien { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn phòng mới")]
    public int MaPhongMoi { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do chuyển phòng")]
    [StringLength(500)]
    public string LyDo { get; set; } = string.Empty;

    public List<Phong> DanhSachPhongTrong { get; set; } = new();
}

// ── Gia hạn lưu trú ──────────────────────────────────────────────────────────
public class GiaHanViewModel
{
    public int MaDatPhong { get; set; }
    public string MaDon { get; set; } = string.Empty;
    public string TenKhach { get; set; } = string.Empty;
    public DateOnly NgayNhanPhong { get; set; }
    public DateOnly NgayTraPhongHien { get; set; }
    public decimal TongTienHien { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày trả phòng mới")]
    [DataType(DataType.Date)]
    public DateTime NgayTraPhongMoi { get; set; }

    public decimal TongTienMoi { get; set; }
    public bool KhaDung { get; set; } = true;
}
