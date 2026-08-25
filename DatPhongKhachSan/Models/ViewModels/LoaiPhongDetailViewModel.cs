using DatPhongKhachSan.Models.Entities;

namespace DatPhongKhachSan.Models.ViewModels;

public class LoaiPhongDetailViewModel
{
    public LoaiPhong LoaiPhong { get; set; } = null!;
    public List<AnhPhong> Anhs { get; set; } = new();
    public List<TienNghi> TienNghis { get; set; } = new();
    public int SoPhongHienCo { get; set; }

    // Prefill từ form tìm kiếm
    public DateTime? NgayNhanPhong { get; set; }
    public DateTime? NgayTraPhong { get; set; }
    public int SoNguoiLon { get; set; } = 1;
    public int SoTreEm { get; set; } = 0;
}
