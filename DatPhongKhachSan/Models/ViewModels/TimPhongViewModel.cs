using System.ComponentModel.DataAnnotations;

namespace DatPhongKhachSan.Models.ViewModels;

public class TimPhongViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn ngày nhận phòng")]
    [DataType(DataType.Date)]
    public DateTime NgayNhanPhong { get; set; } = DateTime.Today.AddDays(1);

    [Required(ErrorMessage = "Vui lòng chọn ngày trả phòng")]
    [DataType(DataType.Date)]
    public DateTime NgayTraPhong { get; set; } = DateTime.Today.AddDays(2);

    [Range(1, 10, ErrorMessage = "Số người lớn từ 1-10")]
    public int SoNguoiLon { get; set; } = 1;

    [Range(0, 5, ErrorMessage = "Số trẻ em từ 0-5")]
    public int SoTreEm { get; set; } = 0;

    public List<LoaiPhongCardViewModel> KetQua { get; set; } = new();
    public bool DaTimKiem { get; set; } = false;
}
