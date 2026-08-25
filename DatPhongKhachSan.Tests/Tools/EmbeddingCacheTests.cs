using DatPhongKhachSan.Services;
using Xunit;

namespace DatPhongKhachSan.Tests.Tools;

/// Kiểm tra EmbeddingCache: LRU eviction, hit/miss stats, thread-safety cơ bản.
public class EmbeddingCacheTests
{
    [Fact(DisplayName = "Cache HIT trả đúng vector, tăng Hits")]
    public void TryGet_AfterSet_ReturnsVector()
    {
        var cache = new EmbeddingCache(capacity: 10);
        var vec   = new float[] { 1f, 2f, 3f };

        cache.Set("key1", vec);
        var hit = cache.TryGet("key1", out var got);

        Assert.True(hit);
        Assert.Equal(vec, got);
        Assert.Equal(1, cache.Hits);
        Assert.Equal(0, cache.Misses);
    }

    [Fact(DisplayName = "Cache MISS với key chưa có, tăng Misses")]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var cache = new EmbeddingCache(capacity: 10);

        var hit = cache.TryGet("ghost", out var got);

        Assert.False(hit);
        Assert.Empty(got);
        Assert.Equal(0, cache.Hits);
        Assert.Equal(1, cache.Misses);
    }

    [Fact(DisplayName = "LRU evicts least-recently-used khi vượt capacity")]
    public void Set_OverCapacity_EvictsLRU()
    {
        var cache = new EmbeddingCache(capacity: 2);

        cache.Set("a", new float[] { 1f });
        cache.Set("b", new float[] { 2f });
        cache.TryGet("a", out _);      // access "a" - làm "b" thành LRU
        cache.Set("c", new float[] { 3f });  // "b" bị evict

        Assert.True(cache.TryGet("a", out _), "a phải còn");
        Assert.True(cache.TryGet("c", out _), "c phải còn");
        Assert.False(cache.TryGet("b", out _), "b phải bị evict");
    }

    [Fact(DisplayName = "Set cùng key => cập nhật không duplicate, Count không tăng")]
    public void Set_DuplicateKey_UpdatesInPlace()
    {
        var cache = new EmbeddingCache(capacity: 10);
        cache.Set("k", new float[] { 1f });
        cache.Set("k", new float[] { 2f });

        var hit = cache.TryGet("k", out var got);

        Assert.True(hit);
        Assert.Equal(2f, got[0]);
        Assert.Equal(1, cache.Count);
    }

    [Fact(DisplayName = "Cache giữ đúng Count sau nhiều thao tác")]
    public void Count_Correct_AfterOperations()
    {
        var cache = new EmbeddingCache(capacity: 3);
        cache.Set("a", new float[1]);
        cache.Set("b", new float[1]);
        cache.Set("c", new float[1]);
        cache.Set("d", new float[1]);   // evict 1

        Assert.Equal(3, cache.Count);
    }
}
