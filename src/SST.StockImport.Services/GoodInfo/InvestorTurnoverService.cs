using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.Services.GoodInfo;

/// <summary>
/// 外資/投信連買連賣轉折處理服務
/// 處理 linkLabel7: 外資連續賣出轉買進
/// 處理 linkLabel8: 投信連續賣出轉買進
/// 對應舊系統：appCommon.cs 的 投外轉折_Insert 方法 (lines 5896-5990)
/// </summary>
public class InvestorTurnoverService
{
    private readonly ITradeDataRepository _tradeDataRepository;
    private readonly ILogger<InvestorTurnoverService> _logger;

    public InvestorTurnoverService(
        ITradeDataRepository tradeDataRepository,
        ILogger<InvestorTurnoverService> logger)
    {
        _tradeDataRepository = tradeDataRepository;
        _logger = logger;
    }

    /// <summary>
    /// 處理外資/投信轉折 CSV 檔案並更新資料庫
    /// </summary>
    /// <param name="csvFilePath">CSV 檔案路徑</param>
    /// <param name="recNote">記錄註記 (例如: "外資連買連賣轉折", "投信連買連賣轉折")</param>
    /// <returns>更新的資料筆數</returns>
    public async Task<int> ProcessInvestorTurnoverCsvAsync(string csvFilePath, string recNote)
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
                if (columns.Count < 12) // 至少需要 12 個欄位 (0-11)
                    continue;

                var stockId = columns[0].Trim();

                // 跳過標題行或無效的 StockID
                if (stockId.Length > 4 || !IsNumeric(stockId))
                    continue;

                // 只處理 4 位數的股票代碼
                if (stockId.Length != 4)
                    continue;

                // 解析日期 (第一次遇到時)
                if (dataDate == null)
                {
                    dataDate = ParseDateWithYear(columns[5].Trim());
                }

                // 解析轉折數據
                var dif = columns[6].Trim();
                var foreignSwitch = columns[7].Trim(); // 外資轉折
                var investSwitch = columns[9].Trim();  // 投信轉折
                var dealerSwitch = columns[11].Trim(); // 自營轉折

                // 建立 reason 字串（與舊系統一致）
                var reason = BuildReasonString(foreignSwitch, investSwitch, dealerSwitch);

                // 更新資料庫
                await UpdateTradeDataAsync(stockId, dataDate.Value, dif, foreignSwitch, investSwitch, reason, recNote);
                updatedCount++;

                _logger.LogDebug($"已更新 {stockId} 的轉折數據：外資={foreignSwitch}, 投信={investSwitch}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"處理 CSV 行時發生錯誤: {ex.Message}, Line: {line}");
                continue;
            }
        }

        _logger.LogInformation($"投外轉折 CSV 處理完成，共更新 {updatedCount} 筆資料，日期: {dataDate}");
        return updatedCount;
    }

    /// <summary>
    /// 建立 reason 字串（與舊系統格式一致）
    /// </summary>
    private string BuildReasonString(string foreignSwitch, string investSwitch, string dealerSwitch)
    {
        var reason = "";
        
        if (!string.IsNullOrWhiteSpace(foreignSwitch))
            reason += $"外資{foreignSwitch},";
        
        if (!string.IsNullOrWhiteSpace(investSwitch))
            reason += $"投信{investSwitch},";
        
        if (!string.IsNullOrWhiteSpace(dealerSwitch))
            reason += $"自營{dealerSwitch}";

        return reason.TrimEnd(',');
    }

    /// <summary>
    /// 更新 TradeData 的轉折欄位
    /// </summary>
    private async Task UpdateTradeDataAsync(
        string stockId,
        DateTime transDate,
        string dif,
        string foreignSwitch,
        string investSwitch,
        string reason,
        string recNote)
    {
        var tradeData = await _tradeDataRepository.GetByStockIdAndDateAsync(stockId, transDate);
        if (tradeData == null)
        {
            _logger.LogWarning($"找不到 {stockId} 在 {transDate:yyyy/MM/dd} 的交易資料");
            return;
        }

        // 更新轉折欄位（空字串轉為 null）
        tradeData.DIF = string.IsNullOrWhiteSpace(dif) ? null : dif;
        tradeData.ForgneSwitch = string.IsNullOrWhiteSpace(foreignSwitch) ? null : foreignSwitch;
        tradeData.InvwstSwitch = string.IsNullOrWhiteSpace(investSwitch) ? null : investSwitch;

        // 更新 recNote
        // 與舊系統邏輯一致：concat(IF(recNote is null, '','|'), '{reason}')
        if (!string.IsNullOrWhiteSpace(reason))
        {
            if (string.IsNullOrWhiteSpace(tradeData.RecNote))
            {
                tradeData.RecNote = reason;
            }
            else
            {
                tradeData.RecNote += $"|{reason}";
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
}
