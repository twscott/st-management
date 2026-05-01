# SST Stock Import - Management Menu
# Central launcher for service management

$Root = $PSScriptRoot

function Show-Status {
    Write-Host ""
    Write-Host "  [Current Status]" -ForegroundColor Yellow

    foreach ($svc in @("SST.StockImport.API", "SST.StockImport.Web")) {
        $s = Get-Service $svc -ErrorAction SilentlyContinue
        if ($s) {
            $color = if ($s.Status -eq "Running") { "Green" } else { "Red" }
            Write-Host ("  {0,-28} {1}" -f $svc, $s.Status) -ForegroundColor $color
        } else {
            Write-Host ("  {0,-28} {1}" -f $svc, "Not Installed") -ForegroundColor DarkGray
        }
    }

    foreach ($entry in @(@{Port=5008;Name="API"}, @{Port=5089;Name="Web"})) {
        $conn = Get-NetTCPConnection -LocalPort $entry.Port -ErrorAction SilentlyContinue | Where-Object State -eq "Listen" | Select-Object -First 1
        if ($conn) {
            Write-Host ("  Port {0} ({1})  LISTENING  PID {2}" -f $entry.Port, $entry.Name, $conn.OwningProcess) -ForegroundColor Green
        } else {
            Write-Host ("  Port {0} ({1})  Not listening" -f $entry.Port, $entry.Name) -ForegroundColor DarkGray
        }
    }
    Write-Host ""
}

while ($true) {
    Clear-Host
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "   SST Stock Import - Management Menu" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  1. Redeploy API + Web  (build + copy)" -ForegroundColor White
    Write-Host "  2. Redeploy API only" -ForegroundColor White
    Write-Host "  3. Redeploy Web only" -ForegroundColor White
    Write-Host "  ----------------------------------------" -ForegroundColor DarkGray
    Write-Host "  4. Start services  (sc start)" -ForegroundColor White
    Write-Host "  5. Stop services   (sc stop)" -ForegroundColor White
    Write-Host "  ----------------------------------------" -ForegroundColor DarkGray
    Write-Host "  6. Check status" -ForegroundColor White
    Write-Host "  Q. Quit" -ForegroundColor DarkGray
    Write-Host "========================================" -ForegroundColor Cyan

    Show-Status

    $choice = Read-Host "Select"

    switch ($choice.Trim().ToUpper()) {
        "1" {
            & "$Root\redeploy-services.ps1"
            Read-Host "Press Enter to continue"
        }
        "2" {
            & "$Root\redeploy-services.ps1" -Api
            Read-Host "Press Enter to continue"
        }
        "3" {
            & "$Root\redeploy-services.ps1" -Web
            Read-Host "Press Enter to continue"
        }
        "4" {
            Write-Host "Starting services..." -ForegroundColor Yellow
            Start-Process powershell -ArgumentList "-NoProfile -Command `"sc.exe start SST.StockImport.API; sc.exe start SST.StockImport.Web`"" -Verb RunAs -Wait
            Write-Host "Done" -ForegroundColor Green
            Read-Host "Press Enter to continue"
        }
        "5" {
            Write-Host "Stopping services..." -ForegroundColor Yellow
            Start-Process powershell -ArgumentList "-NoProfile -Command `"sc.exe stop SST.StockImport.Web; sc.exe stop SST.StockImport.API`"" -Verb RunAs -Wait
            Write-Host "Done" -ForegroundColor Green
            Read-Host "Press Enter to continue"
        }
        "6" {
            Show-Status
            Read-Host "Press Enter to continue"
        }
        "Q" {
            Write-Host "Goodbye!" -ForegroundColor Cyan
            break
        }
        default {
            Write-Host "Invalid option, try again" -ForegroundColor Red
            Start-Sleep 1
        }
    }

    if ($choice.Trim().ToUpper() -eq "Q") { break }
}
Write-Host ""
