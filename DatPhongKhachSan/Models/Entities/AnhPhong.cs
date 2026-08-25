using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("AnhPhong")]
public partial class AnhPhong
{
    [Key]
    public int MaAnh { get; set; }

    public int MaLoaiPhong { get; set; }

    [StringLength(500)]
    public string DuongDanAnh { get; set; } = null!;

    public bool LaAnhChinh { get; set; }

    public int ThuTu { get; set; }

    [ForeignKey("MaLoaiPhong")]
    [InverseProperty("AnhPhongs")]
    public virtual LoaiPhong MaLoaiPhongNavigation { get; set; } = null!;
}
