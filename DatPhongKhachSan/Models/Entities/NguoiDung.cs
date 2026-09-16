using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("NguoiDung")]
public partial class NguoiDung
{
    [Key]
    public int MaNguoiDung { get; set; }

    public string? UserId { get; set; }         // AspNetUsers.Id - NULL nếu walk-in/đặt nhanh

    [StringLength(150)]
    public string HoTen { get; set; } = null!;

    public DateOnly? NgaySinh { get; set; }

    [StringLength(10)]
    public string? GioiTinh { get; set; }

    [StringLength(20)]
    public string? SoDienThoai { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? CCCD { get; set; }

    [StringLength(255)]
    public string? DiaChi { get; set; }

    [StringLength(500)]
    public string? DuongDanAnh { get; set; }

    [StringLength(20)]
    public string VaiTro { get; set; } = "KhachHang";  // "KhachHang"/"NhanVien"/"Admin"

    [StringLength(20)]
    public string PhanLoai { get; set; } = "Thuong";   // "Thuong"/"VIP"/"Corporate"

    public DateTime NgayTao { get; set; }

    // Navigation
    [InverseProperty("MaNguoiDungNavigation")]
    public virtual ICollection<DatPhong> DatPhongs { get; set; } = new List<DatPhong>();

    [InverseProperty("MaNguoiDungNavigation")]
    public virtual ICollection<PhanCa> PhanCas { get; set; } = new List<PhanCa>();

    [InverseProperty("MaNguoiDungNavigation")]
    public virtual ICollection<ChamCong> ChamCongs { get; set; } = new List<ChamCong>();
}
