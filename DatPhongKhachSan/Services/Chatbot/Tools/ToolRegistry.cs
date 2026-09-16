using Microsoft.Extensions.Options;

namespace DatPhongKhachSan.Services.Chatbot.Tools;

public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _map;

    public int MaxToolRounds { get; }

    public ToolRegistry(IEnumerable<ITool> tools, IOptions<AIConfig> cfg)
    {
        _map          = tools.ToDictionary(t => t.Ten, t => t);
        MaxToolRounds = cfg.Value.MaxToolRounds;
    }

    public ITool? TimTool(string ten) =>
        _map.TryGetValue(ten, out var t) ? t : null;

    public IReadOnlyList<object> BuildToolSchemas() =>
        _map.Values.Select(t => (object)new
        {
            type = "function",
            function = new
            {
                name        = t.Ten,
                description = t.MoTa,
                parameters  = t.ThamSoSchema
            }
        }).ToList();
}
