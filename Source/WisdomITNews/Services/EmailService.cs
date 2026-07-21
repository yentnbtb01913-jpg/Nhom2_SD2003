using System.Net;
using System.Net.Mail;

namespace WisdomITNews.Services;

/// <summary>
/// Gửi email thật qua SMTP (Gmail, SendGrid SMTP, Mailtrap, hoặc bất kỳ SMTP server nào).
/// Cấu hình trong appsettings.json -&gt; section "Smtp".
/// </summary>
public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_config["Smtp:Host"]) &&
        !string.IsNullOrWhiteSpace(_config["Smtp:Username"]) &&
        !string.IsNullOrWhiteSpace(_config["Smtp:Password"]);

    /// <summary>
    /// Gửi 1 email. Trả về (success, errorMessage).
    /// </summary>
    // Đây là luồng xử lý gửi 1 email qua SMTP
    // Luồng: 1) Chưa cấu hình SMTP -> trả lỗi
    //        2) Đọc cấu hình Smtp (host/port/ssl/user/pass/from) từ appsettings
    //        3) Tạo MailMessage HTML (UTF-8) -> SendMailAsync -> trả (success, error)
    public async Task<(bool success, string? error)> SendAsync(string toEmail, string subject, string htmlBody, string? toName = null)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("EmailService chưa được cấu hình (Smtp:Host/Username/Password thiếu).");
            return (false, "SMTP chưa được cấu hình trong appsettings.json");
        }

        try
        {
            var host = _config["Smtp:Host"]!;
            var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;
            var enableSsl = bool.TryParse(_config["Smtp:EnableSsl"], out var ssl) ? ssl : true;
            var user = _config["Smtp:Username"]!;
            var pass = _config["Smtp:Password"]!;
            var fromAddr = _config["Smtp:From"] ?? user;
            var fromName = _config["Smtp:FromName"] ?? "Wisdom IT News";

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(user, pass),
                EnableSsl   = enableSsl,
                Timeout     = 20000
            };

            using var msg = new MailMessage
            {
                From = new MailAddress(fromAddr, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                SubjectEncoding = System.Text.Encoding.UTF8,
                BodyEncoding = System.Text.Encoding.UTF8
            };
            msg.To.Add(string.IsNullOrWhiteSpace(toName)
                ? new MailAddress(toEmail)
                : new MailAddress(toEmail, toName));

            await client.SendMailAsync(msg);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gửi email thất bại tới {Email}", toEmail);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Gửi cùng 1 nội dung tới nhiều người nhận, mỗi người 1 email (không lộ danh sách).
    /// Trả về (countOk, countFail).
    /// </summary>
    // Đây là luồng xử lý gửi email hàng loạt (mỗi người 1 email riêng, không lộ danh sách)
    // Luồng: lặp qua từng người nhận (loại trùng) -> gọi SendAsync -> đếm ok/fail
    public async Task<(int ok, int fail)> SendBulkAsync(IEnumerable<string> recipients, string subject, string htmlBody)
    {
        int ok = 0, fail = 0;
        foreach (var to in recipients.Distinct())
        {
            if (string.IsNullOrWhiteSpace(to)) continue;
            var (success, _) = await SendAsync(to, subject, htmlBody);
            if (success) ok++; else fail++;
        }
        return (ok, fail);
    }

    /// <summary>
    /// Email chào mừng khi user đăng ký nhận newsletter.
    /// </summary>
    // Đây là luồng xử lý gửi email chào mừng khi đăng ký nhận bản tin
    public Task<(bool success, string? error)> SendWelcomeAsync(string toEmail)
    {
        var subject = "Chào mừng bạn đến với Wisdom IT News!";
        var html = $@"
            <div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:24px;color:#1f2937;'>
              <h2 style='color:#e63946;margin:0 0 12px;'>Wisdom IT News</h2>
              <p>Xin chào,</p>
              <p>Cảm ơn bạn đã đăng ký nhận bản tin từ <strong>Wisdom IT News</strong> — báo điện tử về Công nghệ, Lập trình & AI.</p>
              <p>Mỗi khi có bài viết nổi bật, chúng tôi sẽ gửi email tới <strong>{toEmail}</strong>.</p>
              <hr style='border:none;border-top:1px solid #e5e7eb;margin:20px 0;'/>
              <p style='font-size:12px;color:#6b7280;'>Bạn nhận được email này vì đã đăng ký tại wisdomitnews.local. Nếu đây là nhầm lẫn, vui lòng bỏ qua email.</p>
            </div>";
        return SendAsync(toEmail, subject, html);
    }
}
