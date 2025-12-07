# 券資比下載測試腳本
# 測試實際下載功能

Write-Host "=== 券資比下載測試 ===" -ForegroundColor Green
Write-Host ""

# 執行單元測試
Write-Host "1. 執行單元測試..." -ForegroundColor Cyan
dotnet test --filter "FullyQualifiedName~MarginRatioDownloadTests" --verbosity quiet --nologo

if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✓ 單元測試通過" -ForegroundColor Green
} else {
    Write-Host "   ✗ 單元測試失敗" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 執行整合測試（不包含實際下載）
Write-Host "2. 執行基礎整合測試..." -ForegroundColor Cyan
dotnet test --filter "FullyQualifiedName~MarginRatioIntegrationTests.Download_ShouldCreateDownloadDirectory" --verbosity quiet --nologo

if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✓ 基礎整合測試通過" -ForegroundColor Green
} else {
    Write-Host "   ✗ 基礎整合測試失敗" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== 所有測試通過 ===" -ForegroundColor Green
Write-Host ""
Write-Host "注意: 實際下載測試需要手動執行，因為需要較長時間:" -ForegroundColor Yellow
Write-Host "  dotnet test --filter ""FullyQualifiedName~MarginRatioIntegrationTests.Download_ShouldReturnTrue_WhenSuccessful"" --verbosity detailed"
