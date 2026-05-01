using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Data;
using System.Text.RegularExpressions;

namespace SST.StockImport.Services;

public class GoodInfoImportService
{
    private readonly StockImportDbContext _context;
    private readonly ILogger<GoodInfoImportService> _logger;

    public GoodInfoImportService(
        StockImportDbContext context,
        ILogger<GoodInfoImportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public GoodInfoImportResult ImportCsv(string csvFilePath, string linkName, DateTime? dataDate = null)
    {
        var result = new GoodInfoImportResult
        {
            LinkName = linkName,
            FilePath = csvFilePath
        };

        if (!File.Exists(csvFilePath))
        {
            result.ErrorMessage = $"CSV 文件不存在: {csvFilePath}";
            _logger.LogError(result.ErrorMessage);
            return result;
        }

        try
        {
            var lines = File.ReadAllLines(csvFilePath);
            if (lines.Length < 2)
            {
                result.ErrorMessage = "CSV 文件没有数据";
                _logger.LogWarning(result.ErrorMessage);
                return result;
            }

            _logger.LogInformation("📊 [{LinkName}] 开始导入 CSV，共 {Count} 行", linkName, lines.Length);
            
            var targetDate = dataDate ?? DateTime.Today;
            var updatedCount = 0;

            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    var fields = ParseCsvLine(line);
                    if (fields.Count < 2) continue;

                    var stockId = fields.Count > 1 ? fields[1].Trim() : "";
                    if (string.IsNullOrEmpty(stockId) || stockId.Length > 10)
                        continue;

                    var tradeData = _context.TradeData
                        .FirstOrDefault(t => t.StockID == stockId && t.TransDate == targetDate);

                    if (tradeData == null)
                    {
                        tradeData = new TradeData
                        {
                            StockID = stockId,
                            TransDate = targetDate,
                            LastDate = targetDate
                        };
                        _context.TradeData.Add(tradeData);
                    }

                    UpdateFieldsByLinkName(linkName, fields, tradeData);
                    updatedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "解析 CSV 第 {Line} 行失败", i);
                }
            }

            _context.SaveChanges();

            result.IsSuccess = true;
            result.UpdatedCount = updatedCount;
            _logger.LogInformation("✅ {LinkName} 导入完成: 更新 {Count} 条记录", linkName, updatedCount);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "导入 {LinkName} 失败", linkName);
        }

        return result;
    }

    private void UpdateFieldsByLinkName(string linkName, List<string> fields, TradeData tradeData)
    {
        switch (linkName)
        {
            case "周轉率":
                if (fields.Count > 7 && decimal.TryParse(fields[7].Trim().Replace(",", ""), out var tr))
                    tradeData.TurnoverRate = tr;
                if (fields.Count > 8 && int.TryParse(fields[8].Trim().Replace(",", ""), out var td))
                    tradeData.TurnoverDiff = td;
                break;
                
            case "券資比":
                if (fields.Count > 7 && decimal.TryParse(fields[7].Trim().Replace(",", ""), out var rzr))
                    tradeData.RongziRate = rzr;
                if (fields.Count > 8 && decimal.TryParse(fields[8].Trim().Replace(",", ""), out var rqr))
                    tradeData.RongquanRate = rqr;
                break;
                
            case "投信連買":
            case "投信連賣":
                if (fields.Count > 7 && int.TryParse(fields[7].Trim().Replace(",", ""), out var ia))
                    tradeData.InvestAmt = ia;
                if (fields.Count > 8 && int.TryParse(fields[8].Trim().Replace(",", ""), out var isd))
                    tradeData.InvestSerealDays = isd;
                break;
                
            case "外资連買":
            case "外资連賣":
                if (fields.Count > 7 && int.TryParse(fields[7].Trim().Replace(",", ""), out var fa))
                    tradeData.ForeigneAmt = fa;
                if (fields.Count > 8 && int.TryParse(fields[8].Trim().Replace(",", ""), out var fsd))
                    tradeData.ForeigneSerealDays = fsd;
                break;
                
            case "MACD>0":
            case "MACD負轉正":
            case "OSC負轉正":
                if (fields.Count > 7)
                    tradeData.MACD = fields[7].Trim();
                if (fields.Count > 8)
                    tradeData.MACDNote = fields[8].Trim();
                break;
                
            case "EPS創新高":
                if (fields.Count > 16 && int.TryParse(fields[16].Trim().Replace(",", ""), out var eps))
                    tradeData.EPS = eps;
                break;
                
            case "五年新高":
                break;
                
            default:
                _logger.LogWarning("⚠️ 未知的 Link 类型: {LinkName}，跳过字段更新", linkName);
                break;
        }
    }

    private List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var regex = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
        var parts = regex.Split(line);

        foreach (var part in parts)
        {
            var cleaned = part.Trim().Replace("\"", "").Replace("=", "");
            result.Add(cleaned);
        }

        return result;
    }
}

public class GoodInfoImportResult
{
    public string LinkName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public bool IsSuccess { get; set; }
    public int UpdatedCount { get; set; }
    public string? ErrorMessage { get; set; }
}
