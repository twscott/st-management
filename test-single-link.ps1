# Test single GoodInfo link with different selectors
# Usage: .\test-single-link.ps1 -TestNumber 1 -SelectorIndex 5
#   TestNumber: 1 or 2 (for Test_01 or Test_02)
#   SelectorIndex: which tr:nth-child to try (4, 5, 6, 7, 8, etc.)

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet(1, 2)]
    [int]$TestNumber,
    
    [Parameter(Mandatory=$false)]
    [int]$SelectorIndex = 5
)

Write-Host "=== Testing Single GoodInfo Link ===" -ForegroundColor Cyan
Write-Host "Test: Test_0$TestNumber" -ForegroundColor Yellow
Write-Host "Selector: tr:nth-child($SelectorIndex)" -ForegroundColor Yellow
Write-Host ""

# Navigate to test project
cd "d:\vibeCoding\sst\tests\GoodInfo19LinksTest"

# Build filter
$filter = "Test_0$TestNumber"

# Temporarily modify the test file to use the specified selector
$testFile = "GoodInfo19LinksTests.cs"
$backupFile = "GoodInfo19LinksTests.cs.backup"

# Create backup
Copy-Item $testFile $backupFile -Force

try {
    # Read file
    $content = Get-Content $testFile -Raw -Encoding UTF8
    
    # For Test_01, replace the selector
    if ($TestNumber -eq 1) {
        $oldPattern = 'tr:nth-child\(\d+\) > td:nth-child\(2\) > input\[type=button\]:nth-child\(2\)'
        $newSelector = "tr:nth-child($SelectorIndex) > td:nth-child(2) > input[type=button]:nth-child(2)"
        
        # Find and replace in Test_01 section only
        $content = $content -replace `
            '(public void Test_01.*?cssSelector = "#txtStockListData > table > tbody > )tr:nth-child\(\d+\)(.*?";)', `
            "`$1tr:nth-child($SelectorIndex)`$2"
    }
    elseif ($TestNumber -eq 2) {
        # For Test_02
        $content = $content -replace `
            '(public void Test_02.*?cssSelector = "#txtStockListData > table > tbody > )tr:nth-child\(\d+\)(.*?";)', `
            "`$1tr:nth-child($SelectorIndex)`$2"
    }
    
    # Save modified content
    $content | Set-Content $testFile -Encoding UTF8 -NoNewline
    
    Write-Host "Modified selector to: tr:nth-child($SelectorIndex)" -ForegroundColor Green
    Write-Host "Running test..." -ForegroundColor Yellow
    Write-Host ""
    
    # Run the test
    dotnet test --filter $filter --logger "console;verbosity=normal"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "SUCCESS! tr:nth-child($SelectorIndex) works for Test_0$TestNumber" -ForegroundColor Green
        
        # Log the result
        $logFile = "d:\vibeCoding\sst\goodinfo-working-selectors.txt"
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        Add-Content -Path $logFile -Value "[$timestamp] Test_0${TestNumber}: tr:nth-child($SelectorIndex) - SUCCESS"
        Write-Host "Result logged to: $logFile" -ForegroundColor Cyan
    }
    else {
        Write-Host ""
        Write-Host "FAILED! tr:nth-child($SelectorIndex) does not work for Test_0$TestNumber" -ForegroundColor Red
    }
}
finally {
    # Restore backup
    Write-Host ""
    Write-Host "Restoring original file..." -ForegroundColor Yellow
    Move-Item $backupFile $testFile -Force
    Write-Host "Original file restored." -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "To try another selector index, run:" -ForegroundColor Yellow
Write-Host "  .\test-single-link.ps1 -TestNumber $TestNumber -SelectorIndex <4|5|6|7|8>"
