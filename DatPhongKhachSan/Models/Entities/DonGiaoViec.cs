using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DatPhongKhachSan.Models.Entities;

[Table("DonGiaoViec")]
public partial class DonGiaoViec
{
    [Key]
    public int MaDonViec { get; set; }

    public int MaPhong { get; set; }        // FK => Phong - đơn gắn với 1 phòng cụ thể

    public string NoiDung { get; set; } = null!;    // tự do: ai làm + việc gì

    [StringLength(20)]
    public string TrangThai { get; set; } = null!;  // "ChoLam" / "HoanThanh"

    public DateTime NgayGiao { get; set; }

    public DateTime? NgayHoanThanh { get; set; }

    public string? GhiChuNghiemThu { get; set; }   // lễ tân ghi khi nghiệm thu

    [ForeignKey("MaPhong")]
    [InverseProperty("DonGiaoViecs")]
    public virtual Phong MaPhongNavigation { get; set; } = null!;
}
