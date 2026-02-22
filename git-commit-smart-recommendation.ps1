# Git Commit Script for Smart Recommendation Feature

Write-Host "=== Adding Smart Recommendation Files to Git ===" -ForegroundColor Cyan

# Core feature files
Write-Host "`n[1/4] Adding DTO files..." -ForegroundColor Yellow
git add src/SST.StockImport.Core/DTOs/SmartRecommendation/
git add src/SST.StockImport.Core/Interfaces/ISmartRecommendationService.cs

Write-Host "[2/4] Adding Service and Controller..." -ForegroundColor Yellow
git add src/SST.StockImport.Infrastructure/Services/SmartRecommendationService.cs
git add src/SST.StockImport.API/Controllers/SmartRecommendationController.cs

Write-Host "[3/4] Adding UI and Configuration..." -ForegroundColor Yellow
git add src/SST.StockImport.Web/Components/Pages/SmartRecommendation.razor
git add src/SST.StockImport.Infrastructure/ServiceCollectionExtensions.cs
git add src/SST.StockImport.Web/Components/Layout/NavMenu.razor

Write-Host "[4/4] Adding Documentation and Scripts..." -ForegroundColor Yellow
git add SMART-RECOMMENDATION-GUIDE.md
git add test-smart-recommendation-api.ps1
git add test-smart-recommendation-complete.ps1
git add docs/Todo/20260221_2215_SessionReport.md

Write-Host "`n=== Staging Complete ===" -ForegroundColor Green
Write-Host "Files added. Review with: git status" -ForegroundColor Cyan

Write-Host "`n=== Ready to Commit ===" -ForegroundColor Yellow
Write-Host "Run this command in Git Bash or WSL:" -ForegroundColor White
Write-Host ""
Write-Host 'git commit -m "feat: Smart Recommendation System (Maturity Score + Backtest)' -ForegroundColor Gray
Write-Host 'New SmartRecommendation module with maturity score algorithm' -ForegroundColor Gray
Write-Host 'Supports today and historical date recommendations' -ForegroundColor Gray
Write-Host 'Historical backtest: track 60-day actual performance' -ForegroundColor Gray
Write-Host 'API endpoints: GET /api/SmartRecommendation/today and /{date}' -ForegroundColor Gray
Write-Host 'Blazor UI with date picker, stats panel, recommendation cards' -ForegroundColor Gray
Write-Host 'Documentation: SMART-RECOMMENDATION-GUIDE.md and Session Report"' -ForegroundColor Gray

Write-Host "`n=== Next Steps ===" -ForegroundColor Cyan
Write-Host "1. Review: git status" -ForegroundColor White
Write-Host "2. Commit: Use command above (in Git Bash)" -ForegroundColor White
Write-Host "3. Push: git push origin main" -ForegroundColor White

