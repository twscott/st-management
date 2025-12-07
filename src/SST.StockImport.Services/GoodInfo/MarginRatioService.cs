using System.Globalization;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.Services.GoodInfo;

/// <summary>
/// 券資比資料服務 - 處理CSV解析和資料庫更新
/// 複製自舊系統 appCommon.cs 的 券資比_Insert 方法
/// </summary>
public class MarginRatioService
{
    private readonly ITradeDataRepository _tradeDataRepository;

    public MarginRatioService(ITradeDataRepository tradeDataRepository)
    {
        _tradeDataRepository = tradeDataRepository;
    }

    /// <summary>
    /// 處理券資比CSV文件並更新資料庫
    /// </summary>
    /// <param name="csvFilePath">CSV文件路徑</param>
    /// <returns>成功更新的記錄數量</returns>
    public async Task<int> ProcessMarginRatioCsvAsync(string csvFilePath)
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

        foreach (var line in dataLines)
        {
            var columns = ParseCsvLine(line);
            if (columns.Length < 24) continue; // 確保有足夠欄位

            // 解析資料日期（第一筆資料時取得）
            if (dataDate == null)
            {
                dataDate = ParseDate(columns[6]); // 第7欄是資券日期
                if (dataDate == null) continue;
            }

            // 取得股票代號
            var stockId = columns[1].Trim();
            if (string.IsNullOrEmpty(stockId)) continue;

            // 查詢現有記錄
            var existingData = await _tradeDataRepository.GetByStockIdAndDateAsync(
                stockId, 
                DateTime.ParseExact(dataDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            );

            if (existingData != null)
            {
                // 更新融資融券資料
                // CSV欄位映射（根據舊系統 appCommon.cs 的券資比_Insert）:
                // [1] = StockID
                // [2] = Name
                // [3] = Price
                // [6] = Date (資券日期)
                // [10] = 融資增減
                // [11] = 融資餘額
                // [12] = 融資使用率
                // [17] = 融券增減
                // [18] = 融券餘額
                // [19] = 融券使用率
                // [23] = 券資比

                existingData.Rongzi = ParseInt(columns[11]);
                existingData.RongziDiff = ParseInt(columns[10]);
                existingData.RongziRate = ParseDecimal(columns[12]);
                
                existingData.Ronquan = ParseInt(columns[18]);
                existingData.RongquanDiff = ParseInt(columns[17]);
                existingData.RongquanRate = ParseDecimal(columns[19]);
                
                existingData.QuanziRate = ParseDecimal(columns[23]);

                await _tradeDataRepository.UpdateAsync(existingData);
                updatedCount++;
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
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    /// <summary>
    /// 解析日期字串 (YYYY/MM/DD 轉 yyyy-MM-dd)
    /// </summary>
    private string? ParseDate(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;

        try
        {
            // GoodInfo的日期格式是 2024/12/06
            var parts = dateStr.Split('/');
            if (parts.Length == 3)
            {
                return $"{parts[0]}-{parts[1].PadLeft(2, '0')}-{parts[2].PadLeft(2, '0')}";
            }
        }
        catch
        {
            // 解析失敗返回null
        }

        return null;
    }

    /// <summary>
    /// 解析整數，移除逗號
    /// </summary>
    private int ParseInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        
        var cleaned = value.Replace(",", "").Trim();
        if (int.TryParse(cleaned, out int result))
        {
            return result;
        }
        return 0;
    }

    /// <summary>
    /// 解析小數，處理百分比
    /// </summary>
    private decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0.00m;

        var cleaned = value.Replace("%", "").Replace(",", "").Trim();
        
        // 處理 "0.12" 形式（舊系統用 '0{contentList[12]}' 來加0前綴）
        if (cleaned.StartsWith("."))
        {
            cleaned = "0" + cleaned;
        }

        if (decimal.TryParse(cleaned, out decimal result))
        {
            return result;
        }
        return 0.00m;
    }
}
