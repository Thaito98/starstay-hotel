namespace DatPhongKhachSan.Services;

public interface IEmbeddingProvider
{
    string ModelId { get; }
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string text);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts);
}
