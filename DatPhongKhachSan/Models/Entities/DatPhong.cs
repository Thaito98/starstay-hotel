using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("DatPhong")]
[Index("MaDon", Name = "UQ__DatPhong__3D89F569D8890C6C", IsUnique = true)]
public partial class DatPhong
{
    [Key]
    public int MaDatPhong { get; set; }

    public int MaNguoiDung { get; set; }    // FK => NguoiDung (người lưu trú)

    [StringLength(20)]
    public string MaDon { get; set; } = null!;

    [StringLength(10)]
    public string NguonDat { get; set; } = null!;   // "Online" / "WalkIn"

    public DateOnly NgayNhanPhong { get; set; }

    public DateOnly NgayTraPhong { get; set; }

    public int? SoDem { get; set; }

    public int TongSoNguoiLon { get; set; }

    public int TongSoTreEm { get; set; }

    [StringLength(20)]
    public string LoaiThanhToan { get; set; } = null!;

    [StringLength(20)]
    public string TrangThai { get; set; } = null!;

    public string? GhiChu { get; set; }

    public DateTime NgayDat { get; set; }

    public DateTime? NgayCapNhat { get; set; }

    public DateTime? NgayHuy { get; set; }

    // Navigation
    [ForeignKey("MaNguoiDung")]
    [InverseProperty("DatPhongs")]
    public virtual NguoiDung MaNguoiDungNavigation { get; set; } = null!;

    [InverseProperty("MaDatPhongNavigation")]
    public virtual ICollection<ChiTietDatPhong> ChiTietDatPhongs { get; set; } = new List<ChiTietDatPhong>();

    [InverseProperty("MaDatPhongNavigation")]
    public virtual ICollection<ThanhToan> ThanhToans { get; set; } = new List<ThanhToan>();
}
