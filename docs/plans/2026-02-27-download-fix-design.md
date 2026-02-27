# 2026-02-27-download-fix-design.md

## Overview

Fix two issues in the stock data download feature:
1. Homepage date picker should use `investbase.RecDate` as default instead of `DateTime.Today`
2. Enhance logging and validation to debug why downloads appear successful but fail silently

---

## Task A: Homepage Date Default Value

### Current Behavior
- `ImportPage.razor` line 141: `selectedDate = DateTime.Today`

### Changes
**File:** `src/SST.StockImport.Web/Components/Pages/ImportPage.razor`

```csharp
// Before
private DateTime selectedDate = DateTime.Today;

// After - Fetch from API in OnInitializedAsync
protected override async Task OnInitializedAsync()
{
    await LoadImportStatus();
    
    // Get default date from investbase.RecDate
    var targetDate = await ApiService.GetDownloadTargetDateAsync();
    if (targetDate.HasValue)
    {
        selectedDate = targetDate.Value;
    }
}
```

---

## Task B: Enhanced Logging and Validation

### Current Issues
1. `TWSEScraper` catches exceptions silently - no visibility into failures
2. No validation of downloaded data counts per market
3. Frontend shows "success" even when downloads fail

### Changes

#### 1. TWSEScraper.cs - Enhanced Error Logging
- Log URL, status code, and exception details when API calls fail
- Return detailed result object with per-market success/failure status
- Log file save results (path, size)

#### 2. ImportService.cs - Per-Market Validation
- Log count of records from each market (TSE/OTC/EMERGING)
- Return detailed result showing which markets succeeded/failed

#### 3. ImportController.cs - Detailed Response
- Return per-market success status
- Include file save paths in response

### Expected Results
- Logs will show:
  - Which API failed and why (URL, status code, exception)
  - Record count per market (TSE: X, OTC: Y, EMERGING: Z)
  - Full file save paths
- Frontend will display more detailed error messages
