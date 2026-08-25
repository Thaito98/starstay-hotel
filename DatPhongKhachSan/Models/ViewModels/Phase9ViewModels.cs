namespace DatPhongKhachSan.Models.ViewModels;

public class BaoCaoViewModel
{
    public int Thang { get; set; } = DateTime.Today.Month;
    public int Nam   { get; set; } = DateTime.Today.Year;

    // Doanh thu
    public decimal TongDoanhThu { get; set; }
    public decimal TongHoanTien { get; set; }
    public decimal DoanhThuThuan => TongDoanhThu - TongHoanTien;

    // Đơn
    public int SoDonDaXacNhan  { get; set; }
    public int SoDonDangO      { get; set; }
    public int SoDonDaTraPhong { get; set; }
    public int SoDonDaHuy      { get; set; }

    // Lấp đầy
    public int TongSoPhong          { get; set; }
    public double TiLeCapDayTrungBinh { get; set; }

    // Chi tiết từng ngày
    public List<BaoCaoNgayItem> ChiTietNgay { get; set; } = new();
}

public class BaoCaoNgayItem
{
    public DateOnly Ngay      { get; set; }
    public decimal DoanhThu   { get; set; }   // thu trong ngày (ThanhCong, không HoanTien)
    public decimal HoanTien   { get; set; }   // hoàn trong ngày
    public int PhongBan       { get; set; }   // số phòng có khách ngày đó
    public double TiLeCapDay  { get; set; }   // %
}
