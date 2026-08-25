using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("ThanhToan")]
public partial class ThanhToan
{
    [Key]
    public int MaThanhToan { get; set; }

    public int MaDatPhong { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal SoTien { get; set; }

    [StringLength(30)]
    public string PhuongThuc { get; set; } = null!;

    [StringLength(20)]
    public string LoaiThanhToan { get; set; } = null!;

    [StringLength(100)]
    public string? MaGiaoDich { get; set; }

    [StringLength(20)]
    public string TrangThai { get; set; } = null!;

    public DateTime? NgayThanhToan { get; set; }

    [StringLength(255)]
    public string? GhiChu { get; set; }

    public DateTime NgayTao { get; set; }

    [ForeignKey("MaDatPhong")]
    [InverseProperty("ThanhToans")]
    public virtual DatPhong MaDatPhongNavigation { get; set; } = null!;
}
