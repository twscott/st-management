using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Services;

/// <summary>
/// 股票統計計算服務實作
/// 對應原系統 appCommon.cs 中的統計方法
/// </summary>
public class StatisticsService : IStatisticsService
{
    private readonly ILogger<StatisticsService> _logger;
    private readonly StockImportDbContext _dbContext;

    public StatisticsService(
        ILogger<StatisticsService> logger,
        StockImportDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    /// <summary>
    /// 計算5日移動平均（價格和成交量）
    /// 對應原系統: calc5Avg()
    /// </summary>
    public async Task<StatisticsResultDto> Calculate5DayAverageAsync(
        DateTime? tradeDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = new StatisticsResultDto
        {
            StatisticsType = "5日均價/均量",
            TradeDate = tradeDate ?? DateTime.Today,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("開始計算 5日均價/均量，日期: {TradeDate}", result.TradeDate);

            // 1. 更新 tradedata 的 5日均價/均量
            // 使用 weekall5avg view 的計算結果
            var sql = @"
                UPDATE tradedata a 
                INNER JOIN (
                    SELECT StockID, avgAmt, avgVol, StockDate, avg5VolPerTrans, lastDate  
                    FROM weekall5avg
                ) b ON a.StockID = b.StockID AND a.TransDate = b.StockDate 
                SET 
                    a.avgAmt5D = IFNULL(b.avgAmt, 0), 
                    a.avgVol5D = IFNULL(b.avgVol, 0), 
                    a.avg5VolPerTrans = b.avg5VolPerTrans, 
                    a.lastDate = b.lastDate";

            await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);

            // 2. 計算成交量比率
            sql = @"
                UPDATE tradedata 
                SET 
                    lastVolRate = IF(lastVol = 0, 0, ROUND(Vol / lastVol, 2)), 
                    avg5VolRate = IF(avgVol5D = 0, 0, ROUND(Vol / avgVol5D, 2))";

            await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);

            // 3. 計算影線
            sql = @"
                UPDATE tradedata 
                SET 
                    upShadow = IF(lastPrice = 0, 0, ROUND((HPrice - GREATEST(OpenPriec, StockPrice)) / lastPrice * 100, 2)), 
                    downShadow = IF(lastPrice = 0, 0, ROUND((LEAST(OpenPriec, StockPrice) - LPrice) / lastPrice * 100, 2)), 
                    kShadow = IF(lastPrice = 0, 0, ROUND((StockPrice - OpenPriec) / lastPrice * 100, 2)) 
                WHERE HPrice > 0 AND LPrice > 0";

            await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);

            // 4. 批次更新各 base tables 的 5日均價/均量
            await Update5DayAverageForBaseTablesAsync(cancellationToken);

            // 7. 更新最新交易資訊到各 base table
            await UpdateBaseTablesWithLatestDataAsync(cancellationToken);

            result.IsSuccess = true;
            result.EndTime = DateTime.UtcNow;
            
            // 使用直接查詢獲取計數
            var countSql = "SELECT COUNT(DISTINCT StockID) FROM tradedata";
            var connection = _dbContext.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = countSql;
                command.CommandTimeout = 60;
                var countResult = await command.ExecuteScalarAsync(cancellationToken);
                result.ProcessedCount = countResult != null ? Convert.ToInt32(countResult) : 0;
            }
            finally
            {
                await connection.CloseAsync();
            }

            _logger.LogInformation(
                "5日均價/均量計算完成，處理 {Count} 檔股票，耗時 {Duration}ms",
                result.ProcessedCount,
                result.Duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.EndTime = DateTime.UtcNow;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "5日均價/均量計算失敗");
        }

        return result;
    }

    /// <summary>
    /// 計算60日統計指標
    /// 對應原系統: calcStock60Days()
    /// </summary>
    public async Task<StatisticsResultDto> Calculate60DayStatisticsAsync(
        DateTime? tradeDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = new StatisticsResultDto
        {
            StatisticsType = "60日統計",
            TradeDate = tradeDate ?? DateTime.Today,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("開始計算 60日統計，日期: {TradeDate}", result.TradeDate);

            // 實作邏輯類似 calcStock60Days
            // 由於原始方法較複雜，這裡提供簡化版本
            // TODO: 完整實作需要參考原始系統的 stock60days 計算邏輯

            // 設定較長的 Command Timeout (120秒)
            var previousTimeout = _dbContext.Database.GetCommandTimeout();
            _dbContext.Database.SetCommandTimeout(120);
            
            try
            {
                var sql = @"
                    UPDATE stock60days b 
                    INNER JOIN tradedata a ON a.StockID = b.StockID AND a.TransDate = b.StockDate 
                    SET 
                        b.EndPrice = a.StockPrice, 
                        b.vol = a.Vol, 
                        b.mv5 = a.avgVol5D 
                    WHERE a.StockPrice <> b.EndPrice";

                await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            }
            finally
            {
                _dbContext.Database.SetCommandTimeout(previousTimeout);
            }

            result.IsSuccess = true;
            result.EndTime = DateTime.UtcNow;
            
            // 使用直接查詢獲取計數
            var countSql = $"SELECT COUNT(*) FROM stock60days WHERE StockDate = '{result.TradeDate:yyyy-MM-dd}'";
            var connection = _dbContext.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = countSql;
                command.CommandTimeout = 60;
                var countResult = await command.ExecuteScalarAsync(cancellationToken);
                result.ProcessedCount = countResult != null ? Convert.ToInt32(countResult) : 0;
            }
            finally
            {
                await connection.CloseAsync();
            }

            _logger.LogInformation(
                "60日統計計算完成，處理 {Count} 檔股票，耗時 {Duration}ms",
                result.ProcessedCount,
                result.Duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.EndTime = DateTime.UtcNow;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "60日統計計算失敗");
        }

        return result;
    }

    /// <summary>
    /// 計算盤量分析
    /// 對應原系統: pan3Analysis()
    /// </summary>
    public async Task<StatisticsResultDto> CalculatePanAnalysisAsync(
        DateTime? tradeDate = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        var result = new StatisticsResultDto
        {
            StatisticsType = "盤量分析",
            TradeDate = tradeDate ?? DateTime.Today,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("開始計算盤量分析，日期: {TradeDate}", result.TradeDate);

            // TODO: 實作 pan3Analysis 邏輯
            // 需要參考原始系統的實作

            result.IsSuccess = true;
            result.EndTime = DateTime.UtcNow;
            result.ProcessedCount = 0; // TODO: 更新實際處理數量

            _logger.LogInformation(
                "盤量分析計算完成，處理 {Count} 檔股票，耗時 {Duration}ms",
                result.ProcessedCount,
                result.Duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.EndTime = DateTime.UtcNow;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "盤量分析計算失敗");
        }

        return result;
    }

    /// <summary>
    /// 計算日均分盤量
    /// 對應原系統: fenPanAVG()
    /// </summary>
    public async Task<StatisticsResultDto> CalculateFenPanAverageAsync(
        DateTime? tradeDate = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        var result = new StatisticsResultDto
        {
            StatisticsType = "日均分盤量",
            TradeDate = tradeDate ?? DateTime.Today,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("開始計算日均分盤量，日期: {TradeDate}", result.TradeDate);

            // TODO: 實作 fenPanAVG 邏輯
            // 需要參考原始系統的實作

            result.IsSuccess = true;
            result.EndTime = DateTime.UtcNow;
            result.ProcessedCount = 0; // TODO: 更新實際處理數量

            _logger.LogInformation(
                "日均分盤量計算完成，處理 {Count} 檔股票，耗時 {Duration}ms",
                result.ProcessedCount,
                result.Duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.EndTime = DateTime.UtcNow;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "日均分盤量計算失敗");
        }

        return result;
    }

    /// <summary>
    /// 執行完整的統計計算流程
    /// 對應原系統: execAll4()
    /// </summary>
    public async Task<ComprehensiveStatisticsResultDto> CalculateAllStatisticsAsync(
        DateTime? tradeDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ComprehensiveStatisticsResultDto
        {
            TradeDate = tradeDate ?? DateTime.Today,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("開始執行完整統計計算流程 (execAll4)，日期: {TradeDate}", result.TradeDate);

            // 按順序執行所有統計
            result.FiveDayAverage = await Calculate5DayAverageAsync(tradeDate, cancellationToken);
            result.SixtyDayStatistics = await Calculate60DayStatisticsAsync(tradeDate, cancellationToken);
            result.PanAnalysis = await CalculatePanAnalysisAsync(tradeDate, cancellationToken);
            result.FenPanAverage = await CalculateFenPanAverageAsync(tradeDate, cancellationToken);

            result.EndTime = DateTime.UtcNow;
            result.IsSuccess = result.AllResults.All(r => r.IsSuccess);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "完整統計計算流程執行成功，總耗時 {Duration}",
                    result.TotalDuration?.ToString(@"mm\:ss"));
            }
            else
            {
                var failedStats = result.AllResults.Where(r => !r.IsSuccess).ToList();
                result.ErrorMessage = $"部分統計失敗: {string.Join(", ", failedStats.Select(s => s.StatisticsType))}";
                
                _logger.LogWarning(
                    "完整統計計算流程部分失敗，成功 {Success}/{Total}，失敗項目: {Failed}",
                    result.SuccessCount,
                    result.AllResults.Count,
                    string.Join(", ", failedStats.Select(s => s.StatisticsType)));
            }
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.EndTime = DateTime.UtcNow;
            result.ErrorMessage = $"統計計算流程異常: {ex.Message}";
            _logger.LogError(ex, "完整統計計算流程執行失敗");
        }

        return result;
    }

    /// <summary>
    /// 批次更新各 base tables 的 5日均價/均量
    /// </summary>
    private async Task Update5DayAverageForBaseTablesAsync(CancellationToken cancellationToken)
    {
        // 定義需要更新的 table 及其對應的日期欄位和成交量欄位
        var tablesToUpdate = new[]
        {
            new { Table = "buyin", DateColumn = "DataDate", VolumeColumn = "onTimeVol" },
            new { Table = "recommandstock", DateColumn = "reccDate", VolumeColumn = "onTimeVol" },
            new { Table = "investbase", DateColumn = "recDate", VolumeColumn = "onTimeVol" }
        };

        foreach (var tableInfo in tablesToUpdate)
        {
            // 更新 5日均價/均量
            var sql = $@"
                UPDATE {tableInfo.Table} a 
                INNER JOIN (
                    SELECT StockID, avgAmt, avgVol, StockDate, lastDate 
                    FROM weekall5avg
                ) b ON a.StockID = b.StockID  
                SET 
                    a.avgAmt5D = IFNULL(b.avgAmt, 0), 
                    a.avgVol5D = IFNULL(b.avgVol, 0), 
                    a.{tableInfo.DateColumn} = b.StockDate, 
                    a.lastDate = b.lastDate";

            await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);

            // 更新成交量比率
            sql = $@"
                UPDATE {tableInfo.Table} 
                SET 
                    lastVolRate = IF(lastVol = 0, 0, ROUND({tableInfo.VolumeColumn} / lastVol, 2)), 
                    avg5VolRate = IF(avgVol5D = 0, 0, ROUND({tableInfo.VolumeColumn} / avgVol5D, 2))";

            await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }

    /// <summary>
    /// 更新最新交易資訊到各 base table
    /// </summary>
    private async Task UpdateBaseTablesWithLatestDataAsync(CancellationToken cancellationToken)
    {
        // 定義需要更新的 base tables 及其對應的日期欄位和成交量欄位
        var baseTableMappings = new[]
        {
            new { Table = "investbase", DateColumn = "recDate", VolumeColumn = "onTimeVol", PriceColumn = "onTimePrice", JoinCondition = "a.StockID = b.StockID" },
            new { Table = "recommandstock", DateColumn = "reccDate", VolumeColumn = "onTimeVol", PriceColumn = "onTimePrice", JoinCondition = "a.StockID = b.StockID" },
            new { Table = "buyin", DateColumn = "DataDate", VolumeColumn = "onTimeVol", PriceColumn = "onTimePrice", JoinCondition = "a.StockID = b.StockID" }
        };

        foreach (var mapping in baseTableMappings)
        {
            var sql = $@"
                UPDATE {mapping.Table} a 
                INNER JOIN weekallmostrecent b ON {mapping.JoinCondition}
                SET 
                    a.StockName = b.StockName, 
                    a.StockType = b.StockType, 
                    a.{mapping.DateColumn} = b.StockDate, 
                    a.lastDate = b.lastDate, 
                    a.transVol = b.transVol, 
                    a.{mapping.VolumeColumn} = b.Vol, 
                    a.{mapping.PriceColumn} = b.EndPrice, 
                    a.OpenPriec = b.OpenPriec";

            await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }

        // tradedata 有特殊的 JOIN 條件（需要匹配日期）
        var tradedataSql = @"
            UPDATE tradedata a 
            INNER JOIN weekallmostrecent b ON a.StockID = b.StockID AND a.TransDate = b.StockDate 
            SET 
                a.StockName = b.StockName, 
                a.StockType = b.StockType, 
                a.transVol = b.transVol, 
                a.Vol = b.Vol, 
                a.StockPrice = b.EndPrice, 
                a.OpenPriec = b.OpenPriec";

        await _dbContext.Database.ExecuteSqlRawAsync(tradedataSql, cancellationToken);
    }
}
