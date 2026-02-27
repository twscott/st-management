using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Serilog;

namespace SST.StockImport.API.Tests;

/// <summary>
/// 自定義 WebApplicationFactory，用於測試環境配置
/// 核心：重置全局 Serilog 狀態以避免 "logger already frozen" 錯誤
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly object LoggerLock = new object();

    public CustomWebApplicationFactory()
    {
        // 重置全局 Serilog 狀態
        lock (LoggerLock)
        {
            try
            {
                // 關閉並刷新任何現有的全局 logger
                Log.CloseAndFlush();
            }
            catch
            {
                // 忽略任何異常
            }

            // 為測試創建新的 logger（最小配置以避免 stdout 混亂）
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .WriteTo.Console()
                .CreateLogger();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 設定為 Testing 環境，讓 Program.cs 知道這是測試
        builder.UseEnvironment("Testing");
        base.ConfigureWebHost(builder);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        
        // 清理測試 logger
        lock (LoggerLock)
        {
            try
            {
                Log.CloseAndFlush();
            }
            catch
            {
                // 忽略異常
            }
        }
    }
}
