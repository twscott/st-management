using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.API.Controllers;

/// <summary>
/// 健康檢查 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly StockImportDbContext _dbContext;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        StockImportDbContext dbContext,
        ILogger<HealthController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// 基本健康檢查
    /// </summary>
    /// <returns>服務狀態</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> GetHealth()
    {
        return Ok(new
        {
            status = "healthy",
            service = "SST Stock Import API",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }

    /// <summary>
    /// 資料庫連線檢查
    /// </summary>
    /// <returns>資料庫狀態</returns>
    [HttpGet("database")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<object>> GetDatabaseHealth()
    {
        try
        {
            // 嘗試連接資料庫
            var canConnect = await _dbContext.Database.CanConnectAsync();

            if (!canConnect)
            {
                return StatusCode(503, new
                {
                    status = "unhealthy",
                    component = "database",
                    error = "Cannot connect to database"
                });
            }

            // 執行簡單查詢測試
            var jobCount = await _dbContext.ImportJobs.CountAsync();

            return Ok(new
            {
                status = "healthy",
                component = "database",
                timestamp = DateTime.UtcNow,
                statistics = new
                {
                    totalJobs = jobCount
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            
            return StatusCode(503, new
            {
                status = "unhealthy",
                component = "database",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// 完整健康檢查（包含所有組件）
    /// </summary>
    /// <returns>完整系統狀態</returns>
    [HttpGet("detailed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetDetailedHealth()
    {
        var checks = new Dictionary<string, object>();

        // API 狀態
        checks["api"] = new { status = "healthy" };

        // 資料庫狀態
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync();
            checks["database"] = new
            {
                status = canConnect ? "healthy" : "unhealthy",
                connected = canConnect
            };
        }
        catch (Exception ex)
        {
            checks["database"] = new
            {
                status = "unhealthy",
                error = ex.Message
            };
        }

        // 系統資訊
        checks["system"] = new
        {
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            machineName = Environment.MachineName,
            osVersion = Environment.OSVersion.ToString(),
            processorCount = Environment.ProcessorCount,
            dotnetVersion = Environment.Version.ToString()
        };

        var overallHealthy = checks.Values
            .OfType<dynamic>()
            .All(c => c.status == "healthy");

        return Ok(new
        {
            status = overallHealthy ? "healthy" : "degraded",
            timestamp = DateTime.UtcNow,
            checks
        });
    }
}
