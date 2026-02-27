using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Services;

public class DateDataService : IDateDataService
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<DateDataService> _logger;

    public DateDataService(StockImportDbContext context, ILogger<DateDataService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DateRangeDto> GetAvailableDateRangeAsync()
    {
        var latestDate = await _context.WeekAll
            .MaxAsync(w => (DateTime?)w.StockDate) ?? DateTime.Now;
        
        var earliestDate = await _context.WeekAll
            .MinAsync(w => (DateTime?)w.StockDate) ?? DateTime.Now.AddYears(-1);

        return new DateRangeDto
        {
            EarliestDate = earliestDate,
            LatestDate = latestDate
        };
    }

    public async Task<DateDataSummaryDto> GetDateDataSummaryAsync(DateTime date)
    {
        var result = new DateDataSummaryDto
        {
            SelectedDate = date,
            LastTradingDate = await GetLastTradingDateAsync(date),
            TableCounts = new Dictionary<string, TableCountDto>()
        };

        if (result.LastTradingDate.HasValue)
        {
            var lastDate = result.LastTradingDate.Value;

            result.TableCounts["weekall"] = await GetTableCountAsync("weekall", "StockDate", date, lastDate);
            result.TableCounts["tradedata"] = await GetTableCountAsync("tradedata", "TransDate", date, lastDate);
            result.TableCounts["alertlist"] = await GetAlertListCountAsync(date, lastDate);
            result.TableCounts["alertlog"] = await GetAlertLogCountAsync(date, lastDate);
            result.TableCounts["dapan"] = await GetDapanCountAsync(date, lastDate);
            result.TableCounts["detector"] = await GetDetectorCountAsync(date, lastDate);
            result.TableCounts["investbase"] = await GetInvestBaseCountAsync(date, lastDate);
            result.TableCounts["stock60days"] = await GetStock60DaysCountAsync(date, lastDate);

            result.DapanData = await GetDapanDataAsync(date, lastDate);

            result.WeekallStats = await GetStockTypeStatsAsync("weekall", "StockDate", date, lastDate);
            result.TradedataStats = await GetStockTypeStatsAsync("tradedata", "TransDate", date, lastDate);
        }

        return result;
    }

    private async Task<DateTime?> GetLastTradingDateAsync(DateTime date)
    {
        var sql = $"SELECT MAX(StockDate) as LastDate FROM weekall WHERE StockDate < '{date:yyyy-MM-dd}'";
        var connection = _context.Database.GetDbConnection();
        await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        
        var result = await command.ExecuteScalarAsync();
        return result as DateTime?;
    }

    private async Task<TableCountDto> GetTableCountAsync(string tableName, string dateColumn, DateTime today, DateTime yesterday)
    {
        var todaySql = $"SELECT COUNT(*) FROM {tableName} WHERE {dateColumn} = '{today:yyyy-MM-dd}'";
        var yesterdaySql = $"SELECT COUNT(*) FROM {tableName} WHERE {dateColumn} = '{yesterday:yyyy-MM-dd}'";
        
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        
        command.CommandText = todaySql;
        var todayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
        
        command.CommandText = yesterdaySql;
        var yesterdayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

        return new TableCountDto { Today = todayCount, Yesterday = yesterdayCount };
    }

    private async Task<TableCountDto> GetAlertListCountAsync(DateTime today, DateTime yesterday)
    {
        return await GetTableCountAsync("alertlist", "alertDate", today, yesterday);
    }

    private async Task<TableCountDto> GetAlertLogCountAsync(DateTime today, DateTime yesterday)
    {
        var todaySql = $"SELECT COUNT(*) FROM alertlog WHERE DATE(CREATED) = '{today:yyyy-MM-dd}'";
        var yesterdaySql = $"SELECT COUNT(*) FROM alertlog WHERE DATE(CREATED) = '{yesterday:yyyy-MM-dd}'";
        
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        
        command.CommandText = todaySql;
        var todayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
        
        command.CommandText = yesterdaySql;
        var yesterdayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

        return new TableCountDto { Today = todayCount, Yesterday = yesterdayCount };
    }

    private async Task<TableCountDto> GetDapanCountAsync(DateTime today, DateTime yesterday)
    {
        return await GetTableCountAsync("dapan", "dapanDate", today, yesterday);
    }

    private async Task<TableCountDto> GetDetectorCountAsync(DateTime today, DateTime yesterday)
    {
        try
        {
            var todaySql = $"SELECT COUNT(*) FROM detector WHERE DATE(CreateTime) = '{today:yyyy-MM-dd}'";
            var yesterdaySql = $"SELECT COUNT(*) FROM detector WHERE DATE(CreateTime) = '{yesterday:yyyy-MM-dd}'";
            
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            
            command.CommandText = todaySql;
            var todayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
            
            command.CommandText = yesterdaySql;
            var yesterdayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

            return new TableCountDto { Today = todayCount, Yesterday = yesterdayCount };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get detector count, returning 0");
            return new TableCountDto { Today = 0, Yesterday = 0 };
        }
    }

    private async Task<TableCountDto> GetInvestBaseCountAsync(DateTime today, DateTime yesterday)
    {
        var todaySql = $"SELECT COUNT(*) FROM investbase WHERE RecDate = '{today:yyyy-MM-dd}'";
        var yesterdaySql = $"SELECT COUNT(*) FROM investbase WHERE RecDate = '{yesterday:yyyy-MM-dd}'";
        
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        
        command.CommandText = todaySql;
        var todayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
        
        command.CommandText = yesterdaySql;
        var yesterdayCount = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

        return new TableCountDto { Today = todayCount, Yesterday = yesterdayCount };
    }

    private async Task<TableCountDto> GetStock60DaysCountAsync(DateTime today, DateTime yesterday)
    {
        return await GetTableCountAsync("stock60days", "StockDate", today, yesterday);
    }

    private async Task<DapanDataDto?> GetDapanDataAsync(DateTime today, DateTime yesterday)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var result = new DapanDataDto();
            
            using var command = connection.CreateCommand();
            
            command.CommandText = $"SELECT dapanType, dapanPrice FROM dapan WHERE dapanDate = '{today:yyyy-MM-dd}'";
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var type = reader.GetString(0);
                var price = reader.GetDecimal(1);
                
                if (result.Today == null) result.Today = new DapanPointsDto();
                
                if (type == "上市") result.Today.Listed = price;
                else if (type == "上櫃") result.Today.Otc = price;
            }
            
            await reader.CloseAsync();
            
            command.CommandText = $"SELECT dapanType, dapanPrice FROM dapan WHERE dapanDate = '{yesterday:yyyy-MM-dd}'";
            using var reader2 = await command.ExecuteReaderAsync();
            
            while (await reader2.ReadAsync())
            {
                var type = reader2.GetString(0);
                var price = reader2.GetDecimal(1);
                
                if (result.Yesterday == null) result.Yesterday = new DapanPointsDto();
                
                if (type == "上市") result.Yesterday.Listed = price;
                else if (type == "上櫃") result.Yesterday.Otc = price;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get dapan data");
            return null;
        }
    }

    private async Task<StockTypeStatsDto> GetStockTypeStatsAsync(string tableName, string dateColumn, DateTime today, DateTime yesterday)
    {
        try
        {
            var result = new StockTypeStatsDto();
            
            var todayStats = await GetStatsByTypeAsync(tableName, dateColumn, today);
            var yesterdayStats = await GetStatsByTypeAsync(tableName, dateColumn, yesterday);
            
            result.Listed = CreateStats(todayStats.GetValueOrDefault("上市"), yesterdayStats.GetValueOrDefault("上市"));
            result.Otc = CreateStats(todayStats.GetValueOrDefault("上櫃"), yesterdayStats.GetValueOrDefault("上櫃"));
            result.Emerging = CreateStats(todayStats.GetValueOrDefault("興櫃"), yesterdayStats.GetValueOrDefault("興櫃"));
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get stock type stats for {TableName}", tableName);
            return new StockTypeStatsDto();
        }
    }

    private async Task<Dictionary<string, (int count, decimal amount, long volume)>> GetStatsByTypeAsync(string tableName, string dateColumn, DateTime date)
    {
        var result = new Dictionary<string, (int count, decimal amount, long volume)>();
        
        var priceColumn = tableName.ToLower() switch
        {
            "weekall" => "EndPrice",
            "tradedata" => "StockPrice",
            _ => "EndPrice"
        };
        
        var sql = $@"
            SELECT 
                StockType,
                COUNT(*) as Count,
                COALESCE(SUM({priceColumn} * Vol), 0) as TotalAmount,
                COALESCE(SUM(Vol), 0) as TotalVolume
            FROM {tableName}
            WHERE {dateColumn} = '{date:yyyy-MM-dd}'
            GROUP BY StockType";
        
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var stockType = reader.GetString(0);
            var count = reader.GetInt32(1);
            var amount = reader.GetDecimal(2);
            var volume = reader.GetInt64(3);
            
            result[stockType] = (count, amount, volume);
        }
        
        return result;
    }

    private StockTypeStats CreateStats(
        (int count, decimal amount, long volume)? today,
        (int count, decimal amount, long volume)? yesterday)
    {
        var todayCount = today?.count ?? 0;
        var yesterdayCount = yesterday?.count ?? 0;
        var yesterdayAmount = yesterday?.amount ?? 0;
        var yesterdayVolume = yesterday?.volume ?? 0;
        var todayAmount = today?.amount ?? 0;
        var todayVolume = today?.volume ?? 0;
        
        return new StockTypeStats
        {
            Count = todayCount,
            CountChange = yesterdayCount > 0 ? ((todayCount - yesterdayCount) / (double)yesterdayCount) * 100 : 0,
            Amount = todayAmount,
            AmountChange = yesterdayAmount > 0 
                ? (double)((todayAmount - yesterdayAmount) / yesterdayAmount) * 100 
                : 0,
            Volume = todayVolume,
            VolumeChange = yesterdayVolume > 0 
                ? ((todayVolume - yesterdayVolume) / (double)yesterdayVolume) * 100 
                : 0
        };
    }
}
