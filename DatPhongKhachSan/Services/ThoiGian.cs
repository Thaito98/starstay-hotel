namespace DatPhongKhachSan.Services;

// Helper trả giờ Việt Nam (UTC+7) thay vì giờ server host.
// Dùng thay DateTime.Now / DateTime.Today trên mọi luồng nghiệp vụ.
// Cross-platform: thử Windows ID trước, fallback sang IANA (Linux/macOS).
public static class ThoiGian
{
    private static readonly TimeZoneInfo _vn = FindVn();

    private static TimeZoneInfo FindVn()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
    }

    public static DateTime Now   => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _vn);
    public static DateTime Today => Now.Date;
}
