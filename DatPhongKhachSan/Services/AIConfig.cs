namespace DatPhongKhachSan.Services;

public class AIConfig
{
    public string Provider           { get; set; } = "Ollama";
    public string BaseUrl            { get; set; } = "http://localhost:11434/v1";
    public string ApiKey             { get; set; } = "";
    public string EmbeddingModel     { get; set; } = "qllama/bge-m3:q8_0";
    public int    EmbeddingDimensions{ get; set; } = 1024;
    public string ChatModel          { get; set; } = "qwen2.5:7b-instruct";
    public int    MaxToolRounds      { get; set; } = 3;
    public int    TimeoutSeconds     { get; set; } = 30;
    public float  CosineThreshold    { get; set; } = 0.5f;

    // CRAG-lite: phân loại độ tin cậy FAQ theo cosine score (đo thực tế trên bge-m3 + Cloudflare)
    // nguongTot >= 0.78: khớp tốt => LLM dùng FAQ với confidence cao
    // nguongTrungBinh 0.60-0.78: có thể liên quan => LLM tự phán xét
    // < nguongTrungBinh: không đủ bằng chứng => LLM nói không biết
    public float CragNguongTot       { get; set; } = 0.78f;
    public float CragNguongTrungBinh { get; set; } = 0.60f;

    // Provider dự phòng cho chat (ví dụ Groq) - để trống = tắt fallback
    public string FallbackChatBaseUrl { get; set; } = "";
    public string FallbackChatApiKey  { get; set; } = "";
    public string FallbackChatModel   { get; set; } = "";
}
