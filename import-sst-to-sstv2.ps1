# SST Database Import Script
# Import SST database schema and data to specified database

param(
    [string]$SourcePath = "D:\DBbackup\OWN\20260219_SST\SST",
    
    [string]$TargetDb = "sstv2",
    
    [string]$MysqlPath = "D:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe"
)

Write-Host "=== SST Database Import ===" -ForegroundColor Cyan
Write-Host "Source: $SourcePath"
Write-Host "Target Database: $TargetDb"
Write-Host ""

Write-Host "[1/3] Creating database $TargetDb..." -ForegroundColor Yellow
$createCmd = "DROP DATABASE IF EXISTS $TargetDb; CREATE DATABASE $TargetDb CHARACTER SET utf8 COLLATE utf8_general_ci;"
& $MysqlPath -u root -e $createCmd
Write-Host "[OK] Database created" -ForegroundColor Green

# 2. Import schema
Write-Host "[2/3] Importing schema..." -ForegroundColor Yellow
$schemaFile = "$SourcePath\DBSchema\SST_schema.sql"

# Read with UTF-8 encoding
$content = [System.IO.File]::ReadAllText($schemaFile, [System.Text.Encoding]::UTF8)
$content = $content -replace '`SST`', $TargetDb
$content = $content -replace 'USE `SST`', "USE ``$TargetDb``"

# Fix index too long error for hangfireset table (utf8: 100+256 chars = 1068 bytes > 1000 limit)
$content = $content -replace 'UNIQUE KEY IX_hangfireSet_Key_Value \(`Key`,`Value`\)', 'UNIQUE KEY IX_hangfireSet_Key_Value (`Key`,`Value`(100))'

# Write to temp file
$tempSchema = "$env:TEMP\schema_utf8.sql"
[System.IO.File]::WriteAllText($tempSchema, $content, [System.Text.Encoding]::UTF8)

# Use cmd to run mysql with input redirect
cmd /c "$MysqlPath -u root $TargetDb < $tempSchema"
Remove-Item $tempSchema -ErrorAction SilentlyContinue
Write-Host "[OK] Schema imported" -ForegroundColor Green

# 3. Import table data
Write-Host "[3/3] Importing table data..." -ForegroundColor Yellow
$dataPath = "$SourcePath\DBData"
$sqlFiles = Get-ChildItem -Path $dataPath -Filter "*.sql"

$total = $sqlFiles.Count
$current = 0

foreach ($file in $sqlFiles) {
    $current++
    Write-Host "[$current/$total] $($file.Name)"
    
    try {
        $fileContent = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
        if ($fileContent) {
            $fileContent = $fileContent -replace '`SST`', $TargetDb
            $tempFile = "$env:TEMP\table_$current.sql"
            [System.IO.File]::WriteAllText($tempFile, $fileContent, [System.Text.Encoding]::UTF8)
            cmd /c "$MysqlPath -u root $TargetDb < $tempFile" 2>$null
            Remove-Item $tempFile -ErrorAction SilentlyContinue
        }
    } catch {
        Write-Host "  [ERROR]" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== Import Complete ===" -ForegroundColor Green
Write-Host "Database: $TargetDb"
