using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SST.StockImport.Services.Helpers;

/// <summary>
/// GoodInfo CSV 檔案驗證器
/// 檢查下載的 CSV 資料日期是否正確
/// </summary>
public class GoodInfoCsvValidator
{
    private readonly ILogger<GoodInfoCsvValidator> _logger;
    private static readonly string DefaultDownloadPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
        "Downloads");
    private const string DefaultCsvFileName = "StockList.csv";

    public GoodInfoCsvValidator(ILogger<GoodInfoCsvValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 驗證 CSV 檔案的資料日期
    /// </summary>
    /// <param name="expectedDate">預期的資料日期（通常是今天）</param>
    /// <param name="csvPath">CSV 檔案路徑，如果為 null 則使用預設路徑</param>
    /// <returns>ValidationResult 包含是否有效、實際日期、警告訊息</returns>
    public CsvDateValidationResult ValidateCsvDataDate(DateTime expectedDate, string? csvPath = null)
    {
        var result = new CsvDateValidationResult
        {
            ExpectedDate = expectedDate,
            IsValid = false
        };

        try
        {
            var filePath = csvPath ?? Path.Combine(DefaultDownloadPath, DefaultCsvFileName);

            if (!File.Exists(filePath))
            {
                result.Warning = $"CSV 檔案不存在: {filePath}";
                _logger.LogWarning(result.Warning);
                return result;
            }

            // 讀取 CSV 的資料日期（從第一筆有效資料讀取）
            // 格式參考舊系統：stockIDIdx=1, DateIdx=6
            var actualDate = ExtractDataDateFromCsv(filePath);

            if (actualDate == null)
            {
                result.Warning = "無法從 CSV 檔案中提取資料日期";
                _logger.LogWarning(result.Warning);
                return result;
            }

            result.ActualDate = actualDate.Value;

            // 比對日期
            if (actualDate.Value.Date == expectedDate.Date)
            {
                result.IsValid = true;
                _logger.LogInformation("CSV 資料日期驗證通過: {ActualDate}", actualDate.Value.ToString("yyyy/MM/dd"));
            }
            else
            {
                result.IsValid = false;
                result.Warning = $"資料日期不符！預期: {expectedDate:yyyy/MM/dd}, 實際: {actualDate.Value:yyyy/MM/dd}";
                _logger.LogWarning(result.Warning);
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Warning = $"驗證 CSV 日期時發生錯誤: {ex.Message}";
            _logger.LogError(ex, "驗證 CSV 日期時發生錯誤");
            return result;
        }
    }

    /// <summary>
    /// 從 CSV 檔案中提取資料日期
    /// 複刻舊系統邏輯：從第一筆有效的股票資料（4碼數字）中讀取日期欄位
    /// </summary>
    private DateTime? ExtractDataDateFromCsv(string filePath)
    {
        try
        {
            using var reader = new StreamReader(filePath);
            string? line;
            var csvParser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");

            while ((line = reader.ReadLine()) != null)
            {
                var contentList = csvParser.Split(line)
                    .Select(s => s.Replace("\"", "").Replace("=", ""))
                    .ToList();

                // 檢查是否為有效的股票資料（第2欄=stockIDIdx=1，必須是4碼數字）
                if (contentList.Count > 6 && 
                    contentList[1].Length == 4 && 
                    IsNumeric(contentList[1]))
                {
                    // 第7欄 (DateIdx=6) 是日期，格式如 "12/06"
                    var dateString = contentList[6];
                    
                    // 判斷年份：如果日期大於今天，使用去年，否則使用今年
                    // 這是舊系統的邏輯
                    var currentYear = DateTime.Now.Year;
                    var testDate = $"{currentYear}/{dateString}";
                    
                    if (DateTime.TryParse(testDate, out var parsedDate))
                    {
                        // 如果解析出的日期大於今天，代表應該是去年的資料
                        if (parsedDate > DateTime.Now)
                        {
                            parsedDate = parsedDate.AddYears(-1);
                        }
                        
                        _logger.LogDebug("從 CSV 提取到資料日期: {Date}", parsedDate.ToString("yyyy/MM/dd"));
                        return parsedDate;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析 CSV 檔案時發生錯誤");
            return null;
        }
    }

    private bool IsNumeric(string value)
    {
        return int.TryParse(value, out _);
    }
}

/// <summary>
/// CSV 日期驗證結果
/// </summary>
public class CsvDateValidationResult
{
    /// <summary>
    /// 是否驗證通過（實際日期 = 預期日期）
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 預期的資料日期
    /// </summary>
    public DateTime ExpectedDate { get; set; }

    /// <summary>
    /// CSV 中的實際資料日期
    /// </summary>
    public DateTime? ActualDate { get; set; }

    /// <summary>
    /// 警告訊息（如果驗證失敗）
    /// </summary>
    public string? Warning { get; set; }
}
