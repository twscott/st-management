# AGENTS.md - SST Stock Import System

**Project**: Stock Import Timing Task Framework (SST)  
**Stack**: .NET 8.0, C#, xUnit, Entity Framework Core, MySQL

---

## Build & Test Commands

### Build
```powershell
dotnet build SST.StockImport.sln
dotnet build /p:TreatWarningsAsErrors=true  # Strict build
```

### Test Execution
```powershell
# All tests (92 total, ~5 seconds)
.\run-sst-tests.ps1 -TestLevel all

# By level
.\run-sst-tests.ps1 -TestLevel unit         # L1 (29 tests)
.\run-sst-tests.ps1 -TestLevel integration  # L1+L2 (40 tests)

# Single test class/method
dotnet test tests/SST.StockImport.Core.Tests --filter "SSTProcessingTaskTests"
dotnet test tests/SST.StockImport.Core.Tests --filter "FullyQualifiedName~MethodName"
dotnet test tests/SST.StockImport.Core.Tests -v detailed
```

### Running the Application
```powershell
dotnet run --project src/SST.StockImport.API --urls "http://localhost:5000"
```

---

## Project Structure
```
src/
  SST.StockImport.API/         # REST API + SignalR (port 5000/8080)
  SST.StockImport.Core/        # Business logic, scheduling
  SST.StockImport.Services/   # External integrations
  SST.StockImport.Infrastructure/  # EF Core, MySQL
  SST.StockImport.Web/        # Blazor UI
tests/
  SST.StockImport.Core.Tests/  # L1 Unit + L2 Integration
  SST.StockImport.API.Tests/   # L3 WebAPI + L4 E2E
```

---

## Code Style

### General
- C# .NET 8.0, Implicit Usings: Enabled, Nullable: Enabled
- **No comments** unless explicitly requested

### Naming
| Element | Convention | Example |
|---------|------------|---------|
| Classes/Interfaces | PascalCase | `SSTProcessingTask` |
| Methods/Properties | PascalCase | `ExecuteAsync` |
| Private fields | _camelCase | `_mockLogger` |
| Parameters | camelCase | `executionTime` |
| Test methods | Method_Scenario_Expected | `ExecuteAsync_NormalTradingHours_Executes` |

### Import Order: System → Third-party → Project (SST.*)

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
    public TimerController(IService service) => _service = service;
    [HttpGet("logs")]
    public ActionResult<object> GetLogs([FromQuery] int pageSize = 50)
        => Ok(new { data = _service.GetData(pageSize) });
}
```

---

## Testing

### Test Layers: L1 Unit → L2 Integration → L3 WebAPI → L4 E2E

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
        var context = new ExecutionContext { 
            executionTime = new DateTime(2025, 12, 18, 10, 30, 0) 
        };
        await _task.ExecuteAsync(context);
        Assert.True(context.ExecutedModules.Contains("do_sst"));
    }
}
```

### Rules
- MUST call production code, never duplicate business logic
- Use `[Fact]` or `[Theory]`
- For time tests: use `ExecutionContext(executionTime)` with specific DateTime

---

## Timing Rules

| Module | Window | Behavior |
|--------|--------|----------|
| do_sst | 09:00-13:35 | Always execute during trading hours |
| detector | 09:00-13:35 | Trading hours only |
| calcRecommand | minute > 10 | Only when minute > 10 |
| Line Notify | 09:00-09:30, 13:00-13:35 | Open/close notifications |
| Morning Adjustment | 09:06-09:12 | All modules execute |

```csharp
var hour = executionTime.Hour;
if (hour < 9 || hour >= 14) return;
if (executionTime.Minute <= 10) return;
```

---

## Common Pitfalls

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

## Session Handoff
1. Check `HANDOFF_CHECKLIST.md`
2. Run `.\run-sst-tests.ps1 -TestLevel all` (all 92 tests must pass)
3. Review `Docs/Todo/` for priorities
