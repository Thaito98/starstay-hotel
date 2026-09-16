using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DatPhongKhachSan.Models.Entities;

[Table("DoanVanBan")]
public partial class DoanVanBan
{
    [Key]
    public int MaDoan { get; set; }

    public string CauHoi { get; set; } = null!;

    public string TraLoi { get; set; } = null!;

    [StringLength(100)]
    public string? ChuDe { get; set; }

    public byte[]? Embedding { get; set; }

    [StringLength(100)]
    public string? ModelEmbedding { get; set; }

    public DateTime NgayIndex { get; set; }
}
