using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.Services.GoodInfo
{
    /// <summary>
    /// MACD 指標處理服務
    /// 處理 linkLabel11: MACD>0 / OSC負轉正 (DIF、MACD小於0且OSC由負轉正)
    /// </summary>
    public class MacdIndicatorService
    {
        private readonly ITradeDataRepository _tradeDataRepository;
        private readonly ILogger<MacdIndicatorService> _logger;

        public MacdIndicatorService(
            ITradeDataRepository tradeDataRepository,
            ILogger<MacdIndicatorService> logger)
        {
            _tradeDataRepository = tradeDataRepository;
            _logger = logger;
        }

        /// <summary>
        /// 處理 MACD 指標 CSV 檔案並更新資料庫
        /// </summary>
        /// <param name="csvFilePath">CSV 檔案路徑</param>
        /// <param name="recNote">記錄註記 (例如: "MACD>0", "OSC負轉正")</param>
        /// <returns>更新的資料筆數</returns>
        public async Task<int> ProcessMacdIndicatorCsvAsync(string csvFilePath, string recNote)
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
                    if (columns.Count < 9) // 至少需要 9 個欄位 (0-8)
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

                    // 解析 MACD 指標數據
                    var dif = columns[6].Trim();
                    var macd = columns[7].Trim();
                    var osc = columns[8].Trim();

                    // 更新資料庫
                    await UpdateTradeDataAsync(stockId, dataDate.Value, dif, macd, osc, recNote);
                    updatedCount++;

                    _logger.LogDebug($"已更新 {stockId} 的 MACD 指標數據");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"處理 CSV 行時發生錯誤: {ex.Message}, Line: {line}");
                    continue;
                }
            }

            _logger.LogInformation($"MACD 指標 CSV 處理完成，共更新 {updatedCount} 筆資料，日期: {dataDate}");
            return updatedCount;
        }

        /// <summary>
        /// 更新 TradeData 的 MACD 指標欄位
        /// </summary>
        private async Task UpdateTradeDataAsync(
            string stockId,
            DateTime transDate,
            string dif,
            string macd,
            string osc,
            string recNote)
        {
            var tradeData = await _tradeDataRepository.GetByStockIdAndDateAsync(stockId, transDate);
            if (tradeData == null)
            {
                _logger.LogWarning($"找不到 {stockId} 在 {transDate:yyyy/MM/dd} 的交易資料");
                return;
            }

            // 更新 MACD 指標欄位 (資料庫欄位型別為 string)
            tradeData.DIF = dif;
            tradeData.MACD = macd;
            tradeData.OSC = osc;

            // 更新 recNote (如果有內容才更新)
            if (!string.IsNullOrWhiteSpace(recNote))
            {
                if (string.IsNullOrWhiteSpace(tradeData.RecNote))
                {
                    tradeData.RecNote = recNote;
                }
                else if (!tradeData.RecNote.Contains(recNote))
                {
                    tradeData.RecNote += $",{recNote}";
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
        /// 解析 decimal，失敗返回 null
        /// </summary>
        private decimal? ParseDecimalOrNull(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (decimal.TryParse(value, out var result))
                return result;

            return null;
        }
    }
}
