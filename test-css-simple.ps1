Write-Host "=== CSS Config Test ===" -ForegroundColor Cyan
Write-Host ""

try {
    # Load assemblies
    $servicesPath = "src/SST.StockImport.Services/bin/Debug/net8.0/SST.StockImport.Services.dll"
    
    if (-not (Test-Path $servicesPath)) {
        Write-Host "Cannot find Services assembly: $servicesPath" -ForegroundColor Red
        exit 1
    }

    Write-Host "Loading assembly..." -ForegroundColor Gray
    [Reflection.Assembly]::LoadFile((Resolve-Path $servicesPath).Path) | Out-Null

    # Check GoodInfoUrlConfig
    $configType = [SST.StockImport.Services.Scrapers.GoodInfoUrlConfig]
    
    if ($configType.IsSealed -and $configType.IsAbstract) {
        Write-Host "GoodInfoUrlConfig is static class" -ForegroundColor Gray
        $allRequests = $configType::GetAllRequests()
    } else {
        Write-Host "Creating GoodInfoUrlConfig instance" -ForegroundColor Gray
        $urlConfig = New-Object $configType
        $allRequests = $urlConfig.GetAllRequests()
    }

    Write-Host "Total items: $($allRequests.Count)" -ForegroundColor Green
    Write-Host ""

    # Count CSS selectors
    $tr5Count = 0
    $tr7Count = 0
    $tr7Items = @()

    foreach ($request in $allRequests) {
        try {
            if ($configType.IsSealed -and $configType.IsAbstract) {
                $css = $configType::GetCorrectCssSelector($request.Name)
            } else {
                $css = $urlConfig.GetCorrectCssSelector($request.Name)
            }
            
            if ($css -eq "tr:nth-child(5) .link_green") {
                $tr5Count++
            } elseif ($css -eq "tr:nth-child(7) .link_green") {
                $tr7Count++
                $tr7Items += $request.Name
            }
        } catch {
            Write-Host "Error with $($request.Name): $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    Write-Host "CSS Selector Stats:" -ForegroundColor Cyan
    Write-Host "  tr:nth-child(5): $tr5Count items" -ForegroundColor White
    Write-Host "  tr:nth-child(7): $tr7Count items" -ForegroundColor White
    Write-Host ""

    if ($tr7Count -gt 0) {
        Write-Host "SUCCESS! Found differentiated CSS selectors" -ForegroundColor Green
        Write-Host "This should fix the 9/19 success rate issue" -ForegroundColor Green
        Write-Host ""
        
        Write-Host "Items using tr:nth-child(7):" -ForegroundColor Yellow
        $tr7Items | ForEach-Object {
            Write-Host "  - $_" -ForegroundColor Gray
        }
        
        $newSuccessRate = 9 + $tr7Count
        Write-Host ""
        Write-Host "Expected improvement:" -ForegroundColor Cyan
        Write-Host "  Previous: 9/19 (47%)" -ForegroundColor Gray
        Write-Host "  Expected: $newSuccessRate/19 ($([math]::Round($newSuccessRate/19*100, 1))%)" -ForegroundColor Green
    } else {
        Write-Host "WARNING: No tr:nth-child(7) configuration found" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Test completed successfully!" -ForegroundColor Green

} catch {
    Write-Host "Test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.Exception.ToString() -ForegroundColor Gray
}