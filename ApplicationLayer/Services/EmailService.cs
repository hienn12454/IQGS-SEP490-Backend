using ApplicationLayer.Interfaces.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace ApplicationLayer.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink)
    {
        var fromName = _config["EmailSettings:FromName"] ?? "IQGS Platform";
        var fromAddress = _config["EmailSettings:FromAddress"] ?? "noreply@iqgs.com";
        var smtpHost = _config["EmailSettings:SmtpHost"];
        var smtpPort = int.Parse(_config["EmailSettings:SmtpPort"] ?? "587");
        var username = _config["EmailSettings:Username"];
        var password = _config["EmailSettings:Password"];

        // Development fallback — log thay vì gửi thật
        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning(
                "[DEV] Reset password email (chưa cấu hình SMTP).\nTo: {Email}\nLink: {Link}",
                toEmail, resetLink);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = "[IQGS] Đặt lại mật khẩu của bạn";

        message.Body = new TextPart("html")
        {
            Text = BuildResetEmailHtml(toName, resetLink)
        };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);
            _logger.LogInformation("Đã gửi email đặt lại mật khẩu đến {Email}.", toEmail);
        }
        catch (Exception ex)
        {
            // Không throw — email là best-effort, không được làm fail entire flow
            _logger.LogError(ex, "Gửi email đặt lại mật khẩu thất bại.\nTo: {Email}\nLink: {Link}", toEmail, resetLink);
        }
    }

    public async Task SendEmailVerificationAsync(string toEmail, string toName, string verifyLink)
    {
        var fromName = _config["EmailSettings:FromName"] ?? "IQGS Platform";
        var fromAddress = _config["EmailSettings:FromAddress"] ?? "noreply@iqgs.com";
        var smtpHost = _config["EmailSettings:SmtpHost"];
        var smtpPort = int.Parse(_config["EmailSettings:SmtpPort"] ?? "587");
        var username = _config["EmailSettings:Username"];
        var password = _config["EmailSettings:Password"];

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning(
                "[DEV] Email verification (chưa cấu hình SMTP).\nTo: {Email}\nLink: {Link}",
                toEmail, verifyLink);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = "[IQGS] Xác minh địa chỉ email của bạn";
        message.Body = new TextPart("html") { Text = BuildVerifyEmailHtml(toName, verifyLink) };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);
            _logger.LogInformation("Đã gửi email xác minh đến {Email}.", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi email xác minh thất bại.\nTo: {Email}\nLink: {Link}", toEmail, verifyLink);
        }
    }

    // ────────────────────────────────────────────────────────────────
    private static string BuildVerifyEmailHtml(string name, string verifyLink) => $"""
        <!DOCTYPE html>
        <html lang="vi">
        <head><meta charset="UTF-8"/></head>
        <body style="font-family:Arial,sans-serif;color:#333;max-width:600px;margin:0 auto;padding:24px">
          <h2 style="color:#2563eb">Xác minh email IQGS</h2>
          <p>Xin chào <strong>{name}</strong>,</p>
          <p>Cảm ơn bạn đã đăng ký! Nhấp vào nút bên dưới để xác minh địa chỉ email. Đường dẫn có hiệu lực trong <strong>24 giờ</strong>.</p>
          <p style="margin:32px 0">
            <a href="{verifyLink}"
               style="background:#16a34a;color:#fff;padding:12px 28px;border-radius:6px;text-decoration:none;font-size:15px">
              Xác minh email
            </a>
          </p>
          <p style="color:#888;font-size:13px">Nếu bạn không tạo tài khoản IQGS, hãy bỏ qua email này.</p>
          <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
          <p style="color:#aaa;font-size:12px">IQGS — AI-Powered Interview Question Generation System</p>
        </body>
        </html>
        """;

    // ────────────────────────────────────────────────────────────────
    private static string BuildResetEmailHtml(string name, string resetLink) => $"""
        <!DOCTYPE html>
        <html lang="vi">
        <head><meta charset="UTF-8"/></head>
        <body style="font-family:Arial,sans-serif;color:#333;max-width:600px;margin:0 auto;padding:24px">
          <h2 style="color:#2563eb">Đặt lại mật khẩu IQGS</h2>
          <p>Xin chào <strong>{name}</strong>,</p>
          <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.</p>
          <p>Nhấp vào nút bên dưới để đặt lại mật khẩu. Đường dẫn có hiệu lực trong <strong>1 giờ</strong>.</p>
          <p style="margin:32px 0">
            <a href="{resetLink}"
               style="background:#2563eb;color:#fff;padding:12px 28px;border-radius:6px;text-decoration:none;font-size:15px">
              Đặt lại mật khẩu
            </a>
          </p>
          <p style="color:#888;font-size:13px">
            Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.<br/>
            Đường dẫn này chỉ sử dụng được <strong>một lần</strong>.
          </p>
          <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
          <p style="color:#aaa;font-size:12px">IQGS — AI-Powered Interview Question Generation System</p>
        </body>
        </html>
        """;
}
