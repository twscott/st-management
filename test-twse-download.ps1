# 简单测试：直接从交易所下载 2/23 的数据
Write-Host "测试从交易所下载 2/23 数据..." -ForegroundColor Cyan

$url = "https://www.twse.com.tw/rwd/zh/afterTrading/MI_INDEX?date=20260223&type=ALL&response=csv"
Write-Host "URL: $url" -ForegroundColor Yellow

try {
    $response = Invoke-WebRequest -Uri $url -UseBasicParsing
    $content = $response.Content
    
    Write-Host "`n前 500 字符:" -ForegroundColor Green
    Write-Host $content.Substring(0, [Math]::Min(500, $content.Length))
    
    # 统计行数
    $lines = $content -split "`n"
    Write-Host "`n总行数: $($lines.Count)" -ForegroundColor Green
    
    # 找出数据行（跳过标题）
    $dataLines = $lines | Where-Object { $_ -match '^\d{4},' }
    Write-Host "数据行数: $($dataLines.Count)" -ForegroundColor Green
    
    if ($dataLines.Count -gt 0) {
        Write-Host "`n第一笔数据:" -ForegroundColor Yellow
        Write-Host $dataLines[0]
    }
} catch {
    Write-Host "下载失败: $_" -ForegroundColor Red
}
