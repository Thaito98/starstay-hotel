using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("LoaiPhong")]
public partial class LoaiPhong
{
    [Key]
    public int MaLoaiPhong { get; set; }

    [StringLength(100)]
    public string TenLoaiPhong { get; set; } = null!;

    public string? MoTa { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal GiaCoBan { get; set; }

    public int SucChuaNguoiLon { get; set; }

    public int SucChuaTreEm { get; set; }

    [StringLength(50)]
    public string? LoaiGiuong { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal? DienTich { get; set; }

    public bool TrangThai { get; set; }

    public DateTime NgayTao { get; set; }

    [InverseProperty("MaLoaiPhongNavigation")]
    public virtual ICollection<AnhPhong> AnhPhongs { get; set; } = new List<AnhPhong>();

    [InverseProperty("MaLoaiPhongNavigation")]
    public virtual ICollection<Phong> Phongs { get; set; } = new List<Phong>();

    [ForeignKey("MaLoaiPhong")]
    [InverseProperty("MaLoaiPhongs")]
    public virtual ICollection<TienNghi> MaTienNghis { get; set; } = new List<TienNghi>();
}
