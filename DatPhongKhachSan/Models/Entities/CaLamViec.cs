using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("CaLamViec")]
public partial class CaLamViec
{
    [Key]
    public int MaCa { get; set; }

    [StringLength(50)]
    public string TenCa { get; set; } = null!;

    public TimeOnly GioBatDau { get; set; }

    public TimeOnly GioKetThuc { get; set; }

    [InverseProperty("MaCaNavigation")]
    public virtual ICollection<PhanCa> PhanCas { get; set; } = new List<PhanCa>();
}
