namespace DatPhongKhachSan.Models.ViewModels;

public class LichSuDonViewModel
{
    public int MaDatPhong { get; set; }
    public string MaDon { get; set; } = string.Empty;
    public string TenLoaiPhong { get; set; } = string.Empty;
    public string SoPhong { get; set; } = string.Empty;
    public DateOnly NgayNhanPhong { get; set; }
    public DateOnly NgayTraPhong { get; set; }
    public int SoDem { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public string LoaiThanhToan { get; set; } = string.Empty;
    public string NguonDat { get; set; } = string.Empty;
    public decimal TongTien { get; set; }
    public decimal DaTra { get; set; }
    public decimal ConLai { get; set; }
    public DateTime NgayDat { get; set; }
    public bool CoTheHuy { get; set; }
    public int TiLeHoan { get; set; }
    public decimal SoTienHoan { get; set; }
    public DateTime? NgayHuy { get; set; }
}
