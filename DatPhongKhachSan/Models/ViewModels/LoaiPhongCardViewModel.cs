namespace DatPhongKhachSan.Models.ViewModels;

public class LoaiPhongCardViewModel
{
    public int MaLoaiPhong { get; set; }
    public string TenLoaiPhong { get; set; } = string.Empty;
    public string? MoTa { get; set; }
    public decimal GiaCoBan { get; set; }
    public string? AnhChinh { get; set; }
    public int SucChuaNguoiLon { get; set; }
    public int SucChuaTreEm { get; set; }
    public string? LoaiGiuong { get; set; }
    public decimal? DienTich { get; set; }
    public List<string> TenTienNghis { get; set; } = new();
    public List<string> BieuTuongTienNghis { get; set; } = new();
    public int SoPhongConTrong { get; set; }
}
