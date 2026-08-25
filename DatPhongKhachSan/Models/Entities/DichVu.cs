using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("DichVu")]
public partial class DichVu
{
    [Key]
    public int MaDichVu { get; set; }

    [StringLength(100)]
    public string TenDichVu { get; set; } = null!;

    public string? MoTa { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Gia { get; set; }

    [StringLength(50)]
    public string? DonVi { get; set; }

    [StringLength(500)]
    public string? DuongDanAnh { get; set; }

    public bool TrangThai { get; set; }

    [InverseProperty("MaDichVuNavigation")]
    public virtual ICollection<ChiTietDichVu> ChiTietDichVus { get; set; } = new List<ChiTietDichVu>();
}
