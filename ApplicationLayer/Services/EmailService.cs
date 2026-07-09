using ApplicationLayer.Interfaces.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Linq;

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
        var fromName = _config["EmailSettings:FromName"] ?? "HireGen AI";
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
        message.Subject = "[HireGen AI] Đặt lại mật khẩu của bạn";

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

    public async Task SendEmailVerificationAsync(string toEmail, string toName, string otp)
    {
        var fromName = _config["EmailSettings:FromName"] ?? "HireGen AI";
        var fromAddress = _config["EmailSettings:FromAddress"] ?? "noreply@iqgs.com";
        var smtpHost = _config["EmailSettings:SmtpHost"];
        var smtpPort = int.Parse(_config["EmailSettings:SmtpPort"] ?? "587");
        var username = _config["EmailSettings:Username"];
        var password = _config["EmailSettings:Password"];

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning(
                "[DEV] Email verification (chưa cấu hình SMTP).\nTo: {Email}\nOTP: {Otp}",
                toEmail, otp);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = "[HireGen AI] Xác minh địa chỉ email của bạn";
        message.Body = new TextPart("html") { Text = BuildVerifyEmailOtpHtml(toName, otp) };

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
            _logger.LogError(ex, "Gửi email xác minh thất bại.\nTo: {Email}", toEmail);
        }
    }

    // ────────────────────────────────────────────────────────────────
    private const string LogoHtml = """
        <table role="presentation" cellpadding="0" cellspacing="0"><tr>
          <td style="width:36px;height:36px;border-radius:10px;text-align:center;vertical-align:middle;
                     background-color:#6d28d9;background-image:linear-gradient(135deg,#8b5cf6,#4f46e5)">
            <span style="color:#ffffff;font-size:18px;font-weight:800;font-family:Segoe UI,Arial,sans-serif;line-height:36px">H</span>
          </td>
          <td style="padding-left:10px;vertical-align:middle">
            <span style="font-size:18px;font-weight:800;color:#0f172a;font-family:Segoe UI,Arial,sans-serif">HireGen</span><span style="font-size:18px;font-weight:800;color:#7c3aed;font-family:Segoe UI,Arial,sans-serif"> AI</span>
          </td>
        </tr></table>
        """;

    private static string BuildVerifyEmailOtpHtml(string name, string otp)
    {
        var digitsHtml = string.Join("", otp.Select(d => $"""
            <td style="width:44px;height:56px;background:#f5f3ff;border:1px solid #ddd6fe;border-radius:8px;
                       font-size:26px;font-weight:700;color:#6d28d9;text-align:center;vertical-align:middle;
                       font-family:'Courier New',Consolas,monospace">{d}</td>
            """));

        return $$"""
        <!DOCTYPE html>
        <html lang="vi">
        <head><meta charset="UTF-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/></head>
        <body style="margin:0;padding:0;background:#f1f5f9;font-family:Segoe UI,Helvetica,Arial,sans-serif">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f1f5f9;padding:32px 16px">
            <tr><td align="center">
              <table role="presentation" width="480" cellpadding="0" cellspacing="0"
                     style="background:#ffffff;border-radius:16px;overflow:hidden;max-width:480px;width:100%;box-shadow:0 1px 3px rgba(0,0,0,0.08)">
                <tr>
                  <td style="height:6px;background-color:#6d28d9;background-image:linear-gradient(90deg,#8b5cf6,#4f46e5);font-size:0;line-height:0">&nbsp;</td>
                </tr>
                <tr>
                  <td style="padding:24px 32px;border-bottom:1px solid #f1f5f9">
                    {{LogoHtml}}
                  </td>
                </tr>
                <tr>
                  <td style="padding:32px 32px 8px">
                    <h1 style="margin:0 0 4px;font-size:20px;color:#0f172a">Xác minh địa chỉ email</h1>
                    <p style="margin:0;color:#64748b;font-size:14px">Xin chào <strong style="color:#0f172a">{{name}}</strong>,</p>
                  </td>
                </tr>
                <tr>
                  <td style="padding:12px 32px 0">
                    <p style="margin:0;color:#475569;font-size:14px;line-height:22px">
                      Cảm ơn bạn đã đăng ký tài khoản HireGen AI. Vui lòng nhập mã xác minh bên dưới để hoàn tất quá trình đăng ký.
                    </p>
                  </td>
                </tr>
                <tr>
                  <td style="padding:28px 32px 8px" align="center">
                    <table role="presentation" cellpadding="0" cellspacing="6"><tr>{{digitsHtml}}</tr></table>
                  </td>
                </tr>
                <tr>
                  <td style="padding:8px 32px 0" align="center">
                    <span style="display:inline-block;background:#fef9c3;color:#854d0e;font-size:12px;font-weight:600;
                                 padding:4px 12px;border-radius:999px">Mã có hiệu lực trong 10 phút</span>
                  </td>
                </tr>
                <tr>
                  <td style="padding:28px 32px 0">
                    <p style="margin:0;color:#94a3b8;font-size:12.5px;line-height:20px">
                      Nếu bạn không tạo tài khoản HireGen AI, vui lòng bỏ qua email này — không cần thực hiện thêm hành động nào.
                    </p>
                  </td>
                </tr>
                <tr><td style="padding:28px 32px 0"><hr style="border:none;border-top:1px solid #e2e8f0;margin:0"/></td></tr>
                <tr>
                  <td style="padding:16px 32px 28px" align="center">
                    <p style="margin:0;color:#cbd5e1;font-size:11.5px">© {{DateTime.UtcNow.Year}} HireGen AI — AI-Powered Interview Question Generator</p>
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }

    // ────────────────────────────────────────────────────────────────
    private static string BuildResetEmailHtml(string name, string resetLink) => $"""
        <!DOCTYPE html>
        <html lang="vi">
        <head><meta charset="UTF-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/></head>
        <body style="margin:0;padding:0;background:#f1f5f9;font-family:Segoe UI,Helvetica,Arial,sans-serif">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f1f5f9;padding:32px 16px">
            <tr><td align="center">
              <table role="presentation" width="480" cellpadding="0" cellspacing="0"
                     style="background:#ffffff;border-radius:16px;overflow:hidden;max-width:480px;width:100%;box-shadow:0 1px 3px rgba(0,0,0,0.08)">
                <tr>
                  <td style="height:6px;background-color:#6d28d9;background-image:linear-gradient(90deg,#8b5cf6,#4f46e5);font-size:0;line-height:0">&nbsp;</td>
                </tr>
                <tr>
                  <td style="padding:24px 32px;border-bottom:1px solid #f1f5f9">
                    {LogoHtml}
                  </td>
                </tr>
                <tr>
                  <td style="padding:32px 32px 8px">
                    <h1 style="margin:0 0 4px;font-size:20px;color:#0f172a">Đặt lại mật khẩu</h1>
                    <p style="margin:0;color:#64748b;font-size:14px">Xin chào <strong style="color:#0f172a">{name}</strong>,</p>
                  </td>
                </tr>
                <tr>
                  <td style="padding:12px 32px 0">
                    <p style="margin:0;color:#475569;font-size:14px;line-height:22px">
                      Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Nhấp vào nút bên dưới để tạo mật khẩu mới.
                    </p>
                  </td>
                </tr>
                <tr>
                  <td style="padding:28px 32px 8px" align="center">
                    <a href="{resetLink}"
                       style="background-color:#6d28d9;background-image:linear-gradient(135deg,#8b5cf6,#4f46e5);color:#ffffff;
                              padding:13px 36px;border-radius:8px;text-decoration:none;font-size:15px;font-weight:600;display:inline-block">
                      Đặt lại mật khẩu
                    </a>
                  </td>
                </tr>
                <tr>
                  <td style="padding:8px 32px 0" align="center">
                    <span style="display:inline-block;background:#fef9c3;color:#854d0e;font-size:12px;font-weight:600;
                                 padding:4px 12px;border-radius:999px">Đường dẫn có hiệu lực trong 1 giờ</span>
                  </td>
                </tr>
                <tr>
                  <td style="padding:28px 32px 0">
                    <p style="margin:0;color:#94a3b8;font-size:12.5px;line-height:20px">
                      Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này. Đường dẫn này chỉ sử dụng được một lần.
                    </p>
                  </td>
                </tr>
                <tr><td style="padding:28px 32px 0"><hr style="border:none;border-top:1px solid #e2e8f0;margin:0"/></td></tr>
                <tr>
                  <td style="padding:16px 32px 28px" align="center">
                    <p style="margin:0;color:#cbd5e1;font-size:11.5px">© {DateTime.UtcNow.Year} HireGen AI — AI-Powered Interview Question Generator</p>
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}
