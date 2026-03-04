# Emergency Restore: Critical Large Tables
# Tables: alertlist, stock60days, tradedata
# Backup: D:\DBbackup\OWN\20260302_sst\DBData

param(
    [string]$BackupPath = "D:\DBbackup\OWN\20260302_sst\DBData",
    [string]$TargetDatabase = "sstv2"
)

$mysql = "d:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
$tables = @("alertlist", "stock60days", "tradedata")

Write-Host "======================================================" -ForegroundColor Red
Write-Host "  EMERGENCY RESTORE: Critical Large Tables" -ForegroundColor Red
Write-Host "======================================================" -ForegroundColor Red
Write-Host ""

# Check MySQL
if (-not (Test-Path $mysql)) {
    Write-Host "❌ MySQL not found: $mysql" -ForegroundColor Red
    exit 1
}

foreach ($table in $tables) {
    $dataFile = Join-Path $BackupPath "$table.sql"
    
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host "  Processing: $table" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    
    # [1] Check file exists
    Write-Host "[1/5] Checking file..." -ForegroundColor Yellow
    if (-not (Test-Path $dataFile)) {
        Write-Host "  ❌ File not found: $dataFile" -ForegroundColor Red
        Write-Host "  Skipping $table" -ForegroundColor Yellow
        Write-Host ""
        continue
    }
    
    $fileSize = (Get-Item $dataFile).Length / 1MB
    Write-Host "  ✅ Found: $([math]::Round($fileSize, 1)) MB" -ForegroundColor Green
    
    # [2] Check current row count
    Write-Host "[2/5] Checking current data..." -ForegroundColor Yellow
    try {
        $beforeCount = & $mysql -u root -N -e "SELECT COUNT(*) FROM $TargetDatabase.$table" 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  📊 Current rows: $beforeCount" -ForegroundColor Gray
        } else {
            Write-Host "  ⚠️  Cannot read current count" -ForegroundColor Yellow
            $beforeCount = "N/A"
        }
    } catch {
        $beforeCount = "N/A"
    }
    
    # [3] TRUNCATE table (clear old data)
    Write-Host "[3/5] Clearing existing data (TRUNCATE)..." -ForegroundColor Yellow
    try {
        & $mysql -u root -e "TRUNCATE TABLE $TargetDatabase.$table" 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ Table cleared" -ForegroundColor Green
        } else {
            Write-Host "  ⚠️  TRUNCATE failed, continuing anyway" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  ⚠️  TRUNCATE failed: $_" -ForegroundColor Yellow
    }
    
    # [4] Process file to remove BOM (streaming for large files)
    Write-Host "[4/5] Processing SQL file (removing BOM, streaming mode)..." -ForegroundColor Yellow
    $tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
    
    try {
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        $reader = New-Object System.IO.StreamReader($dataFile, [System.Text.Encoding]::UTF8)
        $writer = New-Object System.IO.StreamWriter($tempFile, $false, $utf8NoBom)
        
        $lineCount = 0
        $bomCount = 0
        
        while ($null -ne ($line = $reader.ReadLine())) {
            # Remove BOM character using -replace (regex)
            $cleanLine = $line -replace [char]0xFEFF, ''
            if ($line.Length -ne $cleanLine.Length) {
                $bomCount++
            }
            
            $writer.WriteLine($cleanLine)
            $lineCount++
            
            if ($lineCount % 50000 -eq 0) {
                Write-Host "  ⏳ Processed $($lineCount / 1000)K lines..." -ForegroundColor Gray
            }
        }
        
        $reader.Close()
        $writer.Close()
        
        if ($bomCount -gt 0) {
            Write-Host "  🔧 Removed BOM from $bomCount lines" -ForegroundColor Cyan
        }
        Write-Host "  ✅ Clean file prepared: $([math]::Round((Get-Item $tempFile).Length / 1MB, 1)) MB ($lineCount lines)" -ForegroundColor Green
    } catch {
        Write-Host "  ❌ Failed to process file: $_" -ForegroundColor Red
        if ($reader) { $reader.Close() }
        if ($writer) { $writer.Close() }
        continue
    }
    
    # [5] Import data
    Write-Host "[5/5] Importing data (this may take several minutes)..." -ForegroundColor Yellow
    $startTime = Get-Date
    
    try {
        Write-Host "  ⏳ Started at: $($startTime.ToString('HH:mm:ss'))" -ForegroundColor Gray
        
        # Use cmd /c for < redirection - escape quotes properly
        $cmdArgs = "/c `"`"$mysql`" -u root --default-character-set=utf8 $TargetDatabase < `"$tempFile`"`""
        $process = Start-Process -FilePath "cmd.exe" -ArgumentList $cmdArgs -NoNewWindow -Wait -PassThru
        
        $duration = (Get-Date) - $startTime
        
        if ($process.ExitCode -eq 0) {
            Write-Host "  ✅ Import completed in $([math]::Round($duration.TotalMinutes, 1)) minutes" -ForegroundColor Green
            
            # Verify
            try {
                $afterCount = & $mysql -u root -N -e "SELECT COUNT(*) FROM $TargetDatabase.$table" 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "  📊 Final row count: $afterCount" -ForegroundColor Green
                    
                    if ($beforeCount -ne "N/A") {
                        if ([long]$afterCount -gt [long]$beforeCount) {
                            $diff = [long]$afterCount - [long]$beforeCount
                            Write-Host "  📈 Change: +$diff rows" -ForegroundColor Green
                        } elseif ([long]$afterCount -lt [long]$beforeCount) {
                            $diff = [long]$beforeCount - [long]$afterCount
                            Write-Host "  📉 Change: -$diff rows" -ForegroundColor Yellow
                        } else {
                            Write-Host "  ℹ️  No change" -ForegroundColor Cyan
                        }
                    }
                }
            } catch {
                Write-Host "  ⚠️  Cannot verify final count" -ForegroundColor Yellow
            }
        } else {
            Write-Host "  ❌ Import failed with exit code $($process.ExitCode)" -ForegroundColor Red
        }
        
    } catch {
        Write-Host "  ❌ Import failed: $_" -ForegroundColor Red
    } finally {
        # Cleanup temp file (if we created one)
        if ($tempFile -ne $dataFile -and (Test-Path $tempFile)) {
            Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
        }
    }
    
    Write-Host ""
}

Write-Host "======================================================" -ForegroundColor Green
Write-Host "  EMERGENCY RESTORE COMPLETED" -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Verify data in each table" -ForegroundColor White
Write-Host "  2. Check application functionality" -ForegroundColor White
Write-Host "  3. Review any errors above" -ForegroundColor White
Write-Host ""
