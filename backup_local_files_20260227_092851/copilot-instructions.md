# 🤖 SST Stock Import System - AI Agent Instructions

**Project**: Stock Import Timing Task Framework (SST)  
**Stack**: .NET 8.0, C#, XUnit, Entity Framework Core, MySQL  
**Version**: 1.0 (Feb 2026)

---

## 📐 Architecture at a Glance

**SST Stock Import** is a multi-layered .NET application for automated stock data processing with time-based task scheduling:

```
SST.StockImport.API          ← REST API + SignalR (Port 8080)
    └─ Controllers: TimerManagement, SupplementData
    └─ Program.cs: Serilog, EF, Hangfire config, Testing environment detection

SST.StockImport.Core         ← Business Logic (time-based decision engine)
    ├─ Scheduling/
    │  ├─ TimerManager: Main orchestrator for timed tasks
    │  ├─ SSTProcessingTask: Stock import + anomaly detection + recommendations
    │  ├─ ScheduleService: Task scheduling and execution
    │  └─ TimerExecutionLogService: Centralized logging
    ├─ Entities: Domain models (ExecutionEvent, ScheduleEntry)
    └─ DTOs: Data transfer objects for API responses

SST.StockImport.Services    ← External Integration
    ├─ Scrapers: GoodInfo API, stock data fetchers
    └─ Stock analysis services

SST.StockImport.Infrastructure
    └─ Data: DbContext, migrations, MySQL configuration

SST.StockImport.Web         ← Blazor Components (UI, SignalR integration)

Tests (92 test suite, 100% passing)
    ├─ L1: Unit Tests (29) - Individual module behavior with mocked time
    ├─ L2: Serverless Integration (11) - Module interaction without external services
    ├─ L3: WebAPI Integration (22) - HTTP endpoints via WebApplicationFactory
    └─ L4: End-to-End (9) - Complete production scenarios
```

---

## ⏰ Critical Timing Rules

**SSTProcessingTask** is a time-aware orchestrator with strict execution windows:

| Module | Window | Behavior | Tests |
|--------|--------|----------|-------|
| **do_sst** | 09:00-13:35 | Always execute during trading hours | L1: 8 tests |
| **detector** | 09:00-13:35 | Trading hours only; skip rest | L1: 10 tests |
| **calcRecommand** | minute > 10 | Only when minute > 10 within trading hours | L1: 7 tests |
| **Line Notify** | 09:00-09:30, 13:00-13:35 | Open and close notifications | L1: 3 tests |

**Special Period**: 09:06-09:12 (early adjustment) - All modules execute  
**Key Implementation**: Check `SSTProcessingTask.cs` for `ExecutionTime` parameter validation

---

## 🧪 Testing Architecture

### Test Pyramid (4 Layers)
1. **L1 Unit Tests** (`SSTProcessingTaskTests.cs`)
   - Mocked time: `ExecutionContext(executionTime: new DateTime(...))`
   - No external dependencies
   - ~18ms per test
   - Focus: Time conditions, module isolation

2. **L2 Serverless Integration** (`SSTProcessingTaskIntegrationTests.cs`)
   - Real task invocation, no external services
   - Verifies module interaction sequence
   - ~49ms per test
   - Focus: do_sst → detector → calcRecommand flow

3. **L3 WebAPI Integration** (`TimerManagementControllerTests.cs`)
   - `WebApplicationFactory<Program>` with `CustomWebApplicationFactory`
   - Full HTTP layer testing
   - ~90ms per test
   - Focus: API endpoints, response structure, HTTP status codes
   - **Known**: Task list is empty in test environment (expected)

4. **L4 End-to-End** (`SSTProcessingSandboxTests.cs`)
   - Complete production scenarios (6 time points per trading day)
   - State consistency, error recovery, performance baselines
   - ~222ms per test
   - Focus: Real-world workflows, edge cases

### Running Tests
```powershell
# All tests (92 total, ~5 seconds)
.\run-sst-tests.ps1 -TestLevel all

# By layer
.\run-sst-tests.ps1 -TestLevel unit          # L1 only (29 tests)
.\run-sst-tests.ps1 -TestLevel integration   # L1+L2 (40 tests)

# Direct dotnet
dotnet test tests/SST.StockImport.Core.Tests --filter "SSTProcessingTask"
```

---

## 🔧 Essential Workflows

### Adding a New Test
1. **Identify Layer**: Unit (mocked time) vs Integration (real modules) vs WebAPI vs E2E
2. **Location**: 
   - L1/L2: `tests/SST.StockImport.Core.Tests/Scheduling/`
   - L3: `tests/SST.StockImport.API.Tests/`
   - L4: `tests/SST.StockImport.API.Tests/SSTProcessingSandboxTests.cs`
3. **Pattern for L1**: Mock `ExecutionContext` with specific `executionTime`
4. **Pattern for L3**: Use `CustomWebApplicationFactory` fixture, HttpClient
5. **All tests MUST**: Call production code, never duplicate business logic

### Modifying Timing Conditions
1. **Change location**: `SSTProcessingTask.cs` (Core module)
2. **Update affected tests**:
   - L1: Adjust `ExecutionContext(executionTime)` in ~5 tests
   - L2: Update time transition scenarios in ~3 tests
   - L3: Verify API still works (2 tests may be affected)
   - L4: Adjust complete trading day flow (1 test affected)
3. **Verify**: `.\run-sst-tests.ps1 -TestLevel all` must pass

### Running the API Locally
```powershell
# Terminal 1: Start API (Port 8080 by default)
cd d:\vibeCoding\sst
dotnet run --project src/SST.StockImport.API

# Terminal 2: Run tests against it
.\run-sst-tests.ps1 -TestLevel all

# Terminal 3: Access endpoints
curl http://localhost:8080/api/timermanagement/logs
```

---

## 🎯 Code Patterns

### Time-Based Logic (Most Common Pattern)
```csharp
// Check hour window (09:00-13:59)
var hour = executionTime.Hour;
if (hour < 9 || hour >= 14) return;  // Skip outside trading hours

// Check minute condition (must be > 10)
if (executionTime.Minute <= 10) return;

// Check special early adjustment period (09:06-09:12)
var isEarlyAdjustment = hour == 9 && executionTime.Minute >= 6 && executionTime.Minute <= 12;
```

### Testing Time Conditions (L1 Pattern)
```csharp
// Create context with specific execution time
var context = new ExecutionContext 
{ 
    executionTime: new DateTime(2025, 12, 18, 9, 15, 0)  // 09:15 trading hours
};

// Call production method
_task.do_sst(context);  // MUST call real method, not mock

// Assert expected behavior
Assert.True(context.ExecutedModules.Contains("do_sst"));
```

### WebAPI Testing (L3 Pattern)
```csharp
public class APITests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    
    public APITests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();  // Real HTTP client
    }

    [Fact]
    public async Task GetLogs_ShouldReturnStructuredResponse()
    {
        var response = await _client.GetAsync("/api/timermanagement/logs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("data", json);
        Assert.Contains("statistics", json);
    }
}
```

### Logging Configuration (Critical for Tests)
```csharp
// Program.cs detects Testing environment and simplifies Serilog
if (!Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")?.Contains("Testing") ?? true)
{
    // Production logging setup
}

// CustomWebApplicationFactory resets Serilog to avoid "logger already frozen"
public CustomWebApplicationFactory()
{
    lock (LoggerLock) {
        Log.CloseAndFlush();  // Reset state
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .CreateLogger();
    }
}
```

---

## ⚠️ Common Pitfalls

| Issue | Cause | Fix |
|-------|-------|-----|
| "logger already frozen" in tests | Multiple Serilog instances | Use `CustomWebApplicationFactory` |
| Task list empty in L3/L4 tests | ScheduleService uninitialized in test env | Expected behavior; verify API structure not content |
| Time condition tests fail | Hardcoded time instead of parameterized | Use `ExecutionContext(executionTime)` parameter |
| API test fails with 500 | DbContext issue in Testing env | Check `Program.cs` testing detection |
| Null reference in ExecutionEvent | Description not initialized | Changed to `string?` (nullable) |

---

## 📁 Key Files Reference

| File | Purpose | When to Edit |
|------|---------|--------------|
| [src/SST.StockImport.Core/Scheduling/](src/SST.StockImport.Core/Scheduling/) | Core timing logic | Changing execution windows, adding new tasks |
| [src/SST.StockImport.API/Program.cs](src/SST.StockImport.API/Program.cs) | Service registration, Serilog setup | Adding dependencies, configuring testing |
| [tests/SST.StockImport.Core.Tests/Scheduling/](tests/SST.StockImport.Core.Tests/Scheduling/) | L1/L2 tests | Adding time condition logic |
| [tests/SST.StockImport.API.Tests/CustomWebApplicationFactory.cs](tests/SST.StockImport.API.Tests/CustomWebApplicationFactory.cs) | Test environment setup | Fixing Serilog or logger issues |
| [run-sst-tests.ps1](run-sst-tests.ps1) | Test runner script | Adding new test layers |
| [Docs/SST_Testing_Guide.md](Docs/SST_Testing_Guide.md) | Testing framework documentation | Reference for test patterns |
| [HANDOFF_CHECKLIST.md](HANDOFF_CHECKLIST.md) | Session handoff & status | Current test status, known issues |

---

## 🚀 Quick Reference

**Before committing code:**
```powershell
# 1. Run all tests locally
.\run-sst-tests.ps1 -TestLevel all

# 2. Verify no compiler warnings
dotnet build /p:TreatWarningsAsErrors=true

# 3. Check test coverage for modified modules
# (If modified SSTProcessingTask, ensure ≥95% test coverage)
```

**For debugging a failing test:**
```powershell
# Run single test class with verbose output
dotnet test tests/SST.StockImport.Core.Tests --filter "SSTProcessingTaskTests" -v detailed

# Run single test
dotnet test tests/SST.StockImport.Core.Tests --filter "NameOf::MethodName" --no-build

# Attach debugger
dotnet test tests/... -v diagnostic
```

**When test count seems off:**
- L1 Unit Tests: 29 tests (covering 4 modules × time conditions)
- L2 Integration: 11 tests (covering module interactions)
- L3 WebAPI: 22 tests (covering all endpoints + workflows)
- L4 End-to-End: 9 tests (covering production scenarios)
- **Total: 92 tests, 100% passing = system ready**

---

## 📊 Success Criteria

✅ **Code is ready when:**
- All 92 tests pass
- Execution time ~5 seconds
- No compiler warnings
- Time logic correctly validated (L1)
- API responses verified (L3)
- Production scenarios tested (L4)
- HANDOFF_CHECKLIST shows all phases complete

❌ **Code is NOT ready if:**
- Any test fails
- Time conditions not verified across all layers
- API tests skipped or expectations changed
- Duplicate logic between production and test code
- Documentation out of sync with code

---

## 📞 Session Handoff

When starting a new session:
1. Check `HANDOFF_CHECKLIST.md` for project status
2. Verify `.\run-sst-tests.ps1 -TestLevel all` passes
3. Review Session Report in `Docs/Todo/` for previous context
4. Look for "Cumulative TODOs" section for next priorities

**Current Status** (as of Feb 2026): ✅ Complete testing framework, all 92 tests passing
