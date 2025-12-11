# GoodInfo 分批測試腳本
# 用途：將 19 個測試分批執行，避免觸發反爬蟲機制
# 使用：.\test-goodinfo-batches.ps1

param(
    [string]$Batch = "all"  # all, 1, 2, 3, failed
)

Write-Host "=== GoodInfo 分批測試腳本 ===" -ForegroundColor Cyan
Write-Host ""

# 切換到測試專案目錄
$testProjectPath = "d:\vibeCoding\sst\tests\GoodInfo19LinksTest"
Set-Location $testProjectPath

# 定義測試批次
$batches = @{
    "failed" = @("Test_01", "Test_02")
    "1" = @("Test_03", "Test_04", "Test_05", "Test_06", "Test_07")
    "2" = @("Test_09", "Test_10", "Test_13", "Test_14")
    "3" = @("Test_17", "Test_18")
    "passed" = @("Test_08", "Test_11", "Test_12", "Test_15", "Test_16", "Test_19")
}

function Test-Batch {
    param(
        [string]$BatchName,
        [string[]]$Tests
    )
    
    Write-Host "------------------------------------------------" -ForegroundColor Yellow
    Write-Host "批次: $BatchName" -ForegroundColor Green
    Write-Host "測試數量: $($Tests.Count)" -ForegroundColor Gray
    Write-Host "測試項目: $($Tests -join ', ')" -ForegroundColor Gray
    Write-Host ""
    
    $filter = $Tests -join "|"
    
    Write-Host "執行命令: dotnet test --filter `"$filter`"" -ForegroundColor Cyan
    Write-Host ""
    
    $startTime = Get-Date
    dotnet test --filter $filter --logger "console;verbosity=normal"
    $endTime = Get-Date
    $duration = $endTime - $startTime
    
    Write-Host ""
    Write-Host "批次 $BatchName 完成" -ForegroundColor Green
    Write-Host "執行時間: $($duration.ToString('mm\:ss'))" -ForegroundColor Gray
    Write-Host ""
}

# 根據參數執行測試
switch ($Batch) {
    "all" {
        Write-Host "執行全部測試（分批執行，每批間隔 2 分鐘）" -ForegroundColor Magenta
        Write-Host ""
        
        # 先測試失敗的項目（修正後）
        Write-Host "階段 0: 測試之前失敗的項目（修正後）" -ForegroundColor Yellow
        Test-Batch "failed" $batches["failed"]
        Write-Host "等待 2 分鐘後執行下一批..." -ForegroundColor Yellow
        Start-Sleep -Seconds 120
        
        # 批次 1
        Write-Host "階段 1: 測試第一批未測試項目" -ForegroundColor Yellow
        Test-Batch "1" $batches["1"]
        Write-Host "等待 2 分鐘後執行下一批..." -ForegroundColor Yellow
        Start-Sleep -Seconds 120
        
        # 批次 2
        Write-Host "階段 2: 測試第二批未測試項目" -ForegroundColor Yellow
        Test-Batch "2" $batches["2"]
        Write-Host "等待 2 分鐘後執行下一批..." -ForegroundColor Yellow
        Start-Sleep -Seconds 120
        
        # 批次 3
        Write-Host "階段 3: 測試第三批未測試項目" -ForegroundColor Yellow
        Test-Batch "3" $batches["3"]
        Write-Host "等待 2 分鐘後執行下一批..." -ForegroundColor Yellow
        Start-Sleep -Seconds 120
        
        # 驗證之前通過的項目
        Write-Host "階段 4: 驗證之前已通過的項目" -ForegroundColor Yellow
        Test-Batch "passed" $batches["passed"]
    }
    
    "failed" {
        Write-Host "僅測試之前失敗的項目（Test_01, Test_02）" -ForegroundColor Magenta
        Test-Batch "failed" $batches["failed"]
    }
    
    "1" {
        Write-Host "執行批次 1（Test_03 ~ Test_07）" -ForegroundColor Magenta
        Test-Batch "1" $batches["1"]
    }
    
    "2" {
        Write-Host "執行批次 2（Test_09, Test_10, Test_13, Test_14）" -ForegroundColor Magenta
        Test-Batch "2" $batches["2"]
    }
    
    "3" {
        Write-Host "執行批次 3（Test_17, Test_18）" -ForegroundColor Magenta
        Test-Batch "3" $batches["3"]
    }
    
    "passed" {
        Write-Host "驗證之前已通過的項目" -ForegroundColor Magenta
        Test-Batch "passed" $batches["passed"]
    }
    
    default {
        Write-Host "錯誤: 未知的批次 '$Batch'" -ForegroundColor Red
        Write-Host ""
        Write-Host "可用的批次選項：" -ForegroundColor Yellow
        Write-Host "  all     - 執行全部測試（分批，推薦）"
        Write-Host "  failed  - 僅測試失敗項目 (Test_01, Test_02)"
        Write-Host "  1       - 批次 1 (Test_03~07)"
        Write-Host "  2       - 批次 2 (Test_09, 10, 13, 14)"
        Write-Host "  3       - 批次 3 (Test_17, 18)"
        Write-Host "  passed  - 驗證已通過項目"
        Write-Host ""
        Write-Host "使用範例：" -ForegroundColor Cyan
        Write-Host "  .\test-goodinfo-batches.ps1 -Batch all"
        Write-Host "  .\test-goodinfo-batches.ps1 -Batch failed"
        Write-Host "  .\test-goodinfo-batches.ps1 -Batch 1"
    }
}

Write-Host ""
Write-Host "=== 測試完成 ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "提醒：" -ForegroundColor Yellow
Write-Host "  - 兩個測試間必須間隔 10 秒以上（程式內已處理）"
Write-Host "  - 如需重新測試，請等待 5 分鐘後再執行"
Write-Host "  - 檢查下載目錄確認檔案是否成功下載"
