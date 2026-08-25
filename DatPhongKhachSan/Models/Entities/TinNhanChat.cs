using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("TinNhanChat")]
public partial class TinNhanChat
{
    [Key]
    public int MaTinNhan { get; set; }

    public Guid MaPhienChat { get; set; }

    [StringLength(450)]
    public string? UserId { get; set; }

    [StringLength(10)]
    public string VaiTro { get; set; } = null!;

    public string NoiDung { get; set; } = null!;

    [StringLength(20)]
    public string? LoaiCauTraLoi { get; set; }

    public DateTime NgayTao { get; set; }
}
