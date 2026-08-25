using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[PrimaryKey("MaNguoiDung", "NgayLam", "MaCa")]
[Table("PhanCa")]
public partial class PhanCa
{
    [Key]
    public int MaNguoiDung { get; set; }    // FK => NguoiDung

    [Key]
    public DateOnly NgayLam { get; set; }

    [Key]
    public int MaCa { get; set; }

    [StringLength(255)]
    public string? GhiChu { get; set; }

    [ForeignKey("MaNguoiDung")]
    [InverseProperty("PhanCas")]
    public virtual NguoiDung MaNguoiDungNavigation { get; set; } = null!;

    [ForeignKey("MaCa")]
    [InverseProperty("PhanCas")]
    public virtual CaLamViec MaCaNavigation { get; set; } = null!;
}
