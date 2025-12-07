using Microsoft.Extensions.Logging;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SST.StockImport.Services.GoodInfo;

/// <summary>
/// Service for processing Bollinger Bands data from GoodInfo CSV
/// 超布林上軌 (linkLabel3): 股價高於布林上軌
/// </summary>
public class BollingerBandsService
{
    private readonly ITradeDataRepository _repository;
    private readonly ILogger<BollingerBandsService>? _logger;

    public BollingerBandsService(ITradeDataRepository repository, ILogger<BollingerBandsService>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger;
    }

    /// <summary>
    /// Process Bollinger Bands CSV file and update TradeData
    /// </summary>
    /// <param name="csvFilePath">Path to CSV file</param>
    /// <param name="recNote">Record note to append to boolinPosition</param>
    /// <returns>Number of records updated</returns>
    public async Task<int> ProcessBollingerBandsCsvAsync(string csvFilePath, string recNote)
    {
        if (!File.Exists(csvFilePath))
        {
            _logger?.LogError("CSV file not found: {FilePath}", csvFilePath);
            throw new FileNotFoundException($"CSV file not found: {csvFilePath}");
        }

        if (string.IsNullOrWhiteSpace(recNote))
        {
            throw new ArgumentException("RecNote cannot be null or empty", nameof(recNote));
        }

        int updatedCount = 0;
        string? dataDate = null;
        var lines = await File.ReadAllLinesAsync(csvFilePath);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var columns = ParseCsvLine(line);
            if (columns.Count < 14) // Need at least 14 columns
                continue;

            var stockId = columns[0].Trim();

            // Skip invalid stock IDs
            if (stockId.Length > 4 || !IsNumeric(stockId))
                continue;

            // Skip if not exactly 4 digits
            if (stockId.Length != 4)
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
                // Parse deviations (columns 8, 10, 12)
                var upDeviation = ParseDecimalOrNull(columns[8]);
                var midDeviation = ParseDecimalOrNull(columns[10]);
                var downDeviation = ParseDecimalOrNull(columns[12]);

                // Parse directions from columns 7, 9, 11 (contains ↗ ↘ symbols)
                var boolDirection = ExtractDirection(columns[7]) +
                                  ExtractDirection(columns[9]) +
                                  ExtractDirection(columns[11]);

                // Parse kaikou from column 13 (開口%)
                var kaikouStr = columns[13];
                var kaikouDirection = ExtractDirection(kaikouStr, includeEqual: false);
                // Remove the arrow symbol before parsing
                var kaikouValue = ParseDecimalOrNull(kaikouStr.Replace("↗", "").Replace("↘", "").Replace("→", ""));

                // Get existing entity
                var transDate = DateTime.ParseExact(dataDate, "yyyy/M/d", CultureInfo.InvariantCulture);
                var entity = await _repository.GetByStockIdAndDateAsync(stockId, transDate);
                if (entity == null)
                {
                    _logger?.LogWarning("TradeData not found for StockID: {StockId}, Date: {Date}", stockId, dataDate);
                    continue;
                }

                // Update Bollinger Bands fields
                entity.BoolUpDeviation = upDeviation;
                entity.BoolMidDeviation = midDeviation;
                entity.BoolDownDeviation = downDeviation;
                entity.BoolDirection = boolDirection;
                
                // Only update kaikou if we have a valid value
                if (kaikouValue.HasValue)
                {
                    // Combine direction and value for boolKaikou
                    entity.BoolKaikou = kaikouValue;
                }

                // Append to boolinPosition
                if (string.IsNullOrWhiteSpace(entity.BoolinPosition))
                {
                    entity.BoolinPosition = recNote;
                }
                else if (!entity.BoolinPosition.Contains(recNote))
                {
                    entity.BoolinPosition += $",{recNote}";
                }

                await _repository.UpdateAsync(entity);
                updatedCount++;

                _logger?.LogDebug("Updated StockID: {StockId} with BoolUpDeviation: {Up}, BoolMidDeviation: {Mid}, BoolDownDeviation: {Down}, BoolDirection: {Direction}, BoolKaikou: {Kaikou}",
                    stockId, upDeviation, midDeviation, downDeviation, boolDirection, kaikouValue);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing line for StockID: {StockId}", stockId);
                // Continue processing other records
            }
        }

        _logger?.LogInformation("Bollinger Bands CSV processing complete. Updated {Count} records for date {Date}",
            updatedCount, dataDate);

        return updatedCount;
    }

    /// <summary>
    /// Extract direction symbol from string containing ↗ ↘ →
    /// </summary>
    private string ExtractDirection(string input, bool includeEqual = true)
    {
        if (string.IsNullOrWhiteSpace(input))
            return includeEqual ? "=" : "";

        if (input.Contains("↗"))
            return "+";
        else if (input.Contains("↘"))
            return "-";
        else if (includeEqual)
            return "=";
        else
            return "";
    }

    /// <summary>
    /// Parse CSV line handling quoted fields with commas
    /// </summary>
    private List<string> ParseCsvLine(string line)
    {
        var regex = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
        var fields = regex.Split(line).Select(f => f.Replace("\"", "").Replace("=", "").Trim()).ToList();
        return fields;
    }

    /// <summary>
    /// Parse date string in MM/dd format and add current year
    /// </summary>
    private string ParseDateWithYear(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return string.Empty;

        try
        {
            var now = DateTime.Now;
            // Parse the date correctly
            var parts = dateStr.Split('/');
            if (parts.Length != 2)
                return string.Empty;

            int month = int.Parse(parts[0]);
            int day = int.Parse(parts[1]);
            
            // Determine year - if month/day is in future compared to current month/day, use last year
            // Compare using Date to avoid time component issues
            var testDate = new DateTime(now.Year, month, day).Date;
            var today = now.Date;
            int year = testDate > today ? now.Year - 1 : now.Year;

            // Return in format that matches DateTime parsing: yyyy/M/d
            return $"{year}/{month}/{day}";
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Parse decimal value or return null
    /// </summary>
    private decimal? ParseDecimalOrNull(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Remove any non-numeric characters except decimal point and minus sign
        var cleaned = new string(value.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());

        if (decimal.TryParse(cleaned, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Check if string is numeric
    /// </summary>
    private bool IsNumeric(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit);
    }
}
