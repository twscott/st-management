using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.API.Controllers;

/// <summary>
/// 每日自動任務管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DailyTaskController : ControllerBase
{
    private readonly ILogger<DailyTaskController> _logger;
    private readonly IDailyTaskExecutionService _executionService;
    private readonly IImportService _importService;
    private readonly ISupplementDataService _supplementDataService;

    public DailyTaskController(
        ILogger<DailyTaskController> logger,
        IDailyTaskExecutionService executionService,
        IImportService importService,
        ISupplementDataService supplementDataService)
    {
        _logger = logger;
        _executionService = executionService;
        _importService = importService;
        _supplementDataService = supplementDataService;
    }

    /// <summary>
    /// 獲取今日執行狀態
    /// </summary>
    /// <returns>今日任務執行記錄（如無則返回 null）</returns>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DailyTaskExecution?>> GetTodayStatus()
    {
        try
        {
            var execution = await _executionService.GetTodayExecutionAsync();
            return Ok(execution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取今日執行狀態失敗");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 獲取最近執行歷史記錄
    /// </summary>
    /// <param name="count">查詢筆數（預設 10）</param>
    /// <returns>執行記錄列表（依建立時間倒序）</returns>
    [HttpGet("recent")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<DailyTaskExecution>>> GetRecentExecutions([FromQuery] int count = 10)
    {
        try
        {
            var executions = await _executionService.GetRecentExecutionsAsync(count);
            return Ok(executions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取執行歷史失敗");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 手動觸發重試任務
    /// </summary>
    /// <returns>重試結果（成功/失敗）</returns>
    [HttpPost("retry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<object>> RetryTask()
    {
        try
        {
            // 1. 檢查是否有待重試的執行記錄
            var pendingExecution = await _executionService.GetPendingRetryAsync();
            if (pendingExecution == null)
            {
                return BadRequest(new { Error = "無待重試的任務" });
            }

            // 2. 執行重試（下載 + 統計）
            _logger.LogInformation("手動觸發重試，執行記錄 ID: {ExecutionId}", pendingExecution.Id);

            var targetDate = DateTime.Today;

            try
            {
                // 調用 ImportService 下載數據
                var importRequest = new ImportRequestDto
                {
                    Market = "ALL",  // 所有市場（TSE + OTC + Emerging）
                    DataSource = "TWSE",
                    TradeDate = targetDate,
                    ExecutorType = "MANUAL_RETRY",
                    ExecutorIdentity = "DailyTaskController"
                };
                var downloadResult = await _importService.ImportStockDataAsync(importRequest);
                if (!downloadResult.IsSuccess)
                {
                    throw new Exception($"下載數據失敗: {downloadResult.ErrorMessage ?? "未知錯誤"}");
                }

                // 調用 SupplementDataService 處理統計數據（處理當天 + 前 1 天）
                var supplementResult = await _supplementDataService.ProcessAllAsync(targetDate, days: 2);
                if (!supplementResult.Success)
                {
                    throw new Exception($"處理統計數據失敗: {supplementResult.ErrorMessage ?? "未知錯誤"}");
                }

                // 3. 更新為成功狀態
                pendingExecution.Status = DailyTaskStatus.Completed;
                pendingExecution.EndTime = DateTime.Now;
                await _executionService.UpdateExecutionAsync(pendingExecution);

                _logger.LogInformation("手動重試成功");
                return Ok(new { Success = true, Message = "重試成功" });
            }
            catch (Exception taskEx)
            {
                // 4. 更新失敗狀態並增加重試次數
                pendingExecution.Status = DailyTaskStatus.Failed;
                pendingExecution.ErrorMessage = taskEx.Message;
                pendingExecution.RetryCount++;
                await _executionService.UpdateExecutionAsync(pendingExecution);

                _logger.LogError(taskEx, "手動重試失敗");
                return StatusCode(500, new { Error = $"重試失敗: {taskEx.Message}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "手動重試觸發失敗");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}
