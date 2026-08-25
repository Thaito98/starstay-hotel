using System.ComponentModel.DataAnnotations;
using DatPhongKhachSan.Models.Entities;

namespace DatPhongKhachSan.Models.ViewModels;

public class LoaiPhongFormViewModel
{
    public int MaLoaiPhong { get; set; }

    [Required(ErrorMessage = "Nhập tên loại phòng")]
    [StringLength(100)]
    public string TenLoaiPhong { get; set; } = string.Empty;

    public string? MoTa { get; set; }

    [Required(ErrorMessage = "Nhập giá cơ bản")]
    [Range(0, double.MaxValue, ErrorMessage = "Giá phải >= 0")]
    public decimal GiaCoBan { get; set; }

    [Range(1, 20)] public int SucChuaNguoiLon { get; set; } = 2;
    [Range(0, 10)] public int SucChuaTreEm   { get; set; } = 1;

    [StringLength(50)] public string? LoaiGiuong { get; set; }

    [Range(0, 9999)] public decimal? DienTich { get; set; }

    public bool TrangThai { get; set; } = true;

    // TienNghi multi-select
    public List<int> MaTienNghiChon { get; set; } = new();
    public List<TienNghi> TatCaTienNghi { get; set; } = new();

    // Ảnh hiện có
    public List<AnhPhong> AnhPhongs { get; set; } = new();

    // Upload ảnh mới
    public List<IFormFile>? FileAnhs { get; set; }
}

public class PhongFormViewModel
{
    public int MaPhong { get; set; }

    [Required(ErrorMessage = "Nhập số phòng")]
    [StringLength(10)]
    public string SoPhong { get; set; } = string.Empty;

    public int? Tang { get; set; }

    [Required(ErrorMessage = "Chọn loại phòng")]
    public int MaLoaiPhong { get; set; }

    public string TrangThai { get; set; } = "Trong";  // Trong/DangDonDep (DangO chỉ tự động)

    [StringLength(255)] public string? GhiChu { get; set; }

    public List<LoaiPhong> DanhSachLoaiPhong { get; set; } = new();
}
