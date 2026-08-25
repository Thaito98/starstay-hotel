using System.Text.Json;

namespace DatPhongKhachSan.Services.Chatbot.Tools;

public interface ITool
{
    string Ten { get; }
    string MoTa { get; }
    object ThamSoSchema { get; }
    bool CanDangNhap { get; }
    Task<string> ThucThiAsync(JsonElement thamSo, ToolContext ctx, CancellationToken ct = default);
}
