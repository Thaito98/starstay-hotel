using DatPhongKhachSan.Services.Chatbot;
using Xunit;

namespace DatPhongKhachSan.Tests.Tools;

/// <summary>
/// Kiểm tra AgentService.LocPII - che đúng, không che nhầm.
/// </summary>
public class LocPIITests
{
    // ── Che đúng ──────────────────────────────────────────────────────────────

    [Fact]
    public void CccdDayDu_BiBe()
    {
        var result = AgentService.LocPII("CCCD của bạn là 079201012345 nhé.");
        Assert.DoesNotContain("079201012345", result);
        Assert.Contains("***", result);
    }

    [Fact]
    public void Sdt10So_BiBe_GiuDauCuoi()
    {
        var result = AgentService.LocPII("SĐT: 0912345678");
        Assert.DoesNotContain("0912345678", result);
        Assert.Contains("0912", result);   // 4 số đầu còn
        Assert.Contains("678", result);    // 3 số cuối còn
        Assert.Equal("SĐT: 0912***678", result);
    }

    [Fact]
    public void Sdt11So_BiBe_GiuDauCuoi()
    {
        var result = AgentService.LocPII("Gọi 09123456789 để hỗ trợ.");
        Assert.DoesNotContain("09123456789", result);
        Assert.Contains("0912", result);
        Assert.Contains("789", result);
        Assert.Equal("Gọi 0912***789 để hỗ trợ.", result);
    }

    [Fact]
    public void NhieuPII_MotLuot_TatCaBiBe()
    {
        var text   = "CCCD 079201012345, SĐT 0987654321";
        var result = AgentService.LocPII(text);
        Assert.DoesNotContain("079201012345", result);
        Assert.DoesNotContain("0987654321",   result);
    }

    // ── Không che nhầm ────────────────────────────────────────────────────────

    [Fact]
    public void GiaTien_CoPhay_KhongBiBe()
    {
        var text   = "Giá phòng Deluxe: 1,500,000 VNĐ/đêm.";
        var result = AgentService.LocPII(text);
        Assert.Equal(text, result);   // không thay đổi
    }

    [Fact]
    public void GiaTien_KhongPhay_KhongBiBe()
    {
        // 7 chữ số, không bắt đầu bằng 0 => không phải SĐT, không phải CCCD
        var text   = "Tổng tiền: 3500000 VNĐ.";
        var result = AgentService.LocPII(text);
        Assert.Equal(text, result);
    }

    [Fact]
    public void MaDon_ChuocDem_KhongBiBe()
    {
        // Mã đơn: "DP20260610230422990" - chữ số nằm trong chuỗi alnum
        var text   = "Mã đơn: DP20260610230422990";
        var result = AgentService.LocPII(text);
        Assert.Equal(text, result);
    }

    [Fact]
    public void Ngay_ISOFormat_KhongBiBe()
    {
        var text   = "Nhận phòng 2026-09-01, trả phòng 2026-09-03.";
        var result = AgentService.LocPII(text);
        Assert.Equal(text, result);
    }

    [Fact]
    public void Ngay_SlashFormat_KhongBiBe()
    {
        var text   = "Từ 20/8/2026 đến 22/8/2026.";
        var result = AgentService.LocPII(text);
        Assert.Equal(text, result);
    }

    [Fact]
    public void SoDem_SoPhong_NhoHon10_KhongBiBe()
    {
        var text   = "2 đêm, phòng 101, tầng 3.";
        var result = AgentService.LocPII(text);
        Assert.Equal(text, result);
    }
}
