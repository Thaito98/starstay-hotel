using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DatPhongKhachSan.Services.VnPay;

// Theo chuẩn chính thức VNPay: ký TRÊN chuỗi đã URL-encode (không phải raw).
// vnp_ReturnUrl chứa "://" "/" cần encode trước khi ký, nếu không hash sẽ sai.
public class VnPayLibrary
{
    private readonly SortedList<string, string> _data =
        new(StringComparer.InvariantCultureIgnoreCase);

    public void AddData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
            _data[key] = value;
    }

    // Tạo URL: sort alphabet => URL-encode cả key lẫn value =>
    // nối "k=v&k2=v2..." => ký chuỗi đó => gắn vnp_SecureHash
    public string BuildUrl(string baseUrl, string hashSecret)
    {
        var sb = new StringBuilder();
        foreach (var (k, v) in _data)
            sb.Append(WebUtility.UrlEncode(k) + "=" + WebUtility.UrlEncode(v) + "&");

        // Chuỗi dùng để ký = query string không có dấu & cuối
        var signStr  = sb.ToString().TrimEnd('&');
        var hash     = HmacSha512(hashSecret, signStr);

        return $"{baseUrl}?{signStr}&vnp_SecureHash={hash}";
    }

    // Xác minh callback: ASP.NET trả về giá trị đã URL-decode,
    // phải encode lại trước khi hash (giống lúc tạo URL)
    public static bool VerifySignature(
        IQueryCollection query, string hashSecret, out string responseCode)
    {
        responseCode = query["vnp_ResponseCode"].ToString();
        var inputHash = query["vnp_SecureHash"].ToString();

        var sorted = new SortedList<string, string>(
            StringComparer.InvariantCultureIgnoreCase);

        foreach (var key in query.Keys)
        {
            if (key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("vnp_SecureHash",     StringComparison.OrdinalIgnoreCase)
                && !key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            {
                sorted[key] = query[key].ToString();
            }
        }

        var sb = new StringBuilder();
        foreach (var (k, v) in sorted)
            sb.Append(WebUtility.UrlEncode(k) + "=" + WebUtility.UrlEncode(v) + "&");

        var signStr = sb.ToString().TrimEnd('&');
        var computed = HmacSha512(hashSecret, signStr);

        return computed.Equals(inputHash, StringComparison.OrdinalIgnoreCase);
    }

    public static string HmacSha512(string key, string data)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(data))
        ).Replace("-", "").ToLower();
    }
}
