using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("TienNghi")]
public partial class TienNghi
{
    [Key]
    public int MaTienNghi { get; set; }

    [StringLength(100)]
    public string TenTienNghi { get; set; } = null!;

    [StringLength(100)]
    public string? BieuTuong { get; set; }

    [StringLength(255)]
    public string? MoTa { get; set; }

    [ForeignKey("MaTienNghi")]
    [InverseProperty("MaTienNghis")]
    public virtual ICollection<LoaiPhong> MaLoaiPhongs { get; set; } = new List<LoaiPhong>();
}
