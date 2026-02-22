# AGENTS.md - SST Stock Import System

**Project**: Stock Import Timing Task Framework (SST)  
**Stack**: .NET 8.0, C#, xUnit, Entity Framework Core, MySQL  
**Version**: 1.0 (Feb 2026)

---

## Build & Test Commands

### Build
```powershell
dotnet build SST.StockImport.sln
dotnet build src/SST.StockImport.API/SST.StockImport.API.csproj
```

### Test Execution
```powershell
# All tests (~5 seconds, 92 tests total)
.\run-sst-tests.ps1 -TestLevel all

# Unit tests only (L1 - 29 tests)
.\run-sst-tests.ps1 -TestLevel unit

# Integration tests (L1+L2 - 40 tests)
.\run-sst-tests.ps1 -TestLevel integration

# Single test class
dotnet test tests/SST.StockImport.Core.Tests --filter "SSTProcessingTaskTests"

# Single test method
dotnet test tests/SST.StockImport.Core.Tests --filter "FullyQualifiedName~NameOfTestMethod"

# Verbose output
dotnet test tests/SST.StockImport.Core.Tests -v detailed
```

### Running the Application
```powershell
dotnet run --project src/SST.StockImport.API --urls "http://localhost:5000"
```

---

## Code Style

### General
- **Language**: C# with .NET 8.0, Implicit Usings: Enabled, Nullable: Enabled

### Project Structure
```
src/
  SST.StockImport.API/         # REST API + SignalR
  SST.StockImport.Core/      # Business logic, scheduling
  SST.StockImport.Services/  # External integrations
  SST.StockImport.Infrastructure/  # Data access, EF Core
  SST.StockImport.Web/       # Blazor UI
tests/
  SST.StockImport.Core.Tests/     # L1-L2 tests
  SST.StockImport.API.Tests/      # L3 tests
  SST.StockImport.E2ETests/       # L4 end-to-end
```

### Naming: Classes/Interfaces PascalCase, Methods/Properties PascalCase, Private fields _camelCase, Parameters camelCase

### Imports: System → Third-party → Project

### Error Handling
```csharp
if (context == null) throw new ArgumentNullException(nameof(context));
try { /* operation */ }
catch (Exception ex) { _logger.LogError($"Failed: {ex}"); throw; }
```

### Controller Pattern
```csharp
[ApiController][Route("api/[controller]")]
public class TimerController : ControllerBase {
    private readonly IService _service;
    public Controller(IService service) => _service = service;
    [HttpGet("logs")]
    public ActionResult<object> GetLogs([FromQuery] int pageSize = 50)
        => Ok(new { data = _service.GetData(pageSize) });
}
```

---

## Testing

### Layers: L1 Unit (Core.Tests), L2 Integration (Core.Tests), L3 WebAPI (API.Tests), L4 E2E

### L1 Test Pattern
```csharp
public class SSTProcessingTaskTests {
    private readonly Mock<ILogger<SSTProcessingTask>> _mockLogger;
    private readonly SSTProcessingTask _task;
    public SSTProcessingTaskTests() {
        _mockLogger = new Mock<ILogger<SSTProcessingTask>>();
        _task = new SSTProcessingTask(_mockLogger.Object);
    }
    [Fact]
    public async Task ExecuteAsync_NormalTradingHours_Executes() {
        var context = new TimerExecutionContext { ExecutionTime = new DateTime(2025, 12, 18, 10, 30, 0) };
        await _task.ExecuteAsync(context);
    }
}
```

### Test Rules: MUST call production code, use [Fact]/[Theory], name: Method_Scenario_ExpectedBehavior

---

## Timing Rules

| Module | Window | Behavior |
|--------|--------|----------|
| do_sst | 09:00-13:35 | Always execute during trading hours |
| detector | 09:00-13:35 | Trading hours only |
| calcRecommand | minute > 10 | Only when minute > 10 |
| Line Notify | 09:00-09:30, 13:00-13:35 | Open/close notifications |
| Morning Adjustment | 09:06-09:12 | All modules execute |

---

## Key Files

| File | Purpose |
|------|---------|
| `src/SST.StockImport.Core/Scheduling/Tasks/SSTProcessingTask.cs` | Core timing logic |
| `src/SST.StockImport.API/Program.cs` | Service registration, Serilog |
| `tests/SST.StockImport.Core.Tests/Scheduling/` | L1/L2 tests |
| `tests/SST.StockImport.API.Tests/CustomWebApplicationFactory.cs` | Test environment |

---

## Pitfalls

| Issue | Fix |
|-------|-----|
| "logger already frozen" | Use `CustomWebApplicationFactory` |
| Task list empty in tests | Expected behavior |
| Time condition tests fail | Use `ExecutionContext(executionTime)` |

---

## Before Committing
```powershell
.\run-sst-tests.ps1 -TestLevel all
dotnet build /p:TreatWarningsAsErrors=true
```

## Session Handoff: 1) Check HANDOFF_CHECKLIST.md, 2) Run tests, 3) Review Docs/Todo/
