using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("Phong")]
[Index("SoPhong", Name = "UQ__Phong__7C736CA17C08213B", IsUnique = true)]
public partial class Phong
{
    [Key]
    public int MaPhong { get; set; }

    [StringLength(10)]
    public string SoPhong { get; set; } = null!;

    public int? Tang { get; set; }

    public int MaLoaiPhong { get; set; }

    // 3 trạng thái: "Trong" / "DangO" / "DangDonDep" (bảo trì gộp vào DangDonDep)
    [StringLength(20)]
    public string TrangThai { get; set; } = null!;

    [StringLength(255)]
    public string? GhiChu { get; set; }

    public DateTime NgayTao { get; set; }

    [ForeignKey("MaLoaiPhong")]
    [InverseProperty("Phongs")]
    public virtual LoaiPhong MaLoaiPhongNavigation { get; set; } = null!;

    [InverseProperty("MaPhongNavigation")]
    public virtual ICollection<ChiTietDatPhong> ChiTietDatPhongs { get; set; } = new List<ChiTietDatPhong>();

    [InverseProperty("MaPhongNavigation")]
    public virtual ICollection<DonGiaoViec> DonGiaoViecs { get; set; } = new List<DonGiaoViec>();
}
