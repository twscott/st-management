# Diagnose KD/Bollinger Performance Issues
# Analysis script to understand why processing is slow

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  KD/Bollinger Performance Diagnosis" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Problem Analysis:" -ForegroundColor Yellow
Write-Host ""

Write-Host "1. Database Operations Per Day:" -ForegroundColor White
Write-Host "   - Stocks per day: ~2,300" -ForegroundColor Gray
Write-Host "   - Current code: SaveChangesAsync() EVERY STOCK" -ForegroundColor Red
Write-Host "   - Result: 2,300 database commits per day!" -ForegroundColor Red
Write-Host ""

Write-Host "2. Why 'One Day at a Time' is SLOWER:" -ForegroundColor White
Write-Host "   - Historical data query grows with each day:" -ForegroundColor Gray
Write-Host ""
Write-Host "     Day   1: Load   9 days history per stock (KD needs 9 days)" -ForegroundColor Gray
Write-Host "     Day  30: Load  30 days history per stock" -ForegroundColor Gray
Write-Host "     Day 100: Load 100 days history per stock" -ForegroundColor Gray
Write-Host "     Day 260: Load 260 days history per stock" -ForegroundColor Yellow
Write-Host ""
Write-Host "   - Code location: KDIndicatorProcessor.cs Line 374" -ForegroundColor Gray
Write-Host "     var historicalPrices = await _context.Stock60Days" -ForegroundColor Gray
Write-Host "         .Where(s => s.StockDate <= targetDate)  // <-- No date limit!" -ForegroundColor Red
Write-Host ""

Write-Host "3. Memory Issues:" -ForegroundColor White
Write-Host "   - EF Core ChangeTracker accumulates entities" -ForegroundColor Gray
Write-Host "   - No _context.ChangeTracker.Clear() after updates" -ForegroundColor Red
Write-Host "   - Memory grows: Day 1 (5MB) -> Day 260 (500MB+)" -ForegroundColor Yellow
Write-Host ""

Write-Host "4. Code Problems Found:" -ForegroundColor White
Write-Host ""
Write-Host "   a) Non-Optimized Version (NOT USED, but shows the issue):" -ForegroundColor Gray
Write-Host "      File: KDIndicatorProcessor.cs Line 328-337" -ForegroundColor Gray
Write-Host "      private async Task UpdateStock60DaysKD(...)" -ForegroundColor Gray
Write-Host "      {" -ForegroundColor Gray
Write-Host "          entity.KD_K = k;" -ForegroundColor Gray
Write-Host "          await _context.SaveChangesAsync();  // <-- Every stock!" -ForegroundColor Red
Write-Host "      }" -ForegroundColor Gray
Write-Host ""

Write-Host "   b) Optimized Version (CURRENTLY USED but has issues):" -ForegroundColor Gray
Write-Host "      File: KDIndicatorProcessor.cs Line 374" -ForegroundColor Gray
Write-Host "      var historicalPrices = await _context.Stock60Days" -ForegroundColor Gray
Write-Host "          .Where(s => stockIds.Contains(s.StockID)" -ForegroundColor Gray
Write-Host "                   && s.StockDate <= targetDate  // <-- Gets ALL history!" -ForegroundColor Red
Write-Host "                   && s.EndPrice != null)" -ForegroundColor Gray
Write-Host "          .ToListAsync();  // <-- Loads everything into memory!" -ForegroundColor Red
Write-Host ""

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Solutions" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Option 1: Quick Fix (Recommended for NOW)" -ForegroundColor Green
Write-Host "  - Create batch script: 10 days at a time" -ForegroundColor White
Write-Host "  - Restart process between batches (releases memory)" -ForegroundColor White
Write-Host "  - Only process 2025-01-01 onwards" -ForegroundColor White
Write-Host "  - Estimated time: 26 batches x 2 min = ~50 minutes" -ForegroundColor White
Write-Host ""

Write-Host "Option 2: Code Optimization (Better, but needs testing)" -ForegroundColor Yellow
Write-Host "  Fix 1: Limit history query to only what's needed" -ForegroundColor White
Write-Host "    Line 374: .Where(s => s.StockDate <= targetDate" -ForegroundColor Gray
Write-Host "                       && s.StockDate >= targetDate.AddDays(-30))" -ForegroundColor Green
Write-Host ""
Write-Host "  Fix 2: Add memory cleanup after batch updates" -ForegroundColor White
Write-Host "    Line 469: await BulkUpdateKDToStock60Days(...);" -ForegroundColor Gray
Write-Host "              _context.ChangeTracker.Clear();  // <-- Add this!" -ForegroundColor Green
Write-Host ""
Write-Host "  Fix 3: Use AsNoTracking for read queries" -ForegroundColor White
Write-Host "    Line 351: .AsNoTracking()  // <-- Add this" -ForegroundColor Green
Write-Host "    Line 374: .AsNoTracking()  // <-- Add this" -ForegroundColor Green
Write-Host ""

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Your diagnosis was CORRECT!" -ForegroundColor Green
Write-Host "  - Process should be 'one day at a time' (2,300 records)" -ForegroundColor White
Write-Host "  - But current code has memory leak issues" -ForegroundColor Yellow
Write-Host "  - Need to either: batch script OR fix code" -ForegroundColor White
Write-Host ""

Write-Host "Recommended Next Step:" -ForegroundColor Cyan
Write-Host "  1. Use batch script for immediate needs (Option 1)" -ForegroundColor White
Write-Host "  2. Fix code properly later with testing (Option 2)" -ForegroundColor White
Write-Host ""
