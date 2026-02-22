param(
    [ValidateSet('unit', 'integration', 'all')]
    [string]$TestLevel = 'all'
)

Set-StrictMode -Version Latest

$ProjectRoot = Split-Path -Parent $PSCommandPath
$TestProject = "tests\SST.StockImport.Core.Tests\SST.StockImport.Core.Tests.csproj"

Write-Host "=================================================================================="
Write-Host "SST Processing Task Testing Framework" -ForegroundColor Cyan
Write-Host "Test Level: $TestLevel" -ForegroundColor Cyan
Write-Host "=================================================================================="
Write-Host ""

$filters = @{
    'unit' = 'SSTProcessingTaskTests'
    'integration' = 'SSTProcessingTaskIntegrationTests'
}

function Run-Tests {
    param(
        [string]$Level,
        [string]$Filter
    )
    
    Write-Host "Running [$Level] tests..." -ForegroundColor Cyan
    
    $result = & dotnet test $TestProject --filter $Filter --no-build -v normal
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ [$Level] tests passed" -ForegroundColor Green
        return $true
    } else {
        Write-Host "✗ [$Level] tests failed" -ForegroundColor Red
        return $false
    }
}

$allPassed = $true

if ($TestLevel -eq 'unit' -or $TestLevel -eq 'all') {
    Write-Host ""
    Write-Host "Layer 1: Unit Tests (29 tests)" -ForegroundColor Yellow
    Write-Host "Verify: do_sst, detector, calcRecommand individual behavior" -ForegroundColor Gray
    if (-not (Run-Tests 'unit' $filters['unit'])) {
        $allPassed = $false
    }
}

if ($TestLevel -eq 'integration' -or $TestLevel -eq 'all') {
    Write-Host ""
    Write-Host "Layer 2: Serverless Integration Tests (11 tests)" -ForegroundColor Yellow
    Write-Host "Verify: Module interactions without WebAPI" -ForegroundColor Gray
    if (-not (Run-Tests 'integration' $filters['integration'])) {
        $allPassed = $false
    }
}

Write-Host ""
Write-Host "=================================================================================="
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "=================================================================================="

if ($allPassed) {
    Write-Host "✓ All test layers passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Test Pyramid:" -ForegroundColor Cyan
    Write-Host "  L1 - Unit Tests (29):        Individual module tests" -ForegroundColor Gray
    Write-Host "  L2 - Integration Tests (11): Serverless interaction tests" -ForegroundColor Gray
    Write-Host "  L3 - WebAPI Tests:           With backend server (TODO)" -ForegroundColor Gray
    Write-Host "  L4 - Sandbox Out Tests:      End-to-end tests (TODO)" -ForegroundColor Gray
    exit 0
} else {
    Write-Host "✗ Some tests failed" -ForegroundColor Red
    exit 1
}
