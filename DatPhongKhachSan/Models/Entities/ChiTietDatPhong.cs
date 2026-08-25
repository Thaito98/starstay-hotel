using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("ChiTietDatPhong")]
public partial class ChiTietDatPhong
{
    [Key]
    public int MaChiTiet { get; set; }

    public int MaDatPhong { get; set; }

    public int MaPhong { get; set; }

    public int SoNguoiLon { get; set; }

    public int SoTreEm { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal GiaMotDem { get; set; }

    [StringLength(150)]
    public string? TenKhachLuuTru { get; set; }

    [InverseProperty("MaChiTietNavigation")]
    public virtual ICollection<ChiTietDichVu> ChiTietDichVus { get; set; } = new List<ChiTietDichVu>();

    [ForeignKey("MaDatPhong")]
    [InverseProperty("ChiTietDatPhongs")]
    public virtual DatPhong MaDatPhongNavigation { get; set; } = null!;

    [ForeignKey("MaPhong")]
    [InverseProperty("ChiTietDatPhongs")]
    public virtual Phong MaPhongNavigation { get; set; } = null!;
}
