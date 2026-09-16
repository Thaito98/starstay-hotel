using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("ChiTietDichVu")]
public partial class ChiTietDichVu
{
    [Key]
    public int MaChiTietDV { get; set; }

    public int MaChiTiet { get; set; }

    public int MaDichVu { get; set; }

    public int SoLuong { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal DonGia { get; set; }

    [Column(TypeName = "decimal(29, 2)")]
    public decimal? ThanhTien { get; set; }

    [ForeignKey("MaChiTiet")]
    [InverseProperty("ChiTietDichVus")]
    public virtual ChiTietDatPhong MaChiTietNavigation { get; set; } = null!;

    [ForeignKey("MaDichVu")]
    [InverseProperty("ChiTietDichVus")]
    public virtual DichVu MaDichVuNavigation { get; set; } = null!;
}
