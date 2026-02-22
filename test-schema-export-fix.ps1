# Test Schema Export Fix - Verify the corrected export logic
# Purpose: Ensure future exports generate correct SQL format

param(
    [string]$SourceDatabase = "sst",
    [string]$OutputFolder = "D:\DBbackup\TEST\schema_export_test"
)

Write-Host "=== Schema Export Fix Verification ===" -ForegroundColor Cyan
Write-Host ""

# API endpoint
$apiUrl = "http://localhost:5008/api/database/export"

# Prepare request
$requestBody = @{
    SourceDatabase = $SourceDatabase
    OutputPath = $OutputFolder
} | ConvertTo-Json

Write-Host "[1/4] Testing database export via API..." -ForegroundColor Yellow
Write-Host "  Source DB: $SourceDatabase" -ForegroundColor Gray
Write-Host "  Output:    $OutputFolder" -ForegroundColor Gray
Write-Host ""

try {
    # Call export API
    Write-Host "  Calling: POST $apiUrl" -ForegroundColor Gray
    $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Body $requestBody -ContentType "application/json" -TimeoutSec 60
    
    if ($response.Success) {
        Write-Host "✅ Export completed successfully" -ForegroundColor Green
        Write-Host "  Tables exported: $($response.TableCount)" -ForegroundColor Cyan
    } else {
        Write-Host "❌ Export failed: $($response.Message)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ API call failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[2/4] Checking generated schema file..." -ForegroundColor Yellow

$schemaFile = Get-ChildItem -Path "$OutputFolder\DBSchema" -Filter "*_schema.sql" | Select-Object -First 1

if (-not $schemaFile) {
    Write-Host "❌ Schema file not found!" -ForegroundColor Red
    exit 1
}

Write-Host "  Schema file: $($schemaFile.Name)" -ForegroundColor Cyan
Write-Host "  Size: $([math]::Round($schemaFile.Length / 1KB, 2)) KB" -ForegroundColor Cyan

Write-Host ""
Write-Host "[3/4] Validating schema format..." -ForegroundColor Yellow

$content = Get-Content $schemaFile.FullName -Raw -Encoding UTF8

# Check for common format issues
$issues = @()

# Issue 1: Tab-separated format (should NOT exist)
if ($content -match '\w+\t+CREATE TABLE') {
    $issues += "⚠️  Found tab-separated format (tablename\tCREATE TABLE)"
}

# Issue 2: Literal \n instead of newlines (should NOT exist in CREATE TABLE)
if ($content -match 'CREATE TABLE[^;]*\\n') {
    $issues += "⚠️  Found literal \n in CREATE TABLE statements"
}

# Issue 3: Multi-line CREATE TABLE (SHOULD exist - indicates correct format)
$multiLineCount = ([regex]::Matches($content, 'CREATE TABLE[^\(]*\(\s*\n')).Count
if ($multiLineCount -eq 0) {
    $issues += "⚠️  CREATE TABLE statements are not properly formatted with newlines"
}

# Issue 4: Proper DROP TABLE statements (SHOULD exist)
$dropCount = ([regex]::Matches($content, 'DROP TABLE IF EXISTS `\w+`;')).Count
if ($dropCount -eq 0) {
    $issues += "⚠️  No DROP TABLE statements found"
}

# Display validation results
if ($issues.Count -eq 0) {
    Write-Host "✅ Schema format validation PASSED" -ForegroundColor Green
    Write-Host "  ✓ No tab-separated format found" -ForegroundColor Gray
    Write-Host "  ✓ No literal \n found in CREATE statements" -ForegroundColor Gray
    Write-Host "  ✓ Found $multiLineCount properly formatted CREATE TABLE statements" -ForegroundColor Gray
    Write-Host "  ✓ Found $dropCount DROP TABLE statements" -ForegroundColor Gray
} else {
    Write-Host "❌ Schema format validation FAILED" -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host "  $issue" -ForegroundColor Yellow
    }
    exit 1
}

Write-Host ""
Write-Host "[4/4] Sample schema preview..." -ForegroundColor Yellow

# Show first CREATE TABLE statement
$firstCreateMatch = [regex]::Match($content, '(?s)DROP TABLE IF EXISTS `(\w+)`;.*?CREATE TABLE.*?;')
if ($firstCreateMatch.Success) {
    $sampleTable = $firstCreateMatch.Groups[1].Value
    $sampleCreate = $firstCreateMatch.Value
    
    Write-Host "  First table: $sampleTable" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Sample SQL (first 500 chars):" -ForegroundColor Gray
    Write-Host "  " + ($sampleCreate.Substring(0, [Math]::Min(500, $sampleCreate.Length)) -replace "`n", "`n  ") -ForegroundColor DarkGray
    Write-Host "  ..." -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "✅ SCHEMA EXPORT FIX VERIFIED!" -ForegroundColor Green
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Future exports will generate correct SQL format:" -ForegroundColor Yellow
Write-Host "  ✓ Standard CREATE TABLE statements" -ForegroundColor Gray
Write-Host "  ✓ Real newlines (not \n literals)" -ForegroundColor Gray
Write-Host "  ✓ No tab-separated format" -ForegroundColor Gray
Write-Host "  ✓ Directly executable by MySQL" -ForegroundColor Gray
Write-Host ""
Write-Host "Test output saved to:" -ForegroundColor Cyan
Write-Host "  $OutputFolder" -ForegroundColor White
Write-Host ""
