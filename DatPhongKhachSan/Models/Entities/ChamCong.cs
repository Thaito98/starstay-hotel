using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("ChamCong")]
[Index("MaNguoiDung", "NgayCong", Name = "UQ_ChamCong", IsUnique = true)]
public partial class ChamCong
{
    [Key]
    public int MaChamCong { get; set; }

    public int MaNguoiDung { get; set; }    // FK => NguoiDung

    public DateOnly NgayCong { get; set; }

    public TimeOnly? GioVao { get; set; }

    public TimeOnly? GioRa { get; set; }

    [Column(TypeName = "decimal(4, 2)")]
    public decimal SoCong { get; set; }

    public int SoPhutTre { get; set; }      // phút đi trễ (0 nếu đúng giờ); Trễ vẫn tính 1.0 công

    [StringLength(20)]
    public string TrangThai { get; set; } = null!;  // "DiLam" / "Tre" / "Vang"

    [StringLength(255)]
    public string? GhiChu { get; set; }

    [ForeignKey("MaNguoiDung")]
    [InverseProperty("ChamCongs")]
    public virtual NguoiDung MaNguoiDungNavigation { get; set; } = null!;
}
