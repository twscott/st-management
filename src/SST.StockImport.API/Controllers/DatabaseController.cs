using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.Interfaces;
using System.Text.Json;

namespace SST.StockImport.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DatabaseController : ControllerBase
{
    private readonly ILogger<DatabaseController> _logger;
    private readonly IDatabaseService _databaseService;
    private readonly IConfiguration _configuration;

    public DatabaseController(
        ILogger<DatabaseController> logger, 
        IDatabaseService databaseService,
        IConfiguration configuration)
    {
        _logger = logger;
        _databaseService = databaseService;
        _configuration = configuration;
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

    /// <summary>
    /// 获取当前连接的数据库名称
    /// </summary>
    [HttpGet("current-connection")]
    public IActionResult GetCurrentConnection()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var databaseName = ExtractDatabaseName(connectionString);
            
            _logger.LogInformation("Current database: {DatabaseName}", databaseName);
            
            return Ok(new 
            { 
                DatabaseName = databaseName,
                IsProduction = databaseName?.Equals("sst", StringComparison.OrdinalIgnoreCase) ?? false,
                ConnectionString = MaskConnectionString(connectionString)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current connection");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 切换数据库连接并重启应用
    /// </summary>
    [HttpPost("switch-connection")]
    public async Task<IActionResult> SwitchConnection([FromBody] SwitchConnectionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request?.DatabaseName))
            {
                return BadRequest(new { Success = false, Message = "DatabaseName is required" });
            }

            // 验证数据库名称（仅允许 sst 和 sstv2）
            if (!request.DatabaseName.Equals("sst", StringComparison.OrdinalIgnoreCase) && 
                !request.DatabaseName.Equals("sstv2", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { Success = false, Message = "Invalid database name. Only 'sst' or 'sstv2' allowed." });
            }

            var currentDb = ExtractDatabaseName(_configuration.GetConnectionString("DefaultConnection"));
            
            if (request.DatabaseName.Equals(currentDb, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { Success = true, Message = $"Already using database: {currentDb}" });
            }

            _logger.LogWarning("Switching database from {CurrentDb} to {NewDb}", currentDb, request.DatabaseName);

            // 修改配置文件 - 必须同时修改 appsettings.json 和 appsettings.Development.json
            // 因为在 Development 环境下，appsettings.Development.json 会覆盖 appsettings.json
            var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../"));
            
            // 更新主配置文件
            var mainConfigPath = Path.Combine(projectRoot, "appsettings.json");
            await UpdateConfigFileAsync(mainConfigPath, currentDb, request.DatabaseName);
            
            // 更新 Development 配置文件（如果存在）
            var devConfigPath = Path.Combine(projectRoot, "appsettings.Development.json");
            await UpdateConfigFileAsync(devConfigPath, currentDb, request.DatabaseName);

            _logger.LogInformation("Configuration updated. Database switched to: {NewDb}", request.DatabaseName);

            // 启动一个后台任务来重启应用（延迟 1 秒让响应返回）
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                _logger.LogWarning("Restarting application due to database switch...");
                Environment.Exit(0);
            });

            return Ok(new 
            { 
                Success = true, 
                Message = $"Database switched to {request.DatabaseName}. Application will restart in 1 second.",
                PreviousDatabase = currentDb,
                NewDatabase = request.DatabaseName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch database connection");
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    private string? ExtractDatabaseName(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(connectionString, @"Database=([^;]+)", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        return match.Success ? match.Groups[1].Value : null;
    }

    private string? MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return null;

        // 隐藏密码
        return System.Text.RegularExpressions.Regex.Replace(connectionString, 
            @"Password=([^;]*)", 
            "Password=***", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// 更新配置文件中的数据库连接字符串
    /// </summary>
    private async Task UpdateConfigFileAsync(string configPath, string oldDbName, string newDbName)
    {
        _logger.LogInformation("Updating config file: {Path}", configPath);
        
        if (!System.IO.File.Exists(configPath))
        {
            _logger.LogWarning("Config file not found, skipping: {Path}", configPath);
            return;
        }

        var json = await System.IO.File.ReadAllTextAsync(configPath);
        var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        // 构建新的 JSON
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == "ConnectionStrings")
                {
                    writer.WriteStartObject("ConnectionStrings");
                    var oldConnectionString = property.Value.GetProperty("DefaultConnection").GetString();
                    var newConnectionString = oldConnectionString?.Replace($"Database={oldDbName}", $"Database={newDbName}");
                    writer.WriteString("DefaultConnection", newConnectionString);
                    writer.WriteEndObject();
                }
                else
                {
                    property.WriteTo(writer);
                }
            }
            
            writer.WriteEndObject();
        }

        var updatedJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        await System.IO.File.WriteAllTextAsync(configPath, updatedJson);
        _logger.LogInformation("Config file updated: {Path}", configPath);
    }
}

public record SwitchConnectionRequest(string DatabaseName);
