#!/usr/bin/env pwsh

<#
.SYNOPSIS
    執行完整的分層測試架構
.DESCRIPTION
    按照用戶指定的測試層次順序執行所有測試：
    單元測試 -> 無伺服器整合測試 -> 有伺服器的整合測試 -> 整合前台測試 -> 出 Sandbox 測試 -> UAT
.EXAMPLE
    .\Run-LayeredTests.ps1
    .\Run-LayeredTests.ps1 -Layer "Unit"
    .\Run-LayeredTests.ps1 -SkipPlaywright
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("All", "Unit", "NoServer", "WithServer", "Frontend", "Sandbox", "UAT")]
    [string]$Layer = "All",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipPlaywright,
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

# 設置錯誤處理
$ErrorActionPreference = "Stop"

# 測試層定義
$TestLayers = @{
    "Unit" = @{
        Name = "第一層：單元測試"
        Description = "測試個別組件和服務的功能"
        TestFilter = "Category=Unit"
        Projects = @("SST.StockImport.Tests")
    }
    "NoServer" = @{
        Name = "第二層：無伺服器整合測試"
        Description = "測試服務間協作，不啟動Web服務器"
        TestFilter = "Category=NoServer|FullyQualifiedName~NoServer"
        Projects = @("SST.StockImport.IntegrationTest")
    }
    "WithServer" = @{
        Name = "第三層：有伺服器整合測試"
        Description = "啟動完整Web API伺服器測試"
        TestFilter = "Category=WithServer|FullyQualifiedName~WithServer"
        Projects = @("SST.StockImport.IntegrationTest")
    }
    "Frontend" = @{
        Name = "第四層：整合前台測試"
        Description = "使用Playwright測試前端UI交互"
        TestFilter = "Category=Frontend|FullyQualifiedName~Frontend"
        Projects = @("SST.StockImport.IntegrationTest")
        RequiresPlaywright = $true
    }
    "Sandbox" = @{
        Name = "第五層：Sandbox測試"
        Description = "類生產環境測試"
        TestFilter = "Category=Sandbox|FullyQualifiedName~Sandbox"
        Projects = @("SST.StockImport.IntegrationTest")
        RequiresConfig = $true
    }
    "UAT" = @{
        Name = "第六層：UAT測試"
        Description = "用戶驗收測試，生產環境測試"
        TestFilter = "Category=UAT|FullyQualifiedName~UAT"
        Projects = @("SST.StockImport.IntegrationTest")
        RequiresConfig = $true
        IsProduction = $true
    }
}

# 測試執行順序
$TestOrder = @("Unit", "NoServer", "WithServer", "Frontend", "Sandbox", "UAT")

function Write-TestHeader {
    param($LayerInfo)
    
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host " $($LayerInfo.Name)" -ForegroundColor Yellow
    Write-Host " $($LayerInfo.Description)" -ForegroundColor Gray
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Test-Prerequisites {
    param($LayerKey, $LayerInfo)
    
    Write-Host "🔍 檢查測試前提條件..." -ForegroundColor Blue
    
    # 檢查Playwright
    if ($LayerInfo.RequiresPlaywright -and -not $SkipPlaywright) {
        Write-Host "  - 檢查Playwright安裝..." -ForegroundColor Gray
        try {
            & pwsh -Command "npx playwright --version" 2>$null
            if ($LASTEXITCODE -ne 0) {
                throw "Playwright未安裝"
            }
            Write-Host "  ✅ Playwright已安裝" -ForegroundColor Green
        }
        catch {
            Write-Warning "Playwright未安裝，將嘗試安裝..."
            & npx playwright install
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Playwright安裝失敗"
                return $false
            }
        }
    }
    
    # 檢查配置文件
    if ($LayerInfo.RequiresConfig) {
        $configFile = if ($LayerKey -eq "Sandbox") { "appsettings.sandbox.json" } else { "appsettings.uat.json" }
        $configPath = Join-Path "tests\SST.StockImport.IntegrationTest" $configFile
        
        if (-not (Test-Path $configPath)) {
            Write-Warning "配置文件不存在: $configPath"
            if ($LayerInfo.IsProduction) {
                Write-Error "UAT測試需要正確的生產環境配置"
                return $false
            }
        } else {
            Write-Host "  ✅ 配置文件存在: $configFile" -ForegroundColor Green
        }
    }
    
    # 檢查項目文件
    foreach ($project in $LayerInfo.Projects) {
        $projectPath = "tests\$project\$project.csproj"
        if (-not (Test-Path $projectPath)) {
            Write-Error "測試項目不存在: $projectPath"
            return $false
        }
    }
    
    return $true
}

function Invoke-TestLayer {
    param($LayerKey, $LayerInfo)
    
    Write-TestHeader -LayerInfo $LayerInfo
    
    # 檢查前提條件
    if (-not (Test-Prerequisites -LayerKey $LayerKey -LayerInfo $LayerInfo)) {
        Write-Error "前提條件檢查失敗，跳過 $($LayerInfo.Name)"
        return $false
    }
    
    $success = $true
    
    foreach ($project in $LayerInfo.Projects) {
        Write-Host "📦 執行項目: $project" -ForegroundColor Cyan
        
        $projectPath = "tests\$project"
        $testCommand = "dotnet test `"$projectPath`" --configuration Release --logger trx --results-directory TestResults"
        
        if ($LayerInfo.TestFilter) {
            $testCommand += " --filter `"$($LayerInfo.TestFilter)`""
        }
        
        if ($Verbose) {
            $testCommand += " --verbosity detailed"
        }
        
        Write-Host "🚀 執行命令: $testCommand" -ForegroundColor Gray
        
        try {
            Invoke-Expression $testCommand
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ $project - 所有測試通過" -ForegroundColor Green
            } else {
                Write-Host "❌ $project - 測試失敗 (退出碼: $LASTEXITCODE)" -ForegroundColor Red
                $success = $false
            }
        }
        catch {
            Write-Host "💥 $project - 測試執行異常: $($_.Exception.Message)" -ForegroundColor Red
            $success = $false
        }
    }
    
    return $success
}

function Show-Summary {
    param($Results)
    
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host " 測試執行摘要" -ForegroundColor Yellow
    Write-Host "================================================================" -ForegroundColor Cyan
    
    $totalLayers = $Results.Count
    $passedLayers = ($Results.Values | Where-Object { $_ -eq $true }).Count
    $failedLayers = $totalLayers - $passedLayers
    
    foreach ($layerKey in $TestOrder) {
        if ($Results.ContainsKey($layerKey)) {
            $status = if ($Results[$layerKey]) { "✅ 通過" } else { "❌ 失敗" }
            $color = if ($Results[$layerKey]) { "Green" } else { "Red" }
            Write-Host "  $($TestLayers[$layerKey].Name): $status" -ForegroundColor $color
        }
    }
    
    Write-Host ""
    Write-Host "總結: $passedLayers/$totalLayers 層測試通過" -ForegroundColor $(if ($failedLayers -eq 0) { "Green" } else { "Yellow" })
    
    if ($failedLayers -eq 0) {
        Write-Host "🎉 所有測試層都已成功完成！" -ForegroundColor Green
    } else {
        Write-Host "⚠️  有 $failedLayers 層測試失敗，請檢查上述輸出" -ForegroundColor Yellow
    }
}

# 主執行邏輯
try {
    Write-Host "🚀 開始執行分層測試架構" -ForegroundColor Green
    Write-Host "測試層次: $Layer" -ForegroundColor Gray
    Write-Host "跳過Playwright: $SkipPlaywright" -ForegroundColor Gray
    Write-Host ""
    
    # 確保在正確的目錄
    if (-not (Test-Path "SST.StockImport.sln")) {
        throw "請在包含 SST.StockImport.sln 的目錄中執行此腳本"
    }
    
    # 先構建解決方案
    Write-Host "🔨 構建解決方案..." -ForegroundColor Blue
    & dotnet build --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "解決方案構建失敗"
    }
    Write-Host "✅ 構建成功" -ForegroundColor Green
    
    $results = @{}
    
    if ($Layer -eq "All") {
        # 執行所有層
        foreach ($layerKey in $TestOrder) {
            $layerInfo = $TestLayers[$layerKey]
            
            # 跳過Playwright測試如果指定
            if ($layerInfo.RequiresPlaywright -and $SkipPlaywright) {
                Write-Host "⏭️  跳過 $($layerInfo.Name)（Playwright已禁用）" -ForegroundColor Yellow
                continue
            }
            
            $results[$layerKey] = Invoke-TestLayer -LayerKey $layerKey -LayerInfo $layerInfo
            
            # 如果當前層失敗且是依賴性測試，詢問是否繼續
            if (-not $results[$layerKey] -and $layerKey -in @("Unit", "NoServer")) {
                $continue = Read-Host "當前層測試失敗，是否繼續下一層？(y/N)"
                if ($continue -notmatch "^[Yy]") {
                    Write-Host "用戶選擇停止測試" -ForegroundColor Yellow
                    break
                }
            }
        }
    } else {
        # 執行指定層
        if ($TestLayers.ContainsKey($Layer)) {
            $layerInfo = $TestLayers[$Layer]
            $results[$Layer] = Invoke-TestLayer -LayerKey $Layer -LayerInfo $layerInfo
        } else {
            throw "無效的測試層: $Layer"
        }
    }
    
    # 顯示摘要
    Show-Summary -Results $results
    
    # 返回適當的退出碼
    $failedCount = ($results.Values | Where-Object { $_ -eq $false }).Count
    exit $failedCount
}
catch {
    Write-Host "💥 測試執行失敗: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}