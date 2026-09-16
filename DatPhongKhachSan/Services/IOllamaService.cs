namespace DatPhongKhachSan.Services;

public interface IOllamaService
{
    Task<float[]?> GetEmbeddingAsync(string text);
    Task<string?> GenerateWithQwenAsync(string systemPrompt,
        IEnumerable<(string Role, string Content)> messages);
    Task WarmUpAsync();
    Task<(bool Connected, bool HasModel)> CheckStatusAsync();
}
