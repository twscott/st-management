using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;
using SST.StockImport.Services.Scrapers;

namespace SST.StockImport.Services;

/// <summary>
/// 股票資料匯入服務實作（簡化版本）
/// 數據流：交易所 CSV → weekall（原始數據）→ tradedata（分析數據）
/// </summary>
public class ImportService : IImportService
{
    private readonly ILogger<ImportService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITradeDataRepository _tradeDataRepository;
    private readonly TWSEScraper _twseScraper;

    public ImportService(
        ILogger<ImportService> logger,
        IServiceScopeFactory scopeFactory,
        ITradeDataRepository tradeDataRepository,
        TWSEScraper twseScraper)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _tradeDataRepository = tradeDataRepository;
        _twseScraper = twseScraper;
    }

    /// <summary>
    /// 執行股票數據匯入（主要方法 - 批量處理版本）
    /// </summary>
    public async Task<ImportResultDto> ImportStockDataAsync(
        ImportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;
        
        var result = new ImportResultDto
        {
            JobId = jobId,
            StartTime = startTime,
            IsSuccess = false
        };

        try
        {
            // 確定交易日期
            var tradeDate = request.TradeDate ?? DateTime.Today;

            _logger.LogInformation(
                "📥 開始匯入 - JobId: {JobId}, 市場: {Market}, 日期: {TradeDate}",
                jobId, request.Market, tradeDate);

            // ✅ 步驟 0: 檢查數據是否已存在（無論是否存在都會重新下載並更新）
            using var checkScope = _scopeFactory.CreateScope();
            var checkContext = checkScope.ServiceProvider.GetRequiredService<StockImportDbContext>();
            var existingCount = await checkContext.WeekAll
                .Where(w => w.StockDate == tradeDate)
                .CountAsync(cancellationToken);
            
            if (existingCount > 0)
            {
                _logger.LogWarning(
                    "⚠️ 數據已存在：{Date} 已有 {Count} 筆記錄，將重新下載並更新交易所欄位（ON DUPLICATE KEY UPDATE）",
                    tradeDate.ToString("yyyy-MM-dd"), existingCount);
            }
            else
            {
                _logger.LogInformation("✨ 全新下載：{Date} 無現有數據，將執行完整匯入", 
                    tradeDate.ToString("yyyy-MM-dd"));
            }

            // 步驟 1: 批次爬取資料（下載所有 TSE/OTC/EMERGING 的 CSV 數據）
            // TWSEScraper.ParseTseCsv 內部已過濾只保留 4 位數股票代碼
            _logger.LogWarning("====================================================");
            _logger.LogWarning("🌐 開始從交易所 API 下載數據...");
            _logger.LogWarning("🔧 _twseScraper 類型: {Type}", _twseScraper.GetType().FullName);
            _logger.LogWarning("====================================================");
            
            var scrapedData = await _twseScraper.ScrapeBatchAsync(
                Enumerable.Empty<string>(),  // 傳空列表，下載所有交易所完整資料
                tradeDate, 
                maxDegreeOfParallelism: 5,
                cancellationToken);

            _logger.LogWarning("📊 === 下载结果汇总 ===");
            _logger.LogWarning("📊 總共下載 {Count} 筆股票記錄", scrapedData.Count);

            // 按市場分類統計
            var tseCount = scrapedData.Count(s => s.Market == "TSE");
            var otcCount = scrapedData.Count(s => s.Market == "OTC");
            var emergingCount = scrapedData.Count(s => s.Market == "EMERGING");
            _logger.LogWarning("📊 [TSE] 上市: {Count} 筆", tseCount);
            _logger.LogWarning("📊 [OTC] 上櫃: {Count} 筆", otcCount);
            _logger.LogWarning("📊 [EMERGING] 興櫃: {Count} 筆", emergingCount);

            // ⚠️ 檢查下載結果的合理性
            if (!scrapedData.Any())
            {
                result.ErrorMessage = $"❌ API 調用失敗：未取得任何股票資料 (日期: {tradeDate:yyyy-MM-dd})";
                result.IsSuccess = false;
                result.EndTime = DateTime.UtcNow;
                _logger.LogError(result.ErrorMessage);
                return result;
            }

            // 檢查下載數量是否異常少（正常應該 2000+ 筆）
            if (scrapedData.Count < 1000)
            {
                _logger.LogWarning(
                    "⚠️ 下載數量異常：僅 {Count} 筆（正常應 2000+ 筆），可能部分 API 失敗",
                    scrapedData.Count);
                
                if (tseCount == 0) _logger.LogWarning("⚠️ TSE 數據為空！");
                if (otcCount == 0) _logger.LogWarning("⚠️ OTC 數據為空！");
                if (emergingCount == 0) _logger.LogWarning("⚠️ EMERGING 數據為空！");
            }

            result.TotalCount = scrapedData.Count;

            // 步驟 2: 批量寫入數據庫（使用單一 scope）
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<StockImportDbContext>();

            // 使用MySQL重試策略（支持自動重試事務）
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    // 步驟 2.1: 批量寫入 weekall（原始交易數據，直接使用 CSV/JSON 中的股票名稱）
                    var weekallInserted = await BatchInsertWeekAllAsync(
                        dbContext, scrapedData, tradeDate, cancellationToken);

                    _logger.LogInformation("weekall 表已寫入/更新 {Count} 筆記錄", weekallInserted);

                    // 步驟 2.2: 批量寫入 tradedata（分析统計數據）
                    var tradedataInserted = await BatchInsertTradeDataAsync(
                        dbContext, scrapedData, tradeDate, cancellationToken);

                    _logger.LogInformation("tradedata 表已寫入/更新 {Count} 筆記錄", tradedataInserted);

                    // ⚙️ 对于 SQLite，批量方法不调用 SaveChanges，需要在此统一保存
                    //    对于 MySQL，批量方法使用原始 SQL已直接写入，此处 SaveChanges 无影响
                    await dbContext.SaveChangesAsync(cancellationToken);

                    // 步驟 3: 更新 stockid 表（股票主資料表）- 在提交事务前执行
                    var stockidUpdated = await UpdateStockIdTableAsync(
                        dbContext, scrapedData, cancellationToken);

                    _logger.LogInformation("stockid 表已更新 {Count} 筆記錄", stockidUpdated);

                    // 提交事務（确保所有操作都在事务内）
                    await transaction.CommitAsync(cancellationToken);

                    // ✅ lastDate 已在 INSERT 时从 investbase.MAX(lastDate) 获取，无需额外更新

                    // 統計結果
                    result.SuccessCount = Math.Min(weekallInserted, tradedataInserted);
                    result.FailedCount = result.TotalCount - result.SuccessCount;
                    result.EndTime = DateTime.UtcNow;
                    result.IsSuccess = result.SuccessCount > 0;
                
                    // 根據是否已存在數據，提供不同的消息
                    if (existingCount > 0)
                    {
                        result.ErrorMessage = $"✅ 數據已存在，執行更新操作：{result.SuccessCount} 筆 (原有 {existingCount} 筆)";
                        _logger.LogInformation(
                            "✅ 更新完成 - JobId: {JobId}, 更新: {Success}/{Total}, 耗時: {Duration}秒",
                            jobId, result.SuccessCount, result.TotalCount, result.DurationSeconds);
                    }
                    else
                    {
                        result.ErrorMessage = $"✅ 全新下載成功：{result.SuccessCount} 筆";
                        _logger.LogInformation(
                            "✅ 下載完成 - JobId: {JobId}, 成功: {Success}/{Total}, 耗時: {Duration}秒",
                            jobId, result.SuccessCount, result.TotalCount, result.DurationSeconds);
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    
                    result.EndTime = DateTime.UtcNow;
                    result.IsSuccess = false;
                    result.ErrorMessage = $"資料庫操作失敗: {ex.Message}";
                
                    _logger.LogError(ex, "匯入失敗 (資料庫操作) - JobId: {JobId}", jobId);
                    throw;
                }
            });  // ExecuteAsync 结束
       }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.IsSuccess = false;
            result.ErrorMessage = $"匯入異常: {ex.Message}";
            
            _logger.LogError(ex, "匯入失敗 - JobId: {JobId}", jobId);
        }

        return result;
    }

    /// <summary>
    /// 批量查詢 stockid 表，獲取股票名稱和類型
    /// </summary>
    private async Task<Dictionary<string, (string Name, string SType)>> GetStockInfoBatchAsync(
        StockImportDbContext dbContext,
        List<string> stockCodes,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, (string, string)>();

        if (!stockCodes.Any())
            return result;

        try
        {
            // 使用 ADO.NET 直接查询（避免 SqlQueryRaw 的映射问题）
            var connection = dbContext.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            
            if (!wasOpen)
                await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            var codeList = string.Join(",", stockCodes.Select(c => $"'{c}'"));
            command.CommandText = $"SELECT id, name, stype FROM stockid WHERE id IN ({codeList})";

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var stockCode = reader.GetString(0);
                var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var stype = reader.IsDBNull(2) ? "" : reader.GetString(2);
                result[stockCode] = (name, stype);
            }

            if (!wasOpen)
                await connection.CloseAsync();

            _logger.LogInformation("從 stockid 表查詢到 {Count} 筆股票基本資訊（請求 {Requested} 筆）", 
                result.Count, stockCodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "批量查詢 stockid 表失敗，將使用交易所市場類型作為備用");
        }

        return result;
    }

    /// <summary>
    /// 批量插入 weekall 表（原始交易數據）
    /// </summary>
    private async Task<int> BatchInsertWeekAllAsync(
        StockImportDbContext dbContext,
        List<StockDataDto> scrapedData,
        DateTime tradeDate,
        CancellationToken cancellationToken)
    {
        if (!scrapedData.Any())
            return 0;

        _logger.LogInformation("開始批量插入 weekall - 共 {Count} 筆資料", 
            scrapedData.Count);

        // 检测数据库类型
        var providerName = dbContext.Database.ProviderName;
        var isSqlite = providerName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ?? false;

        if (isSqlite)
        {
            // SQLite: 使用 ON CONFLICT DO UPDATE (需要 SQLite 3.24.0+)
            return await BatchInsertWeekAllSqlite(dbContext, scrapedData, tradeDate, cancellationToken);
        }
        else
        {
            // MySQL: 使用 ON DUPLICATE KEY UPDATE
            return await BatchInsertWeekAllMySql(dbContext, scrapedData, tradeDate, cancellationToken);
        }
    }

    /// <summary>
    /// MySQL 批量插入 (ON DUPLICATE KEY UPDATE)
    /// </summary>
    private async Task<int> BatchInsertWeekAllMySql(
        StockImportDbContext dbContext,
        List<StockDataDto> scrapedData,
        DateTime tradeDate,
        CancellationToken cancellationToken)
    {
        // ⚡ 先查询一次 lastDate，避免在 VALUES 中重复执行 2300 次子查询
        var lastDate = await dbContext.InvestBase
            .MaxAsync(i => (DateTime?)i.LastDate, cancellationToken);
        var lastDateStr = lastDate.HasValue ? $"'{lastDate.Value:yyyy-MM-dd}'" : "NULL";

        // 構建批量 INSERT ON DUPLICATE KEY UPDATE SQL
        var values = new List<string>();

        foreach (var data in scrapedData)
        {
            // 直接使用 CSV/JSON 中的股票名稱和市場類型
            var stockName = data.StockName;
            var stockType = MapMarketToStockType(data.Market) ?? "";

            // 轉義單引號
            stockName = stockName.Replace("'", "''");
            stockType = stockType.Replace("'", "''");

            // weekall 欄位：直接使用查询好的 lastDate 值
            var value = $"('{data.StockCode}', '{stockName}', '{stockType}', " +
                       $"'{tradeDate:yyyy-MM-dd}', {lastDateStr}, " +
                        $"{data.OpenPrice}, {data.ClosePrice}, " +
                        $"{data.HighPrice}, {data.LowPrice}, {data.Volume}, {data.TradeCount ?? 0})";
            values.Add(value);
        }

        // MySQL 8.0 使用传统的 VALUES() 函数（兼容性最好）
        var sql = @"
            INSERT INTO weekall (StockID, StockName, StockType, StockDate, lastDate, OpenPriec, EndPrice, HPrice, LPrice, Vol, transVol)
            VALUES " + string.Join(",\n", values) + @"
            ON DUPLICATE KEY UPDATE
                StockName = VALUES(StockName),
                StockType = VALUES(StockType),
                lastDate = VALUES(lastDate),
                OpenPriec = VALUES(OpenPriec),
                EndPrice = VALUES(EndPrice),
                HPrice = VALUES(HPrice),
                LPrice = VALUES(LPrice),
                Vol = VALUES(Vol),
                transVol = VALUES(transVol)";

        var affectedRows = await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        
        _logger.LogInformation("⚡ weekall 批量插入：{Count} 笔，lastDate={LastDate}，影响 {Rows} 行", 
            scrapedData.Count, lastDateStr, affectedRows);
        
        return scrapedData.Count;
    }

    /// <summary>
    /// SQLite 批量插入 (使用 EF Core Add/Update 逻辑)
    /// 注意：不调用 SaveChanges，由外部事务统一管理
    /// </summary>
    private async Task<int> BatchInsertWeekAllSqlite(
        StockImportDbContext dbContext,
        List<StockDataDto> scrapedData,
        DateTime tradeDate,
        CancellationToken cancellationToken)
    {
        // SQLite: 使用 EF Core 原生 Add/Update 逻辑
        int totalInserted = 0;

        // 从 investbase 获取标准 lastDate
        var lastDate = await dbContext.InvestBase
            .MaxAsync(i => (DateTime?)i.LastDate, cancellationToken);

        foreach (var data in scrapedData)
        {
            var stockName = data.StockName?.Replace("'", "''") ?? "";
            var stockType = MapMarketToStockType(data.Market)?.Replace("'", "''") ?? "";

            // 检查是否已存在
            var existing = await dbContext.WeekAll
                .FirstOrDefaultAsync(w => w.StockID == data.StockCode && w.StockDate == tradeDate, 
                                     cancellationToken);

            if (existing != null)
            {
                // 更新现有记录
                existing.StockName = stockName;
                existing.StockType = stockType;
                existing.LastDate = lastDate;
                existing.OpenPriec = data.OpenPrice;
                existing.EndPrice = data.ClosePrice;
                existing.HPrice = data.HighPrice;
                existing.LPrice = data.LowPrice;
                existing.Vol = data.Volume;
                existing.TransVol = data.TradeCount ?? 0;
            }
            else
            {
                // 插入新记录
                var weekAll = new WeekAll
                {
                    StockID = data.StockCode,
                    StockName = stockName,
                    StockType = stockType,
                    StockDate = tradeDate,
                    LastDate = lastDate,
                    OpenPriec = data.OpenPrice,
                    EndPrice = data.ClosePrice,
                    HPrice = data.HighPrice,
                    LPrice = data.LowPrice,
                    Vol = data.Volume,
                    TransVol = data.TradeCount ?? 0
                };
                await dbContext.WeekAll.AddAsync(weekAll, cancellationToken);
            }
            
            totalInserted++;
        }

        // ⚠️ 不调用 SaveChangesAsync - 由外部事务统一管理
        _logger.LogDebug("weekall 批量插入 (SQLite)：{Count} 筆資料準備完成", totalInserted);
        
        return totalInserted;
    }

    /// <summary>
    /// 批量插入 tradedata 表（分析統計數據）
    /// </summary>
    private async Task<int> BatchInsertTradeDataAsync(
        StockImportDbContext dbContext,
        List<StockDataDto> scrapedData,
        DateTime tradeDate,
        CancellationToken cancellationToken)
    {
        if (!scrapedData.Any())
            return 0;

        // 检测数据库类型
        var providerName = dbContext.Database.ProviderName;
        var isSqlite = providerName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ?? false;

        if (isSqlite)
        {
            // SQLite: 使用 EF Core Add/Update
            return await BatchInsertTradeDataSqlite(dbContext, scrapedData, tradeDate, cancellationToken);
        }
        else
        {
            // MySQL: 使用 ON DUPLICATE KEY UPDATE
            return await BatchInsertTradeDataMySql(dbContext, scrapedData, tradeDate, cancellationToken);
        }
    }

    /// <summary>
    /// MySQL 批量插入 tradedata (ON DUPLICATE KEY UPDATE)
    /// </summary>
    private async Task<int> BatchInsertTradeDataMySql(
        StockImportDbContext dbContext,
        List<StockDataDto> scrapedData,
        DateTime tradeDate,
        CancellationToken cancellationToken)
    {
        // ⚡ 先查询一次 lastDate，避免在 VALUES 中重复执行 2300 次子查询
        var lastDate = await dbContext.InvestBase
            .MaxAsync(i => (DateTime?)i.LastDate, cancellationToken);
        var lastDateStr = lastDate.HasValue ? $"'{lastDate.Value:yyyy-MM-dd}'" : "NULL";

        // 構建批量 INSERT ON DUPLICATE KEY UPDATE SQL
        var values = new List<string>();

        foreach (var data in scrapedData)
        {
            var stockName = data.StockName.Replace("'", "''");
            var stockType = (MapMarketToStockType(data.Market) ?? "").Replace("'", "''");

            // tradedata 欄位：直接使用查询好的 lastDate 值
            var value = $"('{data.StockCode}', '{stockName}', '{stockType}', " +
                       $"'{tradeDate:yyyy-MM-dd}', {lastDateStr}, " +
                       $"{data.OpenPrice}, {data.ClosePrice}, " +
                       $"{data.HighPrice}, {data.LowPrice}, {data.Volume}, {data.TradeCount ?? 0})";
            values.Add(value);
        }

        // MySQL 8.0 使用传统的 VALUES() 函数（兼容性最好）
        var sql = @"
            INSERT INTO tradedata (StockID, StockName, StockType, TransDate, lastDate, OpenPriec, StockPrice, HPrice, LPrice, Vol, TransVol)
            VALUES " + string.Join(",\n", values) + @"
            ON DUPLICATE KEY UPDATE
                StockName = VALUES(StockName),
                StockType = VALUES(StockType),
                lastDate = VALUES(lastDate),
                OpenPriec = VALUES(OpenPriec),
                StockPrice = VALUES(StockPrice),
                HPrice = VALUES(HPrice),
                LPrice = VALUES(LPrice),
                Vol = VALUES(Vol),
                TransVol = VALUES(TransVol)";

        var affectedRows = await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        
        _logger.LogDebug("tradedata 批量插入 (MySQL)：{Count} 筆資料，影響 {Rows} 行", scrapedData.Count, affectedRows);
        
        return scrapedData.Count;
    }

    /// <summary>
    /// SQLite 批量插入 tradedata (使用 EF Core Add/Update)
    /// 注意：不调用 SaveChanges，由外部事务统一管理
    /// </summary>
    private async Task<int> BatchInsertTradeDataSqlite(
        StockImportDbContext dbContext,
        List<StockDataDto> scrapedData,
        DateTime tradeDate,
        CancellationToken cancellationToken)
    {
        int totalInserted = 0;

        // 从 investbase 获取标准 lastDate
        var lastDate = await dbContext.InvestBase
            .MaxAsync(i => (DateTime?)i.LastDate, cancellationToken);

        foreach (var data in scrapedData)
        {
            var stockName = data.StockName ?? "";
            var stockType = MapMarketToStockType(data.Market) ?? "";

            // 检查是否已存在 (tradedata 有唯一索引: StockID + TransDate)
            var existing = await dbContext.TradeData
                .FirstOrDefaultAsync(t => t.StockID == data.StockCode && t.TransDate == tradeDate, 
                                     cancellationToken);

            if (existing != null)
            {
                // 更新现有记录
                existing.StockName = stockName;
                existing.StockType = stockType;                existing.LastDate = lastDate;                existing.OpenPriec = data.OpenPrice;
                existing.StockPrice = data.ClosePrice;  // tradedata 使用 StockPrice (注意與 weekall.EndPrice 不同)
                existing.HPrice = data.HighPrice;
                existing.LPrice = data.LowPrice;
                existing.Vol = data.Volume;
                existing.TransVol = data.TradeCount ?? 0;
            }
            else
            {
                // 插入新记录
                var tradeData = new TradeData
                {
                    StockID = data.StockCode,
                    StockName = stockName,
                    StockType = stockType,
                    TransDate = tradeDate,
                    LastDate = lastDate,
                    OpenPriec = data.OpenPrice,
                    StockPrice = data.ClosePrice,  // tradedata 使用 StockPrice
                    HPrice = data.HighPrice,
                    LPrice = data.LowPrice,
                    Vol = data.Volume,
                    TransVol = data.TradeCount ?? 0
                };
                await dbContext.TradeData.AddAsync(tradeData, cancellationToken);
            }
            
            totalInserted++;
        }

        // ⚠️ 不调用 SaveChangesAsync - 由外部事务统一管理
        _logger.LogDebug("tradedata 批量插入 (SQLite)：{Count} 筆資料準備完成", totalInserted);
        
        return totalInserted;
    }

    /// <summary>
    /// 映射交易所市場類型到股票類型
    /// </summary>
    private string? MapMarketToStockType(string? market)
    {
        return market switch
        {
            "TSE" => "上市",
            "OTC" => "上櫃",
            "EMERGING" => "興櫃",
            _ => null
        };
    }

    /// <summary>
    /// 批量更新 stockid 表（股票主資料表）
    /// 使用 INSERT ... ON DUPLICATE KEY UPDATE 語法同時處理新增和更新
    /// </summary>
    private async Task<int> UpdateStockIdTableAsync(
        StockImportDbContext dbContext,
        List<StockDataDto> scrapedData,
        CancellationToken cancellationToken)
    {
        if (!scrapedData.Any())
            return 0;

        // 建立批次 INSERT ... ON DUPLICATE KEY UPDATE 語句
        var values = new List<string>();
        
        foreach (var data in scrapedData)
        {
            var stockType = MapMarketToStockType(data.Market);
            if (string.IsNullOrEmpty(stockType))
                continue;

            // 逃脫特殊字元避免 SQL 注入
            var escapedCode = data.StockCode.Replace("'", "''");
            var escapedName = (data.StockName ?? "").Replace("'", "''");
            var escapedType = stockType.Replace("'", "''");

            values.Add($"('{escapedCode}', '{escapedName}', '{escapedType}', 0)");
        }

        if (!values.Any())
            return 0;

        // 批次大小：每 500 筆執行一次（避免 SQL 語句過大）
        const int batchSize = 500;
        var totalAffected = 0;

        for (int i = 0; i < values.Count; i += batchSize)
        {
            var batch = values.Skip(i).Take(batchSize);
            var valuesClause = string.Join(",\n            ", batch);

            // MySQL 8.0 使用传统的 VALUES() 函数（兼容性最好）
            var sql = $@"
                INSERT INTO stockid (id, name, stype, issueCnt)
                VALUES
                {valuesClause}
                ON DUPLICATE KEY UPDATE
                    name = VALUES(name),
                    stype = VALUES(stype)";

            var affectedRows = await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            totalAffected += affectedRows;
        }

        _logger.LogDebug("stockid 批量更新：{Count} 筆資料，影響 {Rows} 行", scrapedData.Count, totalAffected);
        
        return scrapedData.Count;
    }

    /// <summary>
    /// 重試失敗的股票匯入（未實作）
    /// </summary>
    public Task<ImportResultDto> RetryFailedStocksAsync(
        string? jobId,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("RetryFailedStocksAsync 功能待實作");
    }

    /// <summary>
    /// 取得匯入任務狀態（未實作）
    /// </summary>
    public Task<ImportResultDto?> GetImportStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetImportStatusAsync 功能待實作");
    }

    /// <summary>
    /// 執行兩階段匯入（未實作）
    /// </summary>
    public Task<TwoPhaseImportResultDto> ExecuteTwoPhaseImportAsync(
        ImportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("ExecuteTwoPhaseImportAsync 功能待實作");
    }

    /// <summary>
    /// 執行三階段完整匯入流程（未實作）
    /// </summary>
    public Task<ThreePhaseImportResultDto> ExecuteThreePhaseCompleteImportAsync(
        DateTime tradeDate,
        bool includeGoodInfo = false,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("ExecuteThreePhaseCompleteImportAsync 功能待實作");
    }

    /// <summary>
    /// 獲取最新交易日期 (從 weekall 資料表)
    /// </summary>
    public async Task<DateTime> GetLatestTradingDateAsync()
    {
        return await _tradeDataRepository.GetLatestTradingDateAsync();
    }

    /// <summary>
    /// 獲取下載目標日期（基於 investbase 券商數據）
    /// 規則：
    /// 1. 時間 >= 15:00 → 下載 investbase.RecDate（當天收盤後的數據）
    /// 2. 時間 < 15:00 → 下載 investbase.LastDate（上一個交易日數據）
    /// 3. 若 investbase 無資料 → 使用昨天（fallback）
    /// 
    /// 說明：從 InvestBase 取得 RecDate，這就是要下載的交易日期
    /// 流程：
    /// 1. 到 InvestBase 找 RECDATE - 這就是需要下載的交易日期
    /// 2. 到交易所網站下載這個日期的資料 CSV
    /// 3. Parse CSV 檔，Insert/Update 到 weekall 和 tradedata
    /// </summary>
    public async Task<DateTime> GetDownloadTargetDateAsync()
    {
        // Fallback：若無資料則使用昨天
        DateTime fallbackDate = DateTime.Today.AddDays(-1);
        
        // 查詢 investbase 的 RecDate
        var latestInvestBase = await _tradeDataRepository.GetLatestInvestBaseAsync();
        
        if (latestInvestBase?.RecDate != null)
        {
            _logger.LogWarning("📅 使用 investbase.RecDate = {Date}", latestInvestBase.RecDate);
            return latestInvestBase.RecDate.Value;
        }
        
        _logger.LogWarning("📅 investbase 無 RecDate，使用 fallback：昨天 {Date}", fallbackDate);
        return fallbackDate;
    }

    /// <summary>
    /// 更新 weekall.lastDate 和 tradedata.lastDate 參考 investbase.lastDate
    /// 這樣可以追蹤每筆資料的前一個交易日
    /// </summary>
    private async Task UpdateLastDateFromInvestBaseAsync(DateTime tradeDate, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<StockImportDbContext>();

            // 從 investbase 更新 weekall.lastDate
            var weekallSql = @"
                UPDATE weekall w
                INNER JOIN investbase i ON w.StockID = i.StockID
                SET w.lastDate = i.lastDate
                WHERE w.StockDate = @tradeDate AND i.lastDate IS NOT NULL";

            var weekallAffected = await dbContext.Database.ExecuteSqlRawAsync(
                weekallSql,
                new MySqlParameter("@tradeDate", tradeDate),
                cancellationToken);

            // 從 investbase 更新 tradedata.lastDate
            var tradedataSql = @"
                UPDATE tradedata t
                INNER JOIN investbase i ON t.StockID = i.StockID
                SET t.lastDate = i.lastDate
                WHERE t.TransDate = @tradeDate AND i.lastDate IS NOT NULL";

            var tradedataAffected = await dbContext.Database.ExecuteSqlRawAsync(
                tradedataSql,
                new MySqlParameter("@tradeDate", tradeDate),
                cancellationToken);

            _logger.LogInformation(
                "📅 已更新 lastDate - weekall: {WeekallCount} 筆, tradedata: {TradedataCount} 筆",
                weekallAffected, tradedataAffected);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ 更新 lastDate 失敗，不影響主要功能");
        }
    }

    /// <summary>
    /// 刪除指定日期的交易資料
    /// </summary>
    public async Task<int> DeleteTradingDataAsync(DateTime date)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StockImportDbContext>();

        var weekallDeleted = await dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM weekall WHERE StockDate = {0}", date);

        var tradedataDeleted = await dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM tradedata WHERE TransDate = {0}", date);

        _logger.LogWarning("🗑️ 刪除 {Date} 資料 - weekall: {WeekallCount} 筆, tradedata: {TradedataCount} 筆",
            date.ToString("yyyy-MM-dd"), weekallDeleted, tradedataDeleted);

        return weekallDeleted + tradedataDeleted;
    }
}
