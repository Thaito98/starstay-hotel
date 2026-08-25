using DatPhongKhachSan.Models.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DatPhongKhachSan.Services;

public class EmailConfig
{
    public bool Enabled { get; set; } = false;
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromName { get; set; } = "StarStay Hotel";
    public string FromEmail { get; set; } = "noreply@staystay.vn";
}

public class EmailService
{
    private readonly EmailConfig _cfg;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailConfig> cfg, ILogger<EmailService> logger)
    {
        _cfg = cfg.Value;
        _logger = logger;
    }

    public async Task GuiXacNhanDatPhongAsync(string toEmail, string toName, DatPhong don, decimal tongTien)
    {
        if (!_cfg.Enabled)
        {
            _logger.LogInformation("Email disabled - skipping confirmation to {Email}", toEmail);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_cfg.FromName, _cfg.FromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = $"[StarStay] Xác nhận đặt phòng #{don.MaDon}";

        var body = $"""
            <h2>Xác nhận đặt phòng thành công!</h2>
            <p>Kính gửi <strong>{toName}</strong>,</p>
            <p>StarStay Hotel xác nhận đơn đặt phòng của quý khách:</p>
            <ul>
              <li><b>Mã đơn:</b> {don.MaDon}</li>
              <li><b>Nhận phòng:</b> {don.NgayNhanPhong:dd/MM/yyyy}</li>
              <li><b>Trả phòng:</b> {don.NgayTraPhong:dd/MM/yyyy} ({don.SoDem} đêm)</li>
              <li><b>Tổng tiền:</b> {tongTien:N0} ₫</li>
            </ul>
            <p>Mọi thắc mắc vui lòng liên hệ: <a href="mailto:info@staystay.vn">info@staystay.vn</a> hoặc 0236 123 4567.</p>
            <p>Trân trọng,<br/><b>StarStay Hotel</b></p>
            """;

        message.Body = new TextPart("html") { Text = body };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_cfg.SmtpHost, _cfg.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_cfg.Username, _cfg.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi email xác nhận thất bại tới {Email}", toEmail);
        }
    }
}
