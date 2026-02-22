# SST Database Export Script
# Export SST database schema and data to specified directory

param(
    [Parameter(Mandatory=$true)]
    [string]$OutputPath,
    
    [string]$SourceDb = "sstv2",
    
    [string]$MysqlPath = "D:\wamp64\bin\mysql\mysql8.0.31\bin"
)

$mysqldump = Join-Path $MysqlPath "mysqldump.exe"
$mysql = Join-Path $MysqlPath "mysql.exe"

Write-Host "=== SST Database Export ===" -ForegroundColor Cyan
Write-Host "Source Database: $SourceDb"
Write-Host "Output Path: $OutputPath"
Write-Host ""

if (-not (Test-Path $mysqldump)) {
    Write-Host "ERROR: mysqldump.exe not found at $mysqldump" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Host "[OK] Created output directory" -ForegroundColor Green
}

$schemaDir = Join-Path $OutputPath "DBSchema"
$dataDir = Join-Path $OutputPath "DBData"

if (-not (Test-Path $schemaDir)) {
    New-Item -ItemType Directory -Path $schemaDir -Force | Out-Null
}
if (-not (Test-Path $dataDir)) {
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
}

Write-Host "[1/2] Exporting schema..." -ForegroundColor Yellow

$schemaFile = Join-Path $schemaDir "$($SourceDb)_schema.sql"

& $mysqldump -u root --no-data --skip-triggers --skip-add-drop-table --complete-insert $SourceDb 2>$null | Out-File -FilePath $schemaFile -Encoding UTF8

$content = Get-Content $schemaFile -Raw -Encoding UTF8
$content = $content -replace "CREATE TABLE ", "CREATE TABLE IF NOT EXISTS "
$content = $content -replace "CREATE DATABASE .*?;", "-- CREATE DATABASE omitted"
$content = $content -replace "USE `.*?`;", "-- USE omitted"
Set-Content -Path $schemaFile -Value $content -Encoding UTF8

Write-Host "[OK] Schema exported to $schemaFile" -ForegroundColor Green

Write-Host "[2/2] Exporting table data..." -ForegroundColor Yellow

$tablesQuery = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = '$SourceDb' AND TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME"
$tables = & $mysql -u root -N -e $tablesQuery 2>$null

if ($tables) {
    $tableList = $tables -split "`n" | Where-Object { $_.Trim() -ne "" }
    $total = $tableList.Count
    $current = 0
    
    foreach ($table in $tableList) {
        $table = $table.Trim()
        if ([string]::IsNullOrWhiteSpace($table)) { continue }
        
        $current++
        Write-Host "[$current/$total] Exporting $table"
        
        $dataFile = Join-Path $dataDir "$table.sql"
        
        & $mysqldump -u root --no-create-info --complete-insert --skip-triggers --skip-extended-insert $SourceDb $table 2>$null | Out-File -FilePath $dataFile -Encoding UTF8
        
        $fileContent = Get-Content $dataFile -Raw -Encoding UTF8
        $fileContent = $fileContent -replace "LOCK TABLES", "-- LOCK TABLES"
        $fileContent = $fileContent -replace "UNLOCK TABLES", "-- UNLOCK TABLES"
        $fileContent = $fileContent -replace "/\*!40000 ALTER TABLE.*?\*/;", "-- ALTER TABLE omitted"
        Set-Content -Path $dataFile -Value $fileContent -Encoding UTF8
    }
    
    Write-Host "[OK] Data exported to $dataDir" -ForegroundColor Green
} else {
    Write-Host "[WARNING] No tables found in database" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Export Complete ===" -ForegroundColor Green
Write-Host "Schema: $schemaDir"
Write-Host "Data: $dataDir"
Write-Host ""
Write-Host "To import this backup, run:" -ForegroundColor Cyan
Write-Host "  .\import-sst-to-sstv2.ps1" -ForegroundColor White
