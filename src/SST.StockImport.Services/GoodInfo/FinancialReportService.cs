using Microsoft.Extensions.Logging;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SST.StockImport.Services.GoodInfo;

/// <summary>
/// 財報分析 Service - 處理 EPS創新高 (linkLabel38), 季營收創高 (linkLabel44), 財報評分 (linkLabel34)
/// 更新 TradeData 的財報相關欄位: grossProfit, Profitability, financialReport, EPS
/// </summary>
public class FinancialReportService
{
    private readonly ITradeDataRepository _repository;
    private readonly ILogger<FinancialReportService>? _logger;

    public FinancialReportService(
        ITradeDataRepository repository,
        ILogger<FinancialReportService>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger;
    }

    /// <summary>
    /// 處理財報分析 CSV 檔案
    /// CSV 格式: [0]=StockID, [9]=grossProfit, [11]=Profitability, [16]=EPS, [19]=financialReport
    /// </summary>
    public async Task<int> ProcessFinancialReportCsvAsync(string csvFilePath, string reportNote, string targetDate, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(csvFilePath))
        {
            throw new FileNotFoundException($"CSV file not found: {csvFilePath}");
        }

        var lines = await File.ReadAllLinesAsync(csvFilePath, cancellationToken);
        int updatedCount = 0;

        // Parse target date
        DateTime transDate;
        try
        {
            transDate = DateTime.ParseExact(targetDate, "yyyy/M/d", CultureInfo.InvariantCulture);
        }
        catch
        {
            throw new ArgumentException($"Invalid date format: {targetDate}. Expected format: yyyy/M/d");
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Parse CSV line (handle quoted fields)
            var columns = ParseCsvLine(line);
            if (columns.Count < 20)
                continue;

            var stockId = columns[0];
            if (stockId.Length > 4 || !IsNumeric(stockId))
                continue;

            try
            {
                // Get existing entity
                var entity = await _repository.GetByStockIdAndDateAsync(stockId, transDate, cancellationToken);
                if (entity == null)
                {
                    _logger?.LogWarning("TradeData not found for StockID: {StockId}, Date: {Date}", stockId, targetDate);
                    continue;
                }

                // Parse financial data (use 0 if empty)
                entity.GrossProfit = (int)(ParseDecimalOrZero(columns[9]) ?? 0);
                entity.Profitability = (int)(ParseDecimalOrZero(columns[11]) ?? 0);
                entity.EPS = (int)(ParseDecimalOrZero(columns[16]) ?? 0);
                entity.FinancialReport = (int)(ParseDecimalOrZero(columns[19]) ?? 0);

                await _repository.UpdateAsync(entity, cancellationToken);
                updatedCount++;

                _logger?.LogDebug("Updated StockID: {StockId} with financial data - EPS: {EPS}, grossProfit: {GP}, profitability: {Profit}, report: {Report}",
                    stockId, entity.EPS, entity.GrossProfit, entity.Profitability, entity.FinancialReport);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing line for StockID: {StockId}", stockId);
                // Continue processing other records
            }
        }

        _logger?.LogInformation("Processed {UpdatedCount} financial report records for note: {ReportNote}", updatedCount, reportNote);
        return updatedCount;
    }

    private List<string> ParseCsvLine(string line)
    {
        var regex = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
        var result = regex.Split(line)
            .Select(s => s.Replace("\"", "").Replace("=", "").Trim())
            .ToList();
        return result;
    }

    private bool IsNumeric(string value)
    {
        return value.All(char.IsDigit);
    }

    private decimal? ParseDecimalOrZero(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0m;

        // Remove common formatting characters
        value = value.Replace("%", "").Replace(",", "").Replace("+", "").Trim();

        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return 0m;
    }
}
