using System.Globalization;
using System.Text.RegularExpressions;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.Services.GoodInfo;

/// <summary>
/// 歷史成交量處理服務（日成交張數創歷日新高）
/// 對應舊系統：appCommon.cs 的 歷史成交_Insert 方法 (lines 5998-6090)
/// linkLabel1: 日成交張數創歷日新高
/// </summary>
public class HistoricalVolumeService
{
    private readonly ITradeDataRepository _tradeDataRepository;

    public HistoricalVolumeService(ITradeDataRepository tradeDataRepository)
    {
        _tradeDataRepository = tradeDataRepository;
    }

    /// <summary>
    /// 處理歷史成交量CSV文件
    /// </summary>
    /// <param name="csvFilePath">CSV文件路徑</param>
    /// <param name="recNote">記錄註記（例如："日成交張數創歷日新高"）</param>
    /// <returns>更新的記錄數量</returns>
    public async Task<int> ProcessHistoricalVolumeCsvAsync(string csvFilePath, string recNote = "日成交張數創歷日新高")
    {
        if (!File.Exists(csvFilePath))
            throw new FileNotFoundException($"CSV文件不存在: {csvFilePath}");

        var content = await File.ReadAllTextAsync(csvFilePath);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
            throw new InvalidOperationException("CSV文件為空");

        var dataLines = lines.Where(l => !string.IsNullOrWhiteSpace(l));
        
        int updatedCount = 0;
        string? dataDate = null;
        const int stockIdIdx = 0;
        const int dateIdx = 2; // 歷史成交量的日期在第2欄

        foreach (var line in dataLines)
        {
            var columns = ParseCsvLine(line);
            if (columns.Length < 3) continue; // 確保至少有股票代號和日期

            // 驗證股票代號（長度≤4且為數字）
            var stockId = columns[stockIdIdx].Trim();
            if (stockId.Length > 4 || !IsNumeric(stockId))
                continue;

            // 解析日期（只需解析一次）
            if (dataDate == null)
            {
                dataDate = ParseDateWithYear(columns[dateIdx].Trim());
                if (string.IsNullOrEmpty(dataDate))
                    continue;
            }

            // 更新 tradedata 的 recNote
            // 對應舊系統：update tradedata set recNote=concat(IF(recNote is null, '','|'), '{recNote}')
            var existingData = await _tradeDataRepository.GetByStockIdAndDateAsync(
                stockId,
                DateTime.ParseExact(dataDate, "yyyy/M/d", CultureInfo.InvariantCulture)
            );

            if (existingData != null)
            {
                // 拼接 recNote（如果已有值則加上 | 分隔符）
                if (string.IsNullOrEmpty(existingData.RecNote))
                    existingData.RecNote = recNote;
                else if (!existingData.RecNote.Contains(recNote))
                    existingData.RecNote += "|" + recNote;

                await _tradeDataRepository.UpdateAsync(existingData);
                updatedCount++;
            }
        }

        return updatedCount;
    }

    /// <summary>
    /// 解析CSV行，處理引號和逗號
    /// 對應舊系統：Regex CSVParser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))")
    /// </summary>
    private string[] ParseCsvLine(string line)
    {
        // 使用與舊系統相同的正則表達式來正確處理CSV中的引號
        var csvParser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
        var parts = csvParser.Split(line);

        return parts.Select(p => p.Replace("\"", "").Replace("=", "").Trim()).ToArray();
    }

    /// <summary>
    /// 檢查字串是否為數字
    /// </summary>
    private bool IsNumeric(string value)
    {
        return !string.IsNullOrEmpty(value) && value.All(char.IsDigit);
    }

    /// <summary>
    /// 將 MM/dd 格式的日期轉換為 yyyy/MM/dd
    /// 對應舊系統邏輯：如果日期 > 今天，表示是去年
    /// </summary>
    private string ParseDateWithYear(string dateStr)
    {
        try
        {
            var now = DateTime.Now;
            var testDate = DateTime.ParseExact(
                $"{now.Year}/{dateStr}",
                "yyyy/M/d",
                CultureInfo.InvariantCulture
            );

            // 如果計算出的日期大於今天，表示是去年的日期
            int year = testDate > now ? now.Year - 1 : now.Year;
            
            return $"{year}/{dateStr}";
        }
        catch
        {
            return string.Empty;
        }
    }
}
