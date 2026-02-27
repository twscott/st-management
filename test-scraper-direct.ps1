# 直接测试 TWSEScraper 能否下载 2/23 的数据

Write-Host "测试直接下载交易所 CSV..." -ForegroundColor Cyan

# TSE (上市) CSV URL
$tseUrl = "https://www.twse.com.tw/rwd/zh/afterTrading/MI_INDEX?date=20260223&type=ALL&response=csv"
Write-Host "`n1. 下载 TSE (上市) CSV..." -ForegroundColor Yellow

try {
    $response = Invoke-WebRequest -Uri $tseUrl -UseBasicParsing
    $lines = ($response.Content -split "`n")
    $dataLines = $lines | Where-Object { $_ -match '^\d{4},' }
    
    Write-Host "   TSE 总行数: $($lines.Count)" -ForegroundColor Green
    Write-Host "   TSE 数据行: $($dataLines.Count)" -ForegroundColor Green
    
    if ($dataLines.Count -gt 0) {
        Write-Host "   第一笔: $($dataLines[0].Substring(0, [Math]::Min(80, $dataLines[0].Length)))" -ForegroundColor Gray
    }
} catch {
    Write-Host "   TSE 下载失败: $_" -ForegroundColor Red
}

# OTC (上柜) JSON URL  
$otcUrl = "https://www.tpex.org.tw/openapi/v1/tpex_mainboard_daily_close_quotes"
Write-Host "`n2. 下载 OTC (上柜) JSON..." -ForegroundColor Yellow

try {
    $otcResponse = Invoke-RestMethod -Uri $otcUrl -UseBasicParsing
    $otcCount = if ($otcResponse -is [Array]) { $otcResponse.Count } else { $otcResponse.data.Count }
    
    Write-Host "   OTC 数据笔数: $otcCount" -ForegroundColor Green
    if ($otcCount -gt 0) {
        $first = if ($otcResponse -is [Array]) { $otcResponse[0] } else { $otcResponse.data[0] }
        Write-Host "   第一笔代码: $($first.Code)" -ForegroundColor Gray
    }
} catch {
    Write-Host "   OTC 下载失败: $_" -ForegroundColor Red
}

Write-Host "`n总结: 交易所数据是否可访问" -ForegroundColor Cyan
