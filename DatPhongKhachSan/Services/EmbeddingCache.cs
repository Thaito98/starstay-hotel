namespace DatPhongKhachSan.Services;

// Thread-safe LRU cache for query embedding vectors - Singleton.
// Capacity 500: each 1024-dim float[] ≈ 4 KB => ~2 MB max.
// Key = EmbeddingUtils.NormalizeText(query) so lần hỏi lại cùng câu = HIT.
public sealed class EmbeddingCache
{
    private readonly int _capacity;
    private readonly Dictionary<string, LinkedListNode<(string Key, float[] Vec)>> _map;
    private readonly LinkedList<(string Key, float[] Vec)> _lru;
    private readonly object _lock = new();
    private int _hits;
    private int _misses;

    public int Hits   => _hits;
    public int Misses => _misses;
    public int Count  { get { lock (_lock) return _lru.Count; } }

    public EmbeddingCache(int capacity = 500)
    {
        _capacity = capacity;
        _map      = new(capacity + 1);
        _lru      = new();
    }

    public bool TryGet(string key, out float[] vec)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                vec = node.Value.Vec;
                _hits++;
                return true;
            }
            vec = Array.Empty<float>();
            _misses++;
            return false;
        }
    }

    public void Set(string key, float[] vec)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _lru.Remove(existing);
                _map.Remove(key);
            }
            var node = _lru.AddFirst((key, vec));
            _map[key] = node;

            while (_lru.Count > _capacity)
            {
                var last = _lru.Last!;
                _lru.RemoveLast();
                _map.Remove(last.Value.Key);
            }
        }
    }
}
