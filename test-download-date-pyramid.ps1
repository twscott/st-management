# Pyramid Test - Download Date Logic

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  L1: Database Layer Test" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

$MysqlPath = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$Server = "127.0.0.1"
$Port = "3306"
$User = "root"
$Database = "sst"

Write-Host "Test 1.1: Check investbase.RecDate distribution" -ForegroundColor Yellow
$Query1 = "SELECT RecDate, COUNT(*) as Count FROM investbase GROUP BY RecDate ORDER BY RecDate DESC LIMIT 3;"
& $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $Query1

Write-Host "`nTest 1.2: Get MAX(RecDate)" -ForegroundColor Yellow
$Query2 = "SELECT MAX(RecDate) as MaxRecDate FROM investbase;"
& $MysqlPath -h $Server -P $Port -u $User -D $Database -e $Query2

Write-Host "`nTest 1.3: Simulate GetLatestInvestBaseAsync()" -ForegroundColor Yellow
$Query3 = "SELECT RecDate, LastDate, StockID FROM investbase ORDER BY RecDate DESC LIMIT 1;"
& $MysqlPath -h $Server -P $Port -u $User -D $Database -t -e $Query3

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  L2: API Endpoint Test" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "Test 2.1: Call /api/import/download-target-date" -ForegroundColor Yellow
try {
    $ApiUrl = "http://localhost:5089/api/import/download-target-date"
    Write-Host "   URL: $ApiUrl" -ForegroundColor Gray
    $Response = Invoke-RestMethod -Uri $ApiUrl -Method Get -ErrorAction Stop
    Write-Host "   Response: $($Response | ConvertTo-Json)" -ForegroundColor Green
    
    $LatestDate = $Response.LatestDate
    if ($LatestDate) {
        Write-Host "`n   ✅ API returned: $LatestDate" -ForegroundColor Green
        
        if ($LatestDate -eq "2026-02-25") {
            Write-Host "   ✅ CORRECT: Date is 2026-02-25 (today)" -ForegroundColor Green
        } else {
            Write-Host "   ❌ WRONG: Date is $LatestDate, expected 2026-02-25" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "   ❌ API call failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Is the API running on port 5089?" -ForegroundColor Yellow
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  L3: Logic Analysis" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

$CurrentHour = (Get-Date).Hour
Write-Host "Current Time: $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Yellow
Write-Host "Current Hour: $CurrentHour" -ForegroundColor Yellow

if ($CurrentHour -ge 15) {
    Write-Host "Logic: Hour >= 15, should use investbase.RecDate" -ForegroundColor Green
} else {
    Write-Host "Logic: Hour < 15, should use investbase.LastDate" -ForegroundColor Yellow
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Diagnosis" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

Write-Host "If L1 shows 2026-02-25 but L2 shows 2026-02-24:" -ForegroundColor Yellow
Write-Host "  → API has NOT been restarted after code changes" -ForegroundColor Red
Write-Host "  → Solution: Restart the API" -ForegroundColor Green
Write-Host "`nIf L1 shows 2026-02-24:" -ForegroundColor Yellow
Write-Host "  → Database update failed or wrong database" -ForegroundColor Red
Write-Host "  → Solution: Re-run fix-investbase-today.ps1" -ForegroundColor Green
