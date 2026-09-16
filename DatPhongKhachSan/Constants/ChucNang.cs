namespace DatPhongKhachSan.Constants;

/// <summary>
/// ClaimType cố định = "ChucNang" (plan.md mục 6.2).
/// Mỗi giá trị tương ứng 1 Policy đăng ký trong Program.cs.
/// Admin tối cao có tất cả; NhanVien chỉ có những cái được gán qua PhanQuyen.
/// </summary>
public static class ChucNang
{
    public const string ClaimType = "ChucNang";

    // Khu lễ tân
    public const string CheckInOut      = "CheckInOut";    // nhận/trả phòng
    public const string DatWalkIn       = "DatWalkIn";     // đặt tại quầy
    public const string ChuyenGiaHan    = "ChuyenGiaHan";  // chuyển phòng / gia hạn
    public const string TraCuuDon       = "TraCuuDon";     // tra cứu đơn

    // Buồng phòng
    public const string BuongPhong      = "BuongPhong";    // giao việc & nghiệm thu

    // Nhân sự (Admin)
    public const string ChamCong        = "ChamCong";      // chấm công
    public const string XepCa           = "XepCa";         // xếp ca

    // Quản trị (Admin)
    public const string QuanLyDanhMuc   = "QuanLyDanhMuc"; // CRUD loại phòng/phòng/tiện nghi/dịch vụ
    public const string QuanLyDon       = "QuanLyDon";     // xem/xử lý tất cả đơn
    public const string QuanLyNguoiDung = "QuanLyNguoiDung"; // khóa/mở, đổi role
    public const string QuanLyChatbot   = "QuanLyChatbot"; // FAQ + re-index
    public const string XemBaoCao       = "XemBaoCao";     // báo cáo doanh thu
    public const string GanQuyen        = "GanQuyen";      // gán claim cho người khác (cao nhất)

    /// <summary>Tất cả claim - dùng để seed admin và hiển thị PhanQuyen.</summary>
    public static readonly string[] TatCa =
    [
        CheckInOut, DatWalkIn, ChuyenGiaHan, TraCuuDon,
        BuongPhong,
        ChamCong, XepCa,
        QuanLyDanhMuc, QuanLyDon, QuanLyNguoiDung, QuanLyChatbot, XemBaoCao,
        GanQuyen
    ];

    public static readonly Dictionary<string, string> NhanLabels = new()
    {
        [CheckInOut]      = "Check-in / Check-out",
        [DatWalkIn]       = "Đặt phòng tại quầy (Walk-in)",
        [ChuyenGiaHan]    = "Chuyển phòng / Gia hạn",
        [TraCuuDon]       = "Tra cứu & xử lý đơn",
        [BuongPhong]      = "Giao việc & nghiệm thu buồng phòng",
        [ChamCong]        = "Chấm công",
        [XepCa]           = "Xếp ca làm việc",
        [QuanLyDanhMuc]   = "Quản lý danh mục (loại phòng, phòng...)",
        [QuanLyDon]       = "Xem & xử lý tất cả đơn",
        [QuanLyNguoiDung] = "Quản lý người dùng (khóa/mở, đổi quyền)",
        [QuanLyChatbot]   = "Quản lý Chatbot / FAQ",
        [XemBaoCao]       = "Xem báo cáo doanh thu",
        [GanQuyen]        = "Gán quyền cho nhân viên khác",
    };
}
