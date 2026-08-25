using DatPhongKhachSan.Data;
using DatPhongKhachSan.Models.Entities;
using DatPhongKhachSan.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DatPhongKhachSan.Tests.Tools;

/// Kiểm tra ChatbotService.TimFaqLikeAsync - fallback LIKE khi embedding service lỗi.
public class FaqLikeFallbackTests
{
    private static IServiceScopeFactory BuildScope(DatPhongKhachSanContext db)
    {
        var sp = new ServiceCollection()
            .AddSingleton<DatPhongKhachSanContext>(db)
            .BuildServiceProvider();
        return new Mock_ScopeFactory(sp);
    }

    // Minimal scope factory wrapper
    private class Mock_ScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _sp;
        public Mock_ScopeFactory(IServiceProvider sp) => _sp = sp;
        public IServiceScope CreateScope() => new Mock_Scope(_sp);
    }
    private class Mock_Scope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; }
        public Mock_Scope(IServiceProvider sp) => ServiceProvider = sp;
        public void Dispose() { }
    }

    private static DatPhongKhachSanContext MakeDb()
    {
        var opts = new DbContextOptionsBuilder<DatPhongKhachSanContext>()
            .UseInMemoryDatabase("FAQ_LIKE_" + Guid.NewGuid())
            .Options;
        return new DatPhongKhachSanContext(opts);
    }

    [Fact(DisplayName = "TimFaqLikeAsync tìm được FAQ khi query khớp CauHoi")]
    public async Task TimFaqLikeAsync_MatchCauHoi_ReturnsFaq()
    {
        var db = MakeDb();
        db.DoanVanBans.AddRange(
            new DoanVanBan { MaDoan = 1, CauHoi = "giờ nhận phòng là mấy giờ", TraLoi = "14:00", ChuDe = "checkin",  NgayIndex = DateTime.Now },
            new DoanVanBan { MaDoan = 2, CauHoi = "giờ trả phòng là mấy giờ",  TraLoi = "12:00", ChuDe = "checkout", NgayIndex = DateTime.Now },
            new DoanVanBan { MaDoan = 3, CauHoi = "wifi mật khẩu bao nhiêu",   TraLoi = "staystay2024", ChuDe = "wifi", NgayIndex = DateTime.Now }
        );
        await db.SaveChangesAsync();

        var svc = new ChatbotService(BuildScope(db), NullLogger<ChatbotService>.Instance);

        var result = await svc.TimFaqLikeAsync("nhận phòng");

        Assert.NotEmpty(result);
        Assert.Contains(result, r => r.CauHoi.Contains("nhận phòng"));
    }

    [Fact(DisplayName = "TimFaqLikeAsync trả rỗng khi query không khớp")]
    public async Task TimFaqLikeAsync_NoMatch_ReturnsEmpty()
    {
        var db = MakeDb();
        db.DoanVanBans.Add(
            new DoanVanBan { MaDoan = 1, CauHoi = "giờ nhận phòng là mấy giờ", TraLoi = "14:00", NgayIndex = DateTime.Now }
        );
        await db.SaveChangesAsync();

        var svc = new ChatbotService(BuildScope(db), NullLogger<ChatbotService>.Instance);

        var result = await svc.TimFaqLikeAsync("bơi lội yoga thiền định");

        Assert.Empty(result);
    }

    [Fact(DisplayName = "TimFaqLikeAsync fallback từng từ khi cụm không khớp")]
    public async Task TimFaqLikeAsync_WordFallback_FindsByWord()
    {
        var db = MakeDb();
        // Dùng tiếng Anh để tránh vấn đề Unicode normalize Vietnamese trong EF InMemory Contains
        db.DoanVanBans.Add(
            new DoanVanBan { MaDoan = 1, CauHoi = "wifi password hotel", TraLoi = "staystay2024", NgayIndex = DateTime.Now }
        );
        await db.SaveChangesAsync();

        var svc = new ChatbotService(BuildScope(db), NullLogger<ChatbotService>.Instance);

        // Cụm "wifi access code here" không khớp chính xác, nhưng từ "wifi" (4 chars) sẽ khớp
        var result = await svc.TimFaqLikeAsync("wifi access code here");

        Assert.NotEmpty(result);
    }
}
