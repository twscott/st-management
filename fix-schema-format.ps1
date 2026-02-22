# Fix Schema Format - Convert SHOW CREATE TABLE output to executable SQL
# Problem: Schema file contains tab-separated output with literal \n instead of newlines

param(
    [string]$InputFile = "D:\DBbackup\OWN\20260220_sst\DBSchema\sst_schema.sql",
    [string]$OutputFile = "D:\DBbackup\OWN\20260220_sst\DBSchema\sst_schema_fixed.sql"
)

Write-Host "=== Schema Format Fixer ===" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $InputFile)) {
    Write-Host "ERROR: Input file not found: $InputFile" -ForegroundColor Red
    exit 1
}

Write-Host "[1/3] Reading schema file..." -ForegroundColor Yellow
$content = Get-Content $InputFile -Raw -Encoding UTF8

Write-Host "  Original size: $($content.Length) chars" -ForegroundColor Gray

Write-Host "[2/3] Fixing format..." -ForegroundColor Yellow

# Split by lines
$lines = $content -split "`n"
$output = New-Object System.Text.StringBuilder

# Process header
$output.AppendLine("-- Schema for database: sst") | Out-Null
$output.AppendLine("-- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')") | Out-Null
$output.AppendLine("-- Fixed format: tab-separated -> standard SQL") | Out-Null
$output.AppendLine("") | Out-Null

$tableCount = 0

foreach ($line in $lines) {
    # Skip header comments
    if ($line -match "^--") {
        continue
    }
    
    # Process DROP TABLE lines
    if ($line -match "^DROP TABLE IF EXISTS") {
        $output.AppendLine($line.Trim()) | Out-Null
        continue
    }
    
    # Process CREATE TABLE lines (tab-separated format)
    if ($line -match "^(\w+)\s+CREATE TABLE") {
        # Format: "tablename\tCREATE TABLE `tablename` (\n  column1...\n) ENGINE=...;"
        # Split by tab to extract CREATE statement
        $parts = $line -split "`t", 2
        
        if ($parts.Length -eq 2) {
            $createStmt = $parts[1]
            
            # Replace literal \n with real newlines
            $createStmt = $createStmt -replace '\\n', "`n"
            
            # Ensure ends with semicolon
            if (-not $createStmt.TrimEnd().EndsWith(';')) {
                $createStmt = $createStmt.TrimEnd() + ";"
            }
            
            $output.AppendLine($createStmt) | Out-Null
            $output.AppendLine("") | Out-Null
            $tableCount++
            
            if ($tableCount % 10 -eq 0) {
                Write-Host "  Processed $tableCount tables..." -ForegroundColor Gray
            }
        }
        continue
    }
    
    # Process FUNCTION/PROCEDURE/VIEW definitions
    if ($line -match "DROP (FUNCTION|PROCEDURE|VIEW|TRIGGER)" -or 
        $line -match "CREATE (FUNCTION|PROCEDURE|VIEW| TRIGGER)" -or
        $line -match "DELIMITER") {
        $output.AppendLine($line.Trim()) | Out-Null
    }
}

$fixedContent = $output.ToString()

Write-Host "[3/3] Writing fixed schema..." -ForegroundColor Yellow
[System.IO.File]::WriteAllText($OutputFile, $fixedContent, [System.Text.Encoding]::UTF8)

Write-Host ""
Write-Host "✅ Schema format fixed!" -ForegroundColor Green
Write-Host "  Input:  $InputFile" -ForegroundColor Cyan
Write-Host "  Output: $OutputFile" -ForegroundColor Cyan
Write-Host "  Tables: $tableCount" -ForegroundColor Cyan
Write-Host "  Size:   $($fixedContent.Length) chars" -ForegroundColor Cyan
Write-Host ""
Write-Host "✨ You can now import the fixed schema file" -ForegroundColor Yellow
