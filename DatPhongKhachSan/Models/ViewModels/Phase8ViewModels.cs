namespace DatPhongKhachSan.Models.ViewModels;

// Dành cho trang quản lý người dùng của Admin
public class NguoiDungAdminViewModel
{
    public int MaNguoiDung { get; set; }
    public string? UserId { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? SoDienThoai { get; set; }
    public string VaiTro { get; set; } = "KhachHang";
    public bool IsLocked { get; set; }
    public string? PhanLoai { get; set; }
    public DateTime NgayTao { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool IsCurrentUser { get; set; }  // không cho tự đổi role chính mình
}
