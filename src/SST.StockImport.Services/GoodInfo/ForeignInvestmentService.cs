using System.Globalization;
using System.Text.RegularExpressions;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.Services.GoodInfo;

/// <summary>
/// 外資/投信連續買賣處理服務
/// 對應舊系統：appCommon.cs 的 外資連續買賣 方法 (lines 5680-5755)
/// </summary>
public class ForeignInvestmentService
{
    private readonly ITradeDataRepository _tradeDataRepository;

    public ForeignInvestmentService(ITradeDataRepository tradeDataRepository)
    {
        _tradeDataRepository = tradeDataRepository;
    }

    /// <summary>
    /// 處理外資/投信連續買賣CSV文件
    /// </summary>
    /// <param name="csvFilePath">CSV文件路徑</param>
    /// <returns>更新的記錄數量</returns>
    public async Task<int> ProcessForeignInvestmentCsvAsync(string csvFilePath)
    {
        if (!File.Exists(csvFilePath))
            throw new FileNotFoundException($"CSV文件不存在: {csvFilePath}");

        var content = await File.ReadAllTextAsync(csvFilePath);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
            throw new InvalidOperationException("CSV文件為空");

        // 不跳過標題行，因為第一個欄位非數字的行會在後續驗證中被過濾掉
        var dataLines = lines.Where(l => !string.IsNullOrWhiteSpace(l));
        
        int updatedCount = 0;
        string? dataDate = null;
        const int stockIdIdx = 0;
        const int dateIdx = 5;

        foreach (var line in dataLines)
        {
            var columns = ParseCsvLine(line);
            if (columns.Length < 20) continue; // 確保有足夠欄位

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
                // 更新外資/投信連續買賣資料
                // CSV欄位映射（根據舊系統 appCommon.cs lines 5733-5738）:
                // [0] = StockID
                // [5] = Date (MM/dd格式)
                // [6] = 外資天數 (foreigneSerealDays)
                // [7] = 外資金額 (foreigneAmt) 單位:千元
                // [10] = 投信天數 (InvestSerealDays)
                // [11] = 投信金額 (InvestAmt) 單位:千元
                // [18] = 法人天數 (farenSerialDays)
                // [19] = 法人金額 (farenSerialAmt) 單位:千元

                existingData.ForeigneSerealDays = ParseInt(columns[6]);
                existingData.ForeigneAmt = ParseInt(columns[7]);
                existingData.InvestSerealDays = ParseInt(columns[10]);
                existingData.InvestAmt = ParseInt(columns[11]);
                existingData.FarenSerialDays = ParseInt(columns[18]);
                existingData.FarenSerialAmt = ParseInt(columns[19]);

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
                result.Add(current.ToString().Trim().Replace("\"", "").Replace("=", ""));
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        
        result.Add(current.ToString().Trim().Replace("\"", "").Replace("=", ""));
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
    /// 解析整數
    /// </summary>
    private int ParseInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;

        var cleaned = value.Replace(",", "").Replace("%", "").Trim();

        if (int.TryParse(cleaned, out int result))
        {
            return result;
        }
        return 0;
    }

    /// <summary>
    /// 解析小數（單位：千元）
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
