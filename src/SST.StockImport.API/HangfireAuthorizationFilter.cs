using Hangfire.Dashboard;

namespace SST.StockImport.API;

/// <summary>
/// Hangfire Dashboard 授權過濾器
/// 開發環境：允許所有訪問
/// 生產環境：需要實作更嚴格的授權邏輯（例如 IP 白名單、身份驗證等）
/// </summary>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // 取得 HttpContext
        var httpContext = context.GetHttpContext();

        // 開發環境：允許本地訪問
        if (httpContext.Request.Host.Host == "localhost" || 
            httpContext.Request.Host.Host == "127.0.0.1")
        {
            return true;
        }

        // 生產環境：實作更嚴格的授權邏輯
        // TODO: 實作 IP 白名單、JWT Token 驗證、或其他授權機制
        // 例如：
        // return httpContext.User.Identity?.IsAuthenticated == true;
        // return IsIpWhitelisted(httpContext.Connection.RemoteIpAddress);

        return false;
    }
}
