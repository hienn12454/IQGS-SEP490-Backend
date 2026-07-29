using System.Net;

namespace ApplicationLayer.Helpers;

/// <summary>
/// Trang HTML server-rendered cho luồng public accept offer (candidate bấm link trong email) —
/// trả trực tiếp qua SuccessResp.Content, KHÔNG qua SMTP. Shell/màu đồng bộ với EmailService
/// nhưng đặt ở file riêng vì đây không phải nội dung email.
/// </summary>
public static class CandidateOfferPageHtml
{
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

    /// <summary>[GET] Trang xem lại nội dung offer + nút xác nhận (POST form) — chưa mutate gì.</summary>
    public static string BuildConfirmPage(string offerMessage, string rawToken) => BuildShell(
        heading: "Lời đề nghị phỏng vấn",
        bodyHtml: $"""
            <p style="margin:0 0 16px;color:#475569;font-size:14px;line-height:22px">
              Vui lòng xem lại nội dung bên dưới. Nhấn nút "Xác nhận" nếu bạn đồng ý tham gia phỏng vấn.
            </p>
            <div style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:16px;
                        color:#334155;font-size:14px;line-height:22px;white-space:pre-wrap;margin-bottom:24px">{WebUtility.HtmlEncode(offerMessage)}</div>
            <form method="post" action="/api/public/candidate-offers/{Uri.EscapeDataString(rawToken)}/accept" style="text-align:center">
              <button type="submit"
                      style="background-color:#6d28d9;background-image:linear-gradient(135deg,#8b5cf6,#4f46e5);color:#ffffff;
                             padding:13px 36px;border-radius:8px;border:none;text-decoration:none;font-size:15px;font-weight:600;
                             display:inline-block;cursor:pointer;font-family:Segoe UI,Helvetica,Arial,sans-serif">
                Xác nhận chấp nhận lời đề nghị
              </button>
            </form>
            """);

    /// <summary>[GET/POST] Đã accept từ trước — hiển thị lại nội dung, không có form.</summary>
    public static string BuildAlreadyAcceptedPage(string offerMessage, DateTime? acceptedAt) => BuildShell(
        heading: "Bạn đã xác nhận lời đề nghị này",
        bodyHtml: $"""
            <p style="margin:0 0 16px;color:#475569;font-size:14px;line-height:22px">
              Bạn đã chấp nhận lời đề nghị này{(acceptedAt.HasValue ? $" vào lúc {acceptedAt.Value:HH:mm dd/MM/yyyy} (UTC)" : "")}.
              HR đã được thông báo và sẽ liên hệ với bạn sớm.
            </p>
            <div style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:16px;
                        color:#334155;font-size:14px;line-height:22px;white-space:pre-wrap">{WebUtility.HtmlEncode(offerMessage)}</div>
            """);

    /// <summary>[POST] Vừa accept thành công.</summary>
    public static string BuildSuccessPage(string offerMessage) => BuildShell(
        heading: "Cảm ơn bạn đã xác nhận!",
        bodyHtml: """
            <p style="margin:0;color:#475569;font-size:14px;line-height:22px">
              Chúng tôi đã ghi nhận việc bạn chấp nhận lời đề nghị phỏng vấn. HR đã được thông báo qua email
              và sẽ liên hệ với bạn sớm để sắp xếp lịch cụ thể.
            </p>
            """);

    /// <summary>[GET/POST] Token sai hoặc đã hết hạn.</summary>
    public static string BuildInvalidOrExpiredPage() => BuildShell(
        heading: "Đường dẫn không hợp lệ hoặc đã hết hạn",
        bodyHtml: """
            <p style="margin:0;color:#475569;font-size:14px;line-height:22px">
              Đường dẫn này không còn hiệu lực. Vui lòng liên hệ HR để được gửi lại lời đề nghị mới.
            </p>
            """);

    private static string BuildShell(string heading, string bodyHtml) => $"""
        <!DOCTYPE html>
        <html lang="vi">
        <head><meta charset="UTF-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/>
        <title>HireGen AI</title></head>
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
                    <h1 style="margin:0;font-size:20px;color:#0f172a">{heading}</h1>
                  </td>
                </tr>
                <tr>
                  <td style="padding:16px 32px 32px">
                    {bodyHtml}
                  </td>
                </tr>
                <tr><td style="padding:0 32px 0"><hr style="border:none;border-top:1px solid #e2e8f0;margin:0"/></td></tr>
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
