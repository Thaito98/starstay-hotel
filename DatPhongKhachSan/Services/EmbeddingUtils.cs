using System.Text;
using System.Text.RegularExpressions;

namespace DatPhongKhachSan.Services;

public static class EmbeddingUtils
{
    // Chuẩn hóa text - GIỐNG HỆT lúc index và lúc query. Không đổi logic.
    public static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var s = text.Normalize(NormalizationForm.FormC).Trim();
        return Regex.Replace(s, @"\s+", " ");
    }

    // Serialize float[] => byte[] (raw Buffer.BlockCopy - 4 bytes/float, little-endian).
    // 1115 embedding trong DB ghi bằng đúng thuật toán này - KHÔNG đổi.
    public static byte[] ToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public static float[]? ToFloats(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
