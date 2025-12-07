using Microsoft.Extensions.Logging;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SST.StockImport.Services.GoodInfo;

/// <summary>
/// 月季黃金 (均線分析) Service - linkLabel14
/// 處理均線分析 CSV 檔案，更新 TradeData 的均線相關欄位
/// </summary>
public class MovingAverageService
{
    private readonly ITradeDataRepository _repository;
    private readonly ILogger<MovingAverageService>? _logger;

    public MovingAverageService(
        ITradeDataRepository repository,
        ILogger<MovingAverageService>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger;
    }

    /// <summary>
    /// 處理均線分析 CSV 檔案
    /// CSV 格式: [0]=StockID, [1]=Name, [2]=Price, [3-6]=其他欄位, [7]=MA5, [8]=MA10, [9]=MA20, [10]=MA60, [11]=MA120, [12]=MA240
    /// </summary>
    public async Task<int> ProcessMovingAverageCsvAsync(string csvFilePath, string maNote, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(csvFilePath))
        {
            throw new FileNotFoundException($"CSV file not found: {csvFilePath}");
        }

        var lines = await File.ReadAllLinesAsync(csvFilePath, cancellationToken);
        int updatedCount = 0;
        string? dataDate = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Parse CSV line (handle quoted fields)
            var columns = ParseCsvLine(line);
            if (columns.Count < 13)
                continue;

            var stockId = columns[0];
            if (stockId.Length > 4 || !IsNumeric(stockId))
                continue;

            // Parse date from column 6 (format: MM/dd)
            if (dataDate == null)
            {
                dataDate = ParseDateWithYear(columns[6]);
            }

            if (string.IsNullOrEmpty(dataDate))
                continue;

            try
            {
                // Extract direction from MA5, MA10, MA20, MA60, MA120, MA240 (columns 7-12)
                var maDirection = ExtractDirection(columns[7]) +
                                ExtractDirection(columns[8]) +
                                ExtractDirection(columns[9]) +
                                ExtractDirection(columns[10]) +
                                ExtractDirection(columns[11]) +
                                ExtractDirection(columns[12]);

                // Get existing entity
                var transDate = DateTime.ParseExact(dataDate, "yyyy/M/d", CultureInfo.InvariantCulture);
                var entity = await _repository.GetByStockIdAndDateAsync(stockId, transDate, cancellationToken);
                if (entity == null)
                {
                    _logger?.LogWarning("TradeData not found for StockID: {StockId}, Date: {Date}", stockId, dataDate);
                    continue;
                }

                // Update MA fields (conditionally - don't overwrite if starts with # or *)
                entity.MA5 = UpdateIfNotMarked(entity.MA5, columns[7], '#');
                entity.MA10 = UpdateIfNotMarked(entity.MA10, columns[8], '#');
                entity.MA20 = UpdateIfNotMarked(entity.MA20, columns[9], '*');
                entity.MASeason = UpdateIfNotMarked(entity.MASeason, columns[10], '*');
                entity.MAHalfYear = UpdateIfNotMarked(entity.MAHalfYear, columns[11], '*');
                entity.MAYear = UpdateIfNotMarked(entity.MAYear, columns[12], '*');
                
                entity.MADirection = maDirection;

                // Append to MANote
                if (string.IsNullOrWhiteSpace(entity.MANote))
                {
                    entity.MANote = $"{maNote}|";
                }
                else if (!entity.MANote.Contains(maNote))
                {
                    entity.MANote += $"{maNote}|";
                }

                await _repository.UpdateAsync(entity, cancellationToken);
                updatedCount++;

                _logger?.LogDebug("Updated StockID: {StockId} with MA values and direction: {Direction}",
                    stockId, maDirection);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing line for StockID: {StockId}", stockId);
                // Continue processing other records
            }
        }

        _logger?.LogInformation("Processed {UpdatedCount} moving average records", updatedCount);
        return updatedCount;
    }

    private string UpdateIfNotMarked(string? field, string newValue, char marker)
    {
        // Don't update if existing value starts with marker
        if (string.IsNullOrEmpty(field) || field[0] != marker)
        {
            return newValue;
        }
        return field;
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

    private string ParseDateWithYear(string dateStr)
    {
        // Format: MM/dd or M/d
        if (string.IsNullOrWhiteSpace(dateStr))
            return string.Empty;

        try
        {
            var parts = dateStr.Split('/');
            if (parts.Length != 2)
                return string.Empty;

            int month = int.Parse(parts[0]);
            int day = int.Parse(parts[1]);
            int year = DateTime.Now.Year;

            // Check if the date is in the future - if so, use last year
            var testDate = new DateTime(year, month, day);
            if (testDate > DateTime.Now)
            {
                year--;
            }

            return $"{year}/{month}/{day}";
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Extract direction symbol from MA value string
    /// ↗ → +, ↘ → -, → → =
    /// </summary>
    private string ExtractDirection(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (value.Contains("↗"))
            return "+";
        if (value.Contains("↘"))
            return "-";
        if (value.Contains("→"))
            return "=";

        return string.Empty;
    }
}
