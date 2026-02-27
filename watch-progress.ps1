# 实时监控 KD & Bollinger 批次计算进度
# 每 30 秒自动刷新一次

while ($true) {
    Clear-Host
    
    Write-Host "`n=========================================" -ForegroundColor Cyan
    Write-Host "  KD & Bollinger 批次计算进度" -ForegroundColor Cyan
    Write-Host "  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
    Write-Host "=========================================`n" -ForegroundColor Cyan
    
    # 查询已完成进度
    d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe -uroot -e @"
USE sst;
SELECT 
    MAX(StockDate) as '已完成到日期',
    COUNT(DISTINCT StockDate) as '已处理天数',
    CONCAT(ROUND(COUNT(DISTINCT StockDate) / 268 * 100, 1), '%') as '完成度'
FROM stock60days 
WHERE StockDate >= '2025-01-01' 
  AND KD_K IS NOT NULL 
  AND KD_K > 0;
"@
    
    Write-Host ""
    Write-Host "目标: 2025-01-02 ~ 2026-02-11 (共 268 天)" -ForegroundColor Gray
    Write-Host ""
    
    # 显示缺少的日期数量
    $missingDates = d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe -uroot -sN -e @"
USE sst;
SELECT COUNT(DISTINCT StockDate)
FROM stock60days 
WHERE StockDate >= '2025-01-01' 
  AND (KD_K IS NULL OR KD_K = 0);
"@
    
    if ($missingDates -eq 0) {
        Write-Host "✓ 全部完成!" -ForegroundColor Green
        break
    }
    else {
        Write-Host "还剩 $missingDates 天待处理..." -ForegroundColor Yellow
    }
    
    Write-Host "`n按 Ctrl+C 停止监控" -ForegroundColor DarkGray
    
    Start-Sleep -Seconds 30
}
