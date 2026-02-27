using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Infrastructure.Repositories;

/// <summary>
/// TradeData 資料存取實作
/// </summary>
public class TradeDataRepository : ITradeDataRepository
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<TradeDataRepository> _logger;

    public TradeDataRepository(StockImportDbContext context, ILogger<TradeDataRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 新增或更新交易資料（UPSERT）
    /// </summary>
    public async Task UpsertAsync(TradeData tradeData, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("🔍 DEBUG: UpsertAsync called for {StockID} on {TransDate}", 
            tradeData.StockID, tradeData.TransDate);

        var existing = await _context.TradeData
            .FirstOrDefaultAsync(t => 
                t.StockID == tradeData.StockID && 
                t.TransDate == tradeData.TransDate);

        if (existing != null)
        {
            _logger.LogDebug("🔍 DEBUG: Updating existing tradedata record for {StockID}", tradeData.StockID);
            // 更新現有記錄 - 複製所有重要欄位
            existing.OpenPriec = tradeData.OpenPriec;
            existing.StockPrice = tradeData.StockPrice;
            existing.HPrice = tradeData.HPrice;
            existing.LPrice = tradeData.LPrice;
            existing.Vol = tradeData.Vol;
            existing.TransVol = tradeData.TransVol;

            _context.TradeData.Update(existing);
        }
        else
        {
            _logger.LogDebug("🔍 DEBUG: Adding new tradedata record for {StockID}", tradeData.StockID);
            // 新增記錄
            await _context.TradeData.AddAsync(tradeData);
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("🔍 DEBUG: SaveChangesAsync completed for tradedata {StockID}", tradeData.StockID);
    }

    /// <summary>
    /// 批次新增或更新（使用 EF Core 的批次處理）
    /// </summary>
    public async Task UpsertBatchAsync(IEnumerable<TradeData> tradeDataList, CancellationToken cancellationToken = default)
    {
        var dataList = tradeDataList.ToList();
        if (!dataList.Any())
            return;

        // 分批處理（每批 1000 筆）
        var batchSize = 1000;
        for (int i = 0; i < dataList.Count; i += batchSize)
        {
            var batch = dataList.Skip(i).Take(batchSize).ToList();

            // 取得該批次的所有股票代碼和日期組合（先載入到記憶體以支援 client evaluation）
            var stockIds = batch.Select(t => t.StockID).Distinct().ToList();
            var dates = batch.Select(t => t.TransDate).Distinct().ToList();
            
            // 查詢所有可能的現有記錄（使用可翻譯的 Contains 查詢）
            var existingData = await _context.TradeData
                .Where(t => stockIds.Contains(t.StockID) && dates.Contains(t.TransDate))
                .ToListAsync(cancellationToken);

            var existingDict = existingData.ToDictionary(t => $"{t.StockID}_{t.TransDate:yyyyMMdd}");

            foreach (var tradeData in batch)
            {
                var key = $"{tradeData.StockID}_{tradeData.TransDate:yyyyMMdd}";
                
                if (existingDict.TryGetValue(key, out var existing))
                {
                    // 更新
                    existing.OpenPriec = tradeData.OpenPriec;
                    existing.StockPrice = tradeData.StockPrice;
                    existing.HPrice = tradeData.HPrice;
                    existing.LPrice = tradeData.LPrice;
                    existing.Vol = tradeData.Vol;
                    existing.TransVol = tradeData.TransVol;
                    
                    _context.TradeData.Update(existing);
                }
                else
                {
                    // 新增
                    _context.TradeData.Add(tradeData);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 根據股票代碼和日期範圍查詢
    /// </summary>
    public async Task<List<TradeData>> GetByStockCodeAsync(
        string stockCode, 
        DateTime startDate, 
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.TradeData
            .Where(t => 
                t.StockID == stockCode && 
                t.TransDate >= startDate && 
                t.TransDate <= endDate)
            .OrderBy(t => t.TransDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查詢指定日期是否已有數據
    /// </summary>
    public async Task<int> CountByDateAsync(
        DateTime tradeDate,
        string? market = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TradeData.Where(t => t.TransDate == tradeDate);
        
        // 注意：TradeData 沒有 Market 欄位，market 參數目前未使用
        // 如需按市場篩選，應使用 StockType 欄位
        
        return await query.CountAsync(cancellationToken);
    }

    /// <summary>
    /// 查詢指定股票代號和日期的交易數據
    /// </summary>
    public async Task<TradeData?> GetByStockIdAndDateAsync(
        string stockId,
        DateTime transDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.TradeData
            .FirstOrDefaultAsync(t => 
                t.StockID == stockId && 
                t.TransDate == transDate,
                cancellationToken);
    }

    /// <summary>
    /// 更新交易數據
    /// </summary>
    public async Task UpdateAsync(TradeData tradeData, CancellationToken cancellationToken = default)
    {
        _context.TradeData.Update(tradeData);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 刪除指定股票代碼和日期的資料
    /// </summary>
    public async Task<bool> DeleteAsync(string stockCode, DateTime tradeDate, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TradeData
            .FirstOrDefaultAsync(t => 
                t.StockID == stockCode && 
                t.TransDate == tradeDate);

        if (entity == null)
            return false;

        _context.TradeData.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// 取得最新的交易日期（SELECT MAX(TransDate) FROM TradeData）
    /// </summary>
    public async Task<DateTime?> GetMaxTransDateAsync(CancellationToken cancellationToken = default)
    {
        return await _context.TradeData
            .MaxAsync(t => (DateTime?)t.TransDate, cancellationToken);
    }

    /// <summary>
    /// 獲取最新交易日期 (從 weekall 資料表)
    /// </summary>
    public async Task<DateTime> GetLatestTradingDateAsync()
    {
        var latestDate = await _context.WeekAll
            .OrderByDescending(w => w.StockDate)
            .Select(w => w.StockDate)
            .FirstOrDefaultAsync();
        
        return latestDate != default ? latestDate : DateTime.Today.AddDays(-1);
    }

    /// <summary>
    /// 獲取最新的 InvestBase 記錄（依 RecDate 降序排列取第一筆）
    /// 使用原始 SQL 避免 EF Core 查詢時缺少欄位的問題
    /// </summary>
    public async Task<InvestBase?> GetLatestInvestBaseAsync()
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT StockID, stockName, StockType, recDate, lastDate 
                FROM investbase 
                ORDER BY recDate DESC 
                LIMIT 1";
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new InvestBase
                {
                    StockID = reader.GetString(0),
                    StockName = reader.IsDBNull(1) ? null : reader.GetString(1),
                    StockType = reader.IsDBNull(2) ? null : reader.GetString(2),
                    RecDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                    LastDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                };
            }
            return null;
        }
        catch
        {
            return await _context.InvestBase
                .OrderByDescending(i => i.RecDate)
                .Select(i => new InvestBase 
                { 
                    StockID = i.StockID,
                    StockName = i.StockName,
                    StockType = i.StockType,
                    RecDate = i.RecDate,
                    LastDate = i.LastDate
                })
                .FirstOrDefaultAsync();
        }
    }
}
