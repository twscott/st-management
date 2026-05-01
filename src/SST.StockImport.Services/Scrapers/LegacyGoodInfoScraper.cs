using Microsoft.Extensions.Logging;

namespace SST.StockImport.Services.Scrapers
{
    /// <summary>
    /// GoodInfo.tw 爬蟲服務 - 完全復刻舊系統的成功實作
    /// 基於 D:\mywork\sstStock\TaskTrayApplication\_1_每日收盤匯入.cs
    /// linkLabel9_LinkClicked 方法和 CommonClass.goodInfodownload 方法
    /// </summary>
    public class LegacyGoodInfoScraper
    {
        private readonly ILogger<LegacyGoodInfoScraper> _logger;
        private readonly GoodInfoImportService _importService;
        private readonly GoodInfoScraper _goodInfoScraper;
        private readonly string _backupPath;

        public LegacyGoodInfoScraper(
            ILogger<LegacyGoodInfoScraper> logger,
            GoodInfoImportService importService,
            GoodInfoScraper goodInfoScraper)
        {
            _logger = logger;
            _importService = importService;
            _goodInfoScraper = goodInfoScraper;
            _backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "GoodInfoBackups");
            if (!Directory.Exists(_backupPath))
            {
                Directory.CreateDirectory(_backupPath);
            }
        }

        /// <summary>
        /// 舊系統週轉率下載方法的完全復刻
        /// 對應 linkLabel9_LinkClicked 和 downloadGoodInfo 方法
        /// </summary>
        public async Task<bool> DownloadTurnoverDataAsync(string url, string cssSelector)
        {
            _logger.LogInformation("開始下載 GoodInfo 資料: {Url}", url);
            try
            {
                var request = new GoodInfoDownloadRequest
                {
                    Name = "Download",
                    Url = url,
                    CssSelector = cssSelector
                };
                return await _goodInfoScraper.DownloadDataAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下載 GoodInfo 資料失敗: {Url}", url);
                return false;
            }
        }

        /// <summary>
        /// 批次執行所有 GoodInfo 下載任務，實現容錯機制
        /// </summary>
        public async Task<BatchDownloadResult> ExecuteBatchDownloadAsync()
        {
            var allRequests = GoodInfoUrlConfig.GetAllRequests();
            var results = new List<DownloadResult>();
            var startTime = DateTime.Now;
            
            _logger.LogInformation($"開始批次下載 {allRequests.Count} 個 GoodInfo 項目");
            
            foreach (var request in allRequests)
            {
                try
                {
                    _logger.LogInformation($"正在下載: {request.Name} (預期成功率: {request.ExpectedSuccessRate}%)");
                    
                    var downloadResult = await DownloadTurnoverDataAsync(request.Url, request.CssSelector ?? "#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)");
                    results.Add(new DownloadResult
                    {
                        ItemName = request.Name,
                        IsSuccess = downloadResult,
                        ErrorMessage = downloadResult ? null : "下載失敗",
                        Duration = DateTime.Now - startTime
                    });
                    
                    if (downloadResult)
                    {
                        _logger.LogInformation($"✅ {request.Name} 下載成功，準備導入數據庫...");
                        
                        var (saved, imported, filePath) = SaveAndImportCsv(request.Name);
                        
                        if (saved && imported)
                        {
                            _logger.LogInformation($"✅ {request.Name} 導入數據庫成功");
                        }
                        else if (saved && !imported)
                        {
                            _logger.LogWarning($"⚠️ {request.Name} 保存成功但導入失敗");
                        }
                        else
                        {
                            _logger.LogWarning($"⚠️ {request.Name} 保存或導入失敗");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"❌ {request.Name} 下載失敗，繼續下一項");
                    }
                    
                    // 防止被判定為爬蟲：間隔 10 秒（與舊系統 extraWait 一致）
                    _logger.LogInformation($"⏳ 等待 10 秒後處理下一個項目...");
                    await Task.Delay(10000);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ {request.Name} 下載發生例外，繼續下一項");
                    results.Add(new DownloadResult
                    {
                        ItemName = request.Name,
                        IsSuccess = false,
                        ErrorMessage = ex.Message,
                        Duration = DateTime.Now - startTime
                    });
                    
                    // 發生例外時等待 10 秒
                    _logger.LogInformation($"⏳ 等待 10 秒後處理下一個項目...");
                    await Task.Delay(10000);
                }
            }
            
            var successCount = results.Count(r => r.IsSuccess);
            var totalDuration = DateTime.Now - startTime;
            
            _logger.LogInformation($"批次下載完成: {successCount}/{results.Count} 成功，耗時 {totalDuration.TotalMinutes:F1} 分鐘");
            
            return new BatchDownloadResult
            {
                TotalItems = results.Count,
                SuccessfulItems = successCount,
                FailedItems = results.Count - successCount,
                Results = results,
                TotalDuration = totalDuration,
                StartTime = startTime,
                EndTime = DateTime.Now
            };
        }
        
        /// <summary>
        /// 執行今日待完成的下載任務
        /// </summary>
        public async Task<BatchDownloadResult> ExecutePendingDownloadAsync()
        {
            var pendingRequests = GoodInfoUrlConfig.GetPendingRequests();
            
            if (!pendingRequests.Any())
            {
                _logger.LogInformation("今日所有 GoodInfo 下載任務已完成");
                return new BatchDownloadResult
                {
                    TotalItems = 0,
                    SuccessfulItems = 0,
                    FailedItems = 0,
                    Results = new List<DownloadResult>(),
                    TotalDuration = TimeSpan.Zero,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now
                };
            }
            
            _logger.LogInformation($"執行今日待完成的 {pendingRequests.Count} 項 GoodInfo 下載");
            return await ExecuteBatchDownloadAsync();
        }

        /// <summary>
        /// 保存下載的 CSV 到備份資料夾，並導入數據庫
        /// </summary>
        private (bool saved, bool imported, string filePath) SaveAndImportCsv(string linkName)
        {
            _logger.LogInformation("🔍 開始搜索 CSV 文件...");
            
            // 首先檢查用戶下載目錄
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var downloadPath = Path.Combine(userProfile, "Downloads");
            _logger.LogInformation("🔍 搜索目錄: {Path}", downloadPath);
            
            // 查找最新的 CSV 文件
            var sourceCsvPath = FindLatestCsvFile(downloadPath, linkName);
            
            if (sourceCsvPath == null)
            {
                // 嘗試其他可能的下載位置
                var alternativePaths = new[]
                {
                    @"D:\vibeCoding\sst\GoodInfoDownloads",
                    @"D:\ChromeUserData\Default\Downloads",
                    Path.GetTempPath(),
                    @"C:\Users\PC\AppData\Local\Temp",
                    @"D:\Users\PC\AppData\Local\Temp"
                };
                
                foreach (var altPath in alternativePaths)
                {
                    _logger.LogInformation("🔍 搜索備用目錄: {Path}", altPath);
                    if (Directory.Exists(altPath))
                    {
                        sourceCsvPath = FindLatestCsvFile(altPath, linkName);
                        if (sourceCsvPath != null)
                        {
                            _logger.LogInformation("✅ 在備用目錄找到 CSV: {Path}", sourceCsvPath);
                            break;
                        }
                    }
                }
            }
            
            if (sourceCsvPath == null || !File.Exists(sourceCsvPath))
            {
                _logger.LogWarning("⚠️ CSV 文件不存在: {Path}, {DownloadPath}", sourceCsvPath ?? "null", downloadPath);
                return (false, false, "");
            }

            try
            {
                // 複製文件到備份目錄
                var dateStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var safeLinkName = linkName.Replace(" ", "_").Replace("/", "_");
                var fileName = $"{safeLinkName}_{dateStr}.csv";
                var backupFilePath = Path.Combine(_backupPath, fileName);

                File.Copy(sourceCsvPath, backupFilePath, true);
                _logger.LogInformation("💾 已保存 CSV: {Path}", backupFilePath);

                // 導入數據庫
                var importResult = _importService.ImportCsv(backupFilePath, linkName);

                return (true, importResult.IsSuccess, backupFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 保存或導入 CSV 失敗: {LinkName}", linkName);
                return (false, false, "");
            }
        }
        
        private string? FindLatestCsvFile(string directory, string? linkName = null)
        {
            if (!Directory.Exists(directory)) 
            {
                _logger.LogWarning("⚠️ 目錄不存在: {Dir}", directory);
                return null;
            }
            
            try
            {
                // 搜索最近 10 分鐘內修改的 CSV 文件
                var cutoffTime = DateTime.Now.AddMinutes(-10);
                var csvFiles = Directory.GetFiles(directory, "*.csv")
                    .Select(f => new FileInfo(f))
                    .Where(f => f.LastWriteTime > cutoffTime)
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();
                    
                if (csvFiles.Any())
                {
                    _logger.LogInformation("📂 在 {Dir} 中找到 {Count} 个最近的 CSV 文件 (最近修改: {Time})", 
                        directory, csvFiles.Count, csvFiles.First().LastWriteTime.ToString("HH:mm:ss"));
                    foreach (var f in csvFiles.Take(3))
                    {
                        _logger.LogInformation("   - {File} ({Size} bytes, {Time})", 
                            f.Name, f.Length, f.LastWriteTime.ToString("HH:mm:ss"));
                    }
                    return csvFiles.First().FullName;
                }
                else
                {
                    // 如果沒有最近的文件，則返回目錄中最後一個文件
                    var allCsvFiles = Directory.GetFiles(directory, "*.csv")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.LastWriteTime)
                        .ToList();
                    
                    if (allCsvFiles.Any())
                    {
                        _logger.LogWarning("⚠️ 目錄中沒有最近 10 分鐘內的 CSV 文件，最後一個是: {File} ({Time})", 
                            allCsvFiles.First().Name, allCsvFiles.First().LastWriteTime.ToString("HH:mm:ss"));
                        return allCsvFiles.First().FullName;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索 CSV 文件失敗: {Dir}", directory);
            }
            
            return null;
        }
    }
    
    /// <summary>
    /// 批次下載結果
    /// </summary>
    public class BatchDownloadResult
    {
        public int TotalItems { get; set; }
        public int SuccessfulItems { get; set; }
        public int FailedItems { get; set; }
        public List<DownloadResult> Results { get; set; } = new();
        public TimeSpan TotalDuration { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
    
    /// <summary>
    /// 單項下載結果
    /// </summary>
    public class DownloadResult
    {
        public string ItemName { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
