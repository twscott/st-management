using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DatabaseController : ControllerBase
{
    private readonly ILogger<DatabaseController> _logger;
    private readonly IDatabaseService _databaseService;

    public DatabaseController(ILogger<DatabaseController> logger, IDatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    [HttpGet("test-connection")]
    public async Task<IActionResult> TestConnection()
    {
        try
        {
            var isConnected = await _databaseService.TestConnectionAsync();
            return Ok(new { Connected = isConnected, Message = isConnected ? "MySQL connection successful" : "MySQL connection failed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            return StatusCode(500, new { Connected = false, Message = ex.Message });
        }
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetDatabases()
    {
        try
        {
            var databases = await _databaseService.GetDatabasesAsync();
            return Ok(new { Databases = databases, Count = databases.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get databases");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("{databaseName}/tables")]
    public async Task<IActionResult> GetTables(string databaseName)
    {
        try
        {
            var tables = await _databaseService.GetTablesAsync(databaseName);
            return Ok(new { Database = databaseName, Tables = tables, Count = tables.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tables for database: {Database}", databaseName);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("backups")]
    public async Task<IActionResult> GetBackupFolders()
    {
        try
        {
            var folders = await _databaseService.GetBackupFoldersAsync();
            return Ok(new { Folders = folders, Count = folders.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup folders");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("export")]
    public async Task<IActionResult> ExportDatabase([FromBody] DatabaseExportRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrEmpty(request.SourceDatabase))
            {
                return BadRequest(new { Success = false, Message = "Invalid request: SourceDatabase is required" });
            }
            
            _logger.LogInformation("Starting database export: {Database} to {Path}", request.SourceDatabase, request.OutputPath);
            
            var result = await _databaseService.ExportDatabaseAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Export failed: {Message}", result.Message);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportDatabase([FromBody] DatabaseImportRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrEmpty(request.SourcePath) || string.IsNullOrEmpty(request.TargetDatabase))
            {
                return BadRequest(new { Success = false, Message = "Invalid request: SourcePath and TargetDatabase are required" });
            }
            
            _logger.LogInformation("Starting database import: {Path} to {Database}", request.SourcePath, request.TargetDatabase);
            
            var result = await _databaseService.ImportDatabaseAsync(request);
            
            if (result.Success)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed");
            return StatusCode(500, new { Success = false, Message = ex.Message, Errors = new[] { ex.Message } });
        }
    }
}
