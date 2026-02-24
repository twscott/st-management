# Test Database Switcher Feature
# Verifies the new database switching functionality

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Database Switcher Feature Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$apiUrl = "http://localhost:5008"

# Test 1: Check current connection
Write-Host "[Test 1] Getting current database connection..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$apiUrl/api/database/current-connection" -Method Get
    Write-Host "  OK Current Database: $($response.DatabaseName)" -ForegroundColor Green
    Write-Host "  OK Is Production: $($response.IsProduction)" -ForegroundColor $(if ($response.IsProduction) {"Red"} else {"Green"})
    Write-Host "  OK Connection String: $($response.ConnectionString)" -ForegroundColor Gray
} catch {
    Write-Host "  ERROR Failed to get current connection: $_" -ForegroundColor Red
}
Write-Host ""

# Test 2: Verify appsettings.json
Write-Host "[Test 2] Verifying appsettings.json..." -ForegroundColor Yellow
$appSettingsPath = "src\SST.StockImport.API\appsettings.json"
if (Test-Path $appSettingsPath) {
    $settings = Get-Content $appSettingsPath | ConvertFrom-Json
    $connString = $settings.ConnectionStrings.DefaultConnection
    if ($connString -match "Database=(\w+)") {
        $dbName = $Matches[1]
        Write-Host "  OK appsettings.json -> Database: $dbName" -ForegroundColor $(if ($dbName -eq "sst") {"Red"} else {"Green"})
    }
} else {
    Write-Host "  ERROR appsettings.json not found" -ForegroundColor Red
}
Write-Host ""

# Test 3: Test switch endpoint (dry run - not actually switching)
Write-Host "[Test 3] Testing switch endpoint (validation only)..." -ForegroundColor Yellow
Write-Host "  INFO This test will NOT actually switch the database" -ForegroundColor Cyan
try {
    $testRequest = @{ DatabaseName = "invalid_db_name" } | ConvertTo-Json
    $response = Invoke-RestMethod -Uri "$apiUrl/api/database/switch-connection" `
        -Method Post `
        -Body $testRequest `
        -ContentType "application/json" `
        -ErrorAction Stop
    Write-Host "  ERROR Should have rejected invalid database name" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 400) {
        Write-Host "  OK Correctly rejected invalid database name" -ForegroundColor Green
    } else {
        Write-Host "  WARNING Unexpected error: $_" -ForegroundColor Yellow
    }
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Backend API endpoints:" -ForegroundColor White
Write-Host "  GET  /api/database/current-connection" -ForegroundColor Cyan
Write-Host "  POST /api/database/switch-connection" -ForegroundColor Cyan
Write-Host ""
Write-Host "Frontend UI:" -ForegroundColor White
Write-Host "  - Database status banner in NavMenu" -ForegroundColor Cyan
Write-Host "  - Production (sst): Red banner with pulse animation" -ForegroundColor Red
Write-Host "  - Development (sstv2): Green banner" -ForegroundColor Green
Write-Host "  - Dropdown switcher with confirmation dialog" -ForegroundColor Cyan
Write-Host ""
Write-Host "To manually test the full feature:" -ForegroundColor Yellow
Write-Host "  1. Start the application: .\start-all-apps.ps1" -ForegroundColor Gray
Write-Host "  2. Open browser: http://localhost:5089" -ForegroundColor Gray
Write-Host "  3. Check the database banner at the top of the left sidebar" -ForegroundColor Gray
Write-Host "  4. Try switching databases using the dropdown" -ForegroundColor Gray
Write-Host "  5. Confirm the application restarts automatically" -ForegroundColor Gray
Write-Host ""
