using System.Threading.Tasks;

namespace SST.StockImport.Services;

/// <summary>
/// 邮件服务实现（SMTP）
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;

    public SmtpEmailService(string smtpHost, int smtpPort, string smtpUsername, string smtpPassword, string fromEmail)
    {
        _smtpHost = smtpHost;
        _smtpPort = smtpPort;
        _smtpUsername = smtpUsername;
        _smtpPassword = smtpPassword;
        _fromEmail = fromEmail;
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string message)
    {
        return await SendEmailAsync(new[] { to }, subject, message);
    }

    public async Task<bool> SendEmailAsync(string[] to, string subject, string message)
    {
        try
        {
            using var client = new System.Net.Mail.SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new System.Net.NetworkCredential(_smtpUsername, _smtpPassword)
            };

            using var mailMessage = new System.Net.Mail.MailMessage
            {
                From = new System.Net.Mail.MailAddress(_fromEmail),
                Subject = subject,
                Body = message,
                IsBodyHtml = false
            };

            foreach (var recipient in to)
            {
                mailMessage.To.Add(recipient);
            }

            await client.SendMailAsync(mailMessage);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// 邮件服务实现（模拟）- 用于开发和测试
/// </summary>
public class MockEmailService : IEmailService
{
    public Task<bool> SendEmailAsync(string to, string subject, string message)
    {
        System.Console.WriteLine($"[Mock Email] To: {to}");
        System.Console.WriteLine($"[Mock Email] Subject: {subject}");
        System.Console.WriteLine($"[Mock Email] Message: {message}");
        return Task.FromResult(true);
    }

    public Task<bool> SendEmailAsync(string[] to, string subject, string message)
    {
        System.Console.WriteLine($"[Mock Email] To: {string.Join(",", to)}");
        System.Console.WriteLine($"[Mock Email] Subject: {subject}");
        System.Console.WriteLine($"[Mock Email] Message: {message}");
        return Task.FromResult(true);
    }
}
