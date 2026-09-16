using DatPhongKhachSan.Services;

namespace DatPhongKhachSan.Services;

public class DonHetHanCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DonHetHanCleanupService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public DonHetHanCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<DonHetHanCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<DatPhongService>();
                await svc.HuyDonHetHanAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DonHetHanCleanupService: lỗi khi dọn đơn hết hạn");
            }
        }
    }
}
