using System.Globalization;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.Services.GoodInfo;

/// <summary>
/// 周轉率資料服務 - 處理CSV解析和資料庫更新
/// 複製自舊系統 appCommon.cs 的 周轉率_insert 方法
/// </summary>
    public class TurnoverRateService
    {
        private readonly ITradeDataRepository _tradeDataRepository;

        public TurnoverRateService(ITradeDataRepository tradeDataRepository)
        {
            _tradeDataRepository = tradeDataRepository;
        }    /// <summary>
    /// 處理周轉率CSV文件並更新資料庫
    /// </summary>
    /// <param name="csvFilePath">CSV文件路徑</param>
    /// <returns>成功更新的記錄數量</returns>
    public async Task<int> ProcessTurnoverRateCsvAsync(string csvFilePath)
    {
        if (!File.Exists(csvFilePath))
        {
            throw new FileNotFoundException($"CSV file not found: {csvFilePath}");
        }

        var lines = await File.ReadAllLinesAsync(csvFilePath);
        if (lines.Length < 2)
        {
            throw new InvalidOperationException("CSV file is empty or has no data rows");
        }

        // 跳過標題行
        var dataLines = lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line));
        
        int updatedCount = 0;
        string? dataDate = null;
        const int stockIdIdx = 1;
        const int dateIdx = 6;

        foreach (var line in dataLines)
        {
            var columns = ParseCsvLine(line);
            if (columns.Length < 9) continue; // 確保有足夠欄位

            // 驗證股票代號（長度≤4且為數字）
            var stockId = columns[stockIdIdx].Trim();
            if (string.IsNullOrEmpty(stockId) || stockId.Length > 4 || !IsNumeric(stockId))
                continue;

            // 解析資料日期（第一筆資料時取得）
            if (dataDate == null)
            {
                dataDate = ParseDateWithYear(columns[dateIdx]);
                if (dataDate == null) continue;
            }

            // 查詢現有記錄
            var existingData = await _tradeDataRepository.GetByStockIdAndDateAsync(
                stockId,
                DateTime.ParseExact(dataDate, "yyyy/MM/dd", CultureInfo.InvariantCulture)
            );

            if (existingData != null)
            {
                // 更新周轉率資料
                // CSV欄位映射（根據舊系統 appCommon.cs 的周轉率_insert）:
                // [1] = StockID
                // [2] = Name
                // [3] = Price
                // [6] = Date (MM/dd格式)
                // [7] = 周轉率（當日）
                // [8] = 周轉率（前一日，可能為空）

                var turnoverRate = ParseDecimal(columns[7]);
                existingData.TurnoverRate = turnoverRate;

                // 計算周轉率差異（舊系統公式：2 * 當日 - 前一日）
                // 但舊系統後來註解掉了 turnoverDiff 的更新，所以我們只更新 turnoverRate
                // double turnoverDiff = 2 * turnoverRate - (string.IsNullOrEmpty(columns[8]) ? turnoverRate : ParseDouble(columns[8]));

                await _tradeDataRepository.UpdateAsync(existingData);
                updatedCount++;

                // TODO: 如果需要 recommandstock 表的邏輯（周轉率差≥25），需要額外實作
                // 目前先只更新 tradedata 表的 turnoverRate
            }
        }

        return updatedCount;
    }

    /// <summary>
    /// 解析CSV行，處理可能的引號和逗號
    /// </summary>
    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Replace("\"", "").Replace("=", ""));
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString().Replace("\"", "").Replace("=", ""));
        return result.ToArray();
    }

    /// <summary>
    /// 解析日期字串 (MM/dd 格式，需要加上年份)
    /// 舊系統邏輯：如果日期大於當前日期，則為去年
    /// 輸出格式：yyyy/MM/dd
    /// </summary>
    private string? ParseDateWithYear(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;

        try
        {
            // GoodInfo的周轉率日期格式是 MM/dd
            var now = DateTime.Now;
            
            // 解析月日
            var parts = dateStr.Split('/');
            if (parts.Length != 2) return null;
            
            if (!int.TryParse(parts[0], out int month) || !int.TryParse(parts[1], out int day))
                return null;
            
            // 先用今年試試看
            int year = now.Year;
            var testDate = new DateTime(year, month, day);
            
            // 如果日期在未來（超過當前日期），表示是去年的資料
            if (testDate > now)
            {
                year = now.Year - 1;
            }
            
            // 確保月日都是兩位數 (例如 2024/12/06)
            var monthStr = month.ToString().PadLeft(2, '0');
            var dayStr = day.ToString().PadLeft(2, '0');
            
            return $"{year}/{monthStr}/{dayStr}";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 檢查字串是否為數字
    /// </summary>
    private bool IsNumeric(string value)
    {
        return double.TryParse(value, out _);
    }

    /// <summary>
    /// 解析小數
    /// </summary>
    private decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0.00m;

        var cleaned = value.Replace("%", "").Replace(",", "").Trim();

        if (decimal.TryParse(cleaned, out decimal result))
        {
            return result;
        }
        return 0.00m;
    }
}
