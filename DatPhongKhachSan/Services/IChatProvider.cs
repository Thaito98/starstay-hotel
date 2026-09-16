namespace DatPhongKhachSan.Services;

public record ToolCall(string Id, string Name, string ArgumentsJson);

public record ChatKetQua(string? NoiDung, IReadOnlyList<ToolCall> ToolCalls);

public interface IChatProvider
{
    // Dùng bởi ChatbotController (GenQwen) - simple role+content, không có tool calling
    Task<ChatKetQua> ChatAsync(
        IEnumerable<(string Role, string Content)> messages,
        IEnumerable<object>? tools = null,
        CancellationToken ct = default);

    // Dùng bởi AgentService - structured messages, hỗ trợ tool_calls + tool results
    Task<ChatKetQua> ChatWithToolsAsync(
        IReadOnlyList<object> messages,
        IReadOnlyList<object> tools,
        CancellationToken ct = default)
        => throw new NotImplementedException(nameof(ChatWithToolsAsync));
}
