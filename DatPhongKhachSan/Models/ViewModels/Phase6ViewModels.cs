using DatPhongKhachSan.Models.Entities;

namespace DatPhongKhachSan.Models.ViewModels;

// ── Lịch ca (PhanCa) ─────────────────────────────────────────────────────────
public class LichCaViewModel
{
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public List<DateOnly> Ngays { get; set; } = new();
    public List<NguoiDung> NhanViens { get; set; } = new();
    public List<PhanCa> TatCaPhanCa { get; set; } = new();
    public List<CaLamViec> CacCa { get; set; } = new();
}

// ── Chấm công - 1 nhân viên trong bảng ngày ──────────────────────────────────
public class ChamCongNhanVienItem
{
    public int MaNguoiDung { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string TenCa { get; set; } = string.Empty;
    public string GioLamViec { get; set; } = string.Empty;  // "06:00-14:00"
    public string TrangThai { get; set; } = "DiLam";        // DiLam / Tre / Vang
    public int SoPhutTre { get; set; } = 0;
    public bool DaChamCong { get; set; }   // đã có bản ghi cho ngày này
    public int? MaChamCong { get; set; }   // PK nếu cần update
}

// ── Chấm công - toàn bộ trang chính ──────────────────────────────────────────
public class ChamCongNgayViewModel
{
    public DateOnly NgayCong { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public List<ChamCongNhanVienItem> DanhSach { get; set; } = new();
    public bool ChuaXepCa { get; set; }   // ngày chưa có ai được xếp ca
}

// ── Tổng hợp tháng ────────────────────────────────────────────────────────────
public class TongHopThangViewModel
{
    public int Thang { get; set; } = DateTime.Today.Month;
    public int Nam { get; set; } = DateTime.Today.Year;
    public List<TongHopNhanVienItem> DanhSach { get; set; } = new();
}

public class TongHopNhanVienItem
{
    public string HoTen { get; set; } = string.Empty;
    public decimal TongCong { get; set; }
    public int SoNgayDiLam { get; set; }  // TrangThai = DiLam
    public int SoLanTre { get; set; }     // TrangThai = Tre
    public int TongPhutTre { get; set; }  // SUM(SoPhutTre)
    public int SoNgayVang { get; set; }   // TrangThai = Vang
}
