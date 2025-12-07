using System.Text.RegularExpressions;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.Services.GoodInfo;

/// <summary>
/// 外資/投信同步買賣超處理服務
/// 對應舊系統：appCommon.cs 的 投外同步 方法 (lines 5833-5890)
/// linkLabel30: 外資、投信同步買超–當日
/// linkLabel31: 外資、投信同步賣超–當日
/// </summary>
public class SyncBuySellService
{
    private readonly ITradeDataRepository _tradeDataRepository;

    public SyncBuySellService(ITradeDataRepository tradeDataRepository)
    {
        _tradeDataRepository = tradeDataRepository;
    }

    /// <summary>
    /// 處理外資/投信同步買賣超 CSV 檔案並更新資料庫
    /// </summary>
    /// <param name="csvFilePath">CSV 檔案路徑</param>
    /// <param name="recNote">記錄註記 (例如: "外資、投信同步買超", "外資、投信同步賣超")</param>
    /// <returns>更新的資料筆數</returns>
    public async Task<int> ProcessSyncBuySellCsvAsync(string csvFilePath, string recNote)
    {
        if (!File.Exists(csvFilePath))
        {
            throw new FileNotFoundException($"CSV 檔案不存在: {csvFilePath}");
        }

        var lines = await File.ReadAllLinesAsync(csvFilePath);
        if (lines.Length == 0)
        {
            throw new InvalidOperationException("CSV 檔案是空的");
        }

        int updatedCount = 0;
        DateTime? dataDate = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var columns = ParseCsvLine(line);
                if (columns.Count < 20) // 至少需要 20 個欄位 (0-19)
                    continue;

                var stockId = columns[0].Trim();

                // 跳過標題行或無效的 StockID
                if (stockId.Length > 4 || !IsNumeric(stockId))
                    continue;

                // 解析日期 (第一次遇到時)
                if (dataDate == null)
                {
                    dataDate = ParseDateWithYear(columns[6].Trim());
                }

                // 解析金額數據 (千元)
                var foreigneAmt = SimpleNumber(columns[9].Trim());
                var investAmt = SimpleNumber(columns[12].Trim());
                var farenSerialAmt = SimpleNumber(columns[18].Trim());
                var legalPersonNote = columns[19].Trim();

                // 更新資料庫
                await UpdateTradeDataAsync(stockId, dataDate.Value, foreigneAmt, investAmt, 
                    farenSerialAmt, legalPersonNote, recNote);
                updatedCount++;
            }
            catch (Exception)
            {
                continue;
            }
        }

        return updatedCount;
    }

    /// <summary>
    /// 更新 TradeData 的外資/投信同步買賣超欄位
    /// </summary>
    private async Task UpdateTradeDataAsync(
        string stockId,
        DateTime transDate,
        int? foreigneAmt,
        int? investAmt,
        int? farenSerialAmt,
        string? legalPersonNote,
        string recNote)
    {
        var tradeData = await _tradeDataRepository.GetByStockIdAndDateAsync(stockId, transDate);
        if (tradeData == null)
        {
            return;
        }

        // 更新金額欄位
        tradeData.ForeigneAmt = foreigneAmt;
        tradeData.InvestAmt = investAmt;
        tradeData.FarenSerialAmt = farenSerialAmt;
        tradeData.LegalPersonNote = legalPersonNote;

        // 更新 recNote (使用 | 分隔)
        if (!string.IsNullOrWhiteSpace(recNote))
        {
            if (string.IsNullOrWhiteSpace(tradeData.RecNote))
            {
                tradeData.RecNote = recNote;
            }
            else if (!tradeData.RecNote.Contains(recNote))
            {
                tradeData.RecNote += $"|{recNote}";
            }
        }

        await _tradeDataRepository.UpdateAsync(tradeData);
    }

    /// <summary>
    /// 解析 CSV 行 (處理雙引號包含的逗號)
    /// </summary>
    private List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = "";
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.Replace("\"", "").Replace("=", ""));
                current = "";
            }
            else
            {
                current += c;
            }
        }

        result.Add(current.Replace("\"", "").Replace("=", ""));
        return result;
    }

    /// <summary>
    /// 解析日期並加上年份 (MM/dd -> yyyy/MM/dd)
    /// </summary>
    private DateTime ParseDateWithYear(string dateStr)
    {
        var testDate = DateTime.Parse($"{DateTime.Now.Year}/{dateStr}");
        if (testDate > DateTime.Now)
        {
            return DateTime.Parse($"{DateTime.Now.Year - 1}/{dateStr}");
        }
        return testDate;
    }

    /// <summary>
    /// 檢查字串是否為數字
    /// </summary>
    private bool IsNumeric(string value)
    {
        return decimal.TryParse(value, out _);
    }

    /// <summary>
    /// 簡化數字 (移除逗號、正負號等，返回整數)
    /// 對應舊系統的 CommonClass.simpleNumber
    /// </summary>
    private int? SimpleNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // 移除逗號、空格、正負號
        var cleaned = value.Replace(",", "")
                          .Replace(" ", "")
                          .Replace("+", "")
                          .Trim();

        if (string.IsNullOrEmpty(cleaned) || cleaned == "-")
            return null;

        if (int.TryParse(cleaned, out var result))
            return result;

        return null;
    }
}
