using System.Threading.Tasks;

namespace SST.StockImport.Services;

/// <summary>
/// 邮件服务接口
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// 发送邮件
    /// </summary>
    Task<bool> SendEmailAsync(string to, string subject, string message);

    /// <summary>
    /// 发送邮件给多个收件人
    /// </summary>
    Task<bool> SendEmailAsync(string[] to, string subject, string message);
}
