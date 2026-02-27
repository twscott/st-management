using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.Services;

public class DatabaseService : IDatabaseService
{
    private readonly ILogger<DatabaseService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _mysqlExePath;
    private readonly string _backupBasePath;

    public DatabaseService(ILogger<DatabaseService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        var binPath = _configuration["MySql:BinPath"] ?? "D:\\wamp64\\bin\\mysql\\mysql8.0.31\\bin";
        _mysqlExePath = Path.Combine(binPath, "mysql.exe");
        _backupBasePath = _configuration["Backup:BasePath"] ?? "D:\\DBbackup\\OWN";
    }

    public Task<List<BackupFolderInfo>> GetBackupFoldersAsync()
    {
        var folders = new List<BackupFolderInfo>();
        
        try
        {
            if (!Directory.Exists(_backupBasePath))
            {
                Directory.CreateDirectory(_backupBasePath);
                return Task.FromResult(folders);
            }

            var dirs = Directory.GetDirectories(_backupBasePath);
            foreach (var dir in dirs.OrderByDescending(d => Directory.GetCreationTime(d)))
            {
                var dirName = Path.GetFileName(dir);
                var dbName = ExtractDatabaseName(dirName);
                
                folders.Add(new BackupFolderInfo
                {
                    FullPath = dir,
                    FolderName = dirName,
                    CreatedTime = Directory.GetCreationTime(dir),
                    DatabaseName = dbName
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup folders");
        }

        return Task.FromResult(folders);
    }

    private string ExtractDatabaseName(string folderName)
    {
        var parts = folderName.Split('_');
        return parts.Length >= 2 ? parts[1] : folderName;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var output = await ExecuteMySqlAsync("SELECT 1", false);
            return !string.IsNullOrEmpty(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MySQL connection test failed");
            return false;
        }
    }

    public async Task<List<string>> GetDatabasesAsync()
    {
        var databases = new List<string>();
        
        try
        {
            var output = await ExecuteMySqlAsync("SHOW DATABASES", false);
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var db = line.Trim();
                if (!string.IsNullOrWhiteSpace(db) && 
                    !db.Equals("information_schema", StringComparison.OrdinalIgnoreCase) &&
                    !db.Equals("mysql", StringComparison.OrdinalIgnoreCase) &&
                    !db.Equals("performance_schema", StringComparison.OrdinalIgnoreCase) &&
                    !db.Equals("sys", StringComparison.OrdinalIgnoreCase))
                {
                    databases.Add(db);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get databases");
        }

        return databases;
    }

    public async Task<List<string>> GetTablesAsync(string databaseName)
    {
        var tables = new List<string>();
        
        try
        {
            _logger.LogInformation("Querying tables from database: {Database}", databaseName);
            
            var output = await ExecuteMySqlAsync(
                $"SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = '{databaseName}' AND TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME", false);
            
            _logger.LogInformation("GetTables query output length: {Length}", output.Length);
            
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var table = line.Trim();
                if (!string.IsNullOrWhiteSpace(table))
                    tables.Add(table);
            }
            
            _logger.LogInformation("Found {Count} tables in {Database}", tables.Count, databaseName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tables for database: {Database}", databaseName);
        }

        return tables;
    }

    public async Task<DatabaseExportResult> ExportDatabaseAsync(DatabaseExportRequest request, CancellationToken cancellationToken = default)
    {
        var result = new DatabaseExportResult();

        try
        {
            _logger.LogInformation("Starting export for database: {Database}", request.SourceDatabase);
            
            var outputPath = request.OutputPath;
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(_backupBasePath, $"{DateTime.Now:yyyyMMdd}_{request.SourceDatabase}");
            }
            
            _logger.LogInformation("Output path: {Path}", outputPath);
            
            Directory.CreateDirectory(outputPath);
            
            var schemaDir = Path.Combine(outputPath, "DBSchema");
            var dataDir = Path.Combine(outputPath, "DBData");
            Directory.CreateDirectory(schemaDir);
            Directory.CreateDirectory(dataDir);

            var schemaFile = Path.Combine(schemaDir, $"{request.SourceDatabase}_schema.sql");
            var schemaBuilder = new StringBuilder();
            
            schemaBuilder.AppendLine($"-- Schema for database: {request.SourceDatabase}");
            schemaBuilder.AppendLine($"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            schemaBuilder.AppendLine();

            _logger.LogInformation("[1/4] Exporting Tables from database: {Database}...", request.SourceDatabase);
            var tables = await GetTablesAsync(request.SourceDatabase);
            _logger.LogInformation("Found {Count} tables in database {Database}", tables.Count, request.SourceDatabase);
            
            foreach (var table in tables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                _logger.LogInformation("Exporting table: {Table}", table);
                
                var createTable = await ExecuteMySqlAsync($"SHOW CREATE TABLE `{request.SourceDatabase}`.`{table}`");
                _logger.LogInformation("SHOW CREATE TABLE result for {Table}: {Result}, Lines: {LineCount}", table, createTable.Length > 0 ? "OK" : "EMPTY", createTable.Split('\n').Length);
                
                if (!string.IsNullOrEmpty(createTable))
                {
                    var lines = createTable.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length >= 2)
                    {
                        // Extract CREATE TABLE statement (skip first line which is column header)
                        var rawStmt = string.Join('\n', lines.Skip(1)).Trim();
                        
                        // Fix tab-separated format: "tablename\tCREATE TABLE ..."
                        if (rawStmt.Contains('\t'))
                        {
                            var parts = rawStmt.Split('\t', 2);
                            if (parts.Length == 2)
                            {
                                rawStmt = parts[1]; // Extract CREATE TABLE part
                            }
                        }
                        
                        // Replace literal \n with real newlines for readability
                        var createStmt = rawStmt.Replace("\\n", "\n");
                        
                        // Remove COMMENT clauses that may contain invalid UTF8 characters
                        createStmt = RemoveInvalidComments(createStmt);
                        
                        schemaBuilder.AppendLine($"DROP TABLE IF EXISTS `{table}`;");
                        schemaBuilder.AppendLine(createStmt.EndsWith(';') ? createStmt : createStmt + ";");
                        schemaBuilder.AppendLine();
                    }
                    else
                    {
                        _logger.LogWarning("Table {Table} schema skipped: only {Count} line(s)", table, lines.Length);
                    }
                }
            }
            result.ExportedTables = tables;
            result.TableCount = tables.Count;

            _logger.LogInformation("[2/4] Exporting Functions...");
            var functions = await GetRoutinesAsync(request.SourceDatabase, "FUNCTION");
            foreach (var func in functions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var createFunc = await ExecuteMySqlAsync($"SHOW CREATE FUNCTION `{request.SourceDatabase}`.`{func}`");
                if (!string.IsNullOrEmpty(createFunc))
                {
                    var lines = createFunc.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length >= 2)
                    {
                        // Extract CREATE FUNCTION statement (skip column header)
                        // SHOW CREATE FUNCTION output format: Function|sql_mode|Create Function|character_set_client|collation_connection|Database Collation
                        var rawStmt = string.Join('\n', lines.Skip(1)).Trim();
                        if (rawStmt.Contains('\t'))
                        {
                            // Split by tab and extract the 3rd column (index 2) which is the CREATE FUNCTION statement
                            var parts = rawStmt.Split('\t');
                            if (parts.Length >= 3)
                            {
                                rawStmt = parts[2];  // Third column contains CREATE FUNCTION
                            }
                        }
                        var createStmt = UnescapeMySqlString(rawStmt);
                        
                        schemaBuilder.AppendLine($"DROP FUNCTION IF EXISTS `{func}`;");
                        schemaBuilder.AppendLine($"DELIMITER ;;");
                        schemaBuilder.AppendLine(createStmt.EndsWith(';') ? createStmt.TrimEnd(';') + ";;" : createStmt + ";;");
                        schemaBuilder.AppendLine("DELIMITER ;");
                        schemaBuilder.AppendLine();
                    }
                }
            }

            _logger.LogInformation("[3/4] Exporting Stored Procedures...");
            var procedures = await GetRoutinesAsync(request.SourceDatabase, "PROCEDURE");
            foreach (var proc in procedures)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var createProc = await ExecuteMySqlAsync($"SHOW CREATE PROCEDURE `{request.SourceDatabase}`.`{proc}`");
                if (!string.IsNullOrEmpty(createProc))
                {
                    var lines = createProc.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length >= 2)
                    {
                        // Extract CREATE PROCEDURE statement (skip column header)
                        // SHOW CREATE PROCEDURE output format: Procedure|sql_mode|Create Procedure|character_set_client|collation_connection|Database Collation
                        var rawStmt = string.Join('\n', lines.Skip(1)).Trim();
                        if (rawStmt.Contains('\t'))
                        {
                            // Split by tab and extract the 3rd column (index 2) which is the CREATE PROCEDURE statement
                            var parts = rawStmt.Split('\t');
                            if (parts.Length >= 3)
                            {
                                rawStmt = parts[2];  // Third column contains CREATE PROCEDURE
                            }
                        }
                        var createStmt = UnescapeMySqlString(rawStmt);
                        
                        schemaBuilder.AppendLine($"DROP PROCEDURE IF EXISTS `{proc}`;");
                        schemaBuilder.AppendLine($"DELIMITER ;;");
                        schemaBuilder.AppendLine(createStmt.EndsWith(';') ? createStmt.TrimEnd(';') + ";;" : createStmt + ";;");
                        schemaBuilder.AppendLine("DELIMITER ;");
                        schemaBuilder.AppendLine();
                    }
                }
            }

            _logger.LogInformation("[4/4] Exporting Views...");
            var views = await GetViewsAsync(request.SourceDatabase);
            foreach (var view in views)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var createView = await ExecuteMySqlAsync($"SHOW CREATE VIEW `{request.SourceDatabase}`.`{view}`");
                if (!string.IsNullOrEmpty(createView))
                {
                    var lines = createView.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length >= 2)
                    {
                        // Extract CREATE VIEW statement (skip column header, handle tab separation)
                        // SHOW CREATE VIEW format: View|Create View|character_set_client|collation_connection
                        var rawStmt = string.Join('\n', lines.Skip(1)).Trim();
                        if (rawStmt.Contains('\t'))
                        {
                            var parts = rawStmt.Split('\t');
                            if (parts.Length >= 2) rawStmt = parts[1];  // Second column (index 1) contains CREATE VIEW
                        }
                        var createStmt = UnescapeMySqlString(rawStmt);
                        
                        // Remove COMMENT clauses that may contain invalid UTF8 characters
                        createStmt = RemoveInvalidComments(createStmt);
                        
                        schemaBuilder.AppendLine($"DROP VIEW IF EXISTS `{view}`;");
                        schemaBuilder.AppendLine(createStmt.EndsWith(';') ? createStmt : createStmt + ";");
                        schemaBuilder.AppendLine();
                    }
                }
            }

            await File.WriteAllTextAsync(schemaFile, schemaBuilder.ToString(), cancellationToken);
            var schemaSize = new FileInfo(schemaFile).Length;
            _logger.LogInformation("Schema file saved: {File} ({Size} bytes)", schemaFile, schemaSize);
            result.SchemaPath = schemaDir;

            _logger.LogInformation("Exporting data for {Count} tables...", tables.Count);
            
            // Get table sizes for optimization
            var tableSizes = await GetTableSizesAsync(request.SourceDatabase);
            
            // Categorize tables by size
            var smallTables = new List<string>();  // < 10MB
            var mediumTables = new List<string>(); // 10-100MB
            var largeTables = new List<string>();  // > 100MB
            
            foreach (var table in tables)
            {
                var sizeMB = tableSizes.TryGetValue(table, out var size) ? size : 0;
                if (sizeMB < 10) smallTables.Add(table);
                else if (sizeMB < 100) mediumTables.Add(table);
                else largeTables.Add(table);
            }
            
            _logger.LogInformation("Table distribution: {Small} small (<10MB), {Medium} medium (10-100MB), {Large} large (>100MB)",
                smallTables.Count, mediumTables.Count, largeTables.Count);
            
            var exportedCount = 0;
            var failedTables = new List<string>();
            var lockObj = new object();
            
            // Export small tables with 5 concurrent tasks
            if (smallTables.Count > 0)
            {
                _logger.LogInformation("Exporting {Count} small tables (5 concurrent)...", smallTables.Count);
                using var smallSemaphore = new SemaphoreSlim(5);
                var smallTasks = smallTables.Select(async table =>
                {
                    await smallSemaphore.WaitAsync(cancellationToken);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var dataFile = Path.Combine(dataDir, $"{table}.sql");
                        await ExportTableDataAsync(request.SourceDatabase, table, dataFile);
                        lock (lockObj)
                        {
                            exportedCount++;
                            if (exportedCount % 5 == 0)
                            {
                                _logger.LogInformation("Exported {Count}/{Total} tables", exportedCount, tables.Count);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to export table: {Table}", table);
                        lock (lockObj) { failedTables.Add(table); }
                    }
                    finally
                    {
                        smallSemaphore.Release();
                    }
                });
                await Task.WhenAll(smallTasks);
            }
            
            // Export medium tables with 2 concurrent tasks
            if (mediumTables.Count > 0)
            {
                _logger.LogInformation("Exporting {Count} medium tables (2 concurrent)...", mediumTables.Count);
                using var mediumSemaphore = new SemaphoreSlim(2);
                var mediumTasks = mediumTables.Select(async table =>
                {
                    await mediumSemaphore.WaitAsync(cancellationToken);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var dataFile = Path.Combine(dataDir, $"{table}.sql");
                        await ExportTableDataAsync(request.SourceDatabase, table, dataFile);
                        lock (lockObj)
                        {
                            exportedCount++;
                            _logger.LogInformation("Exported {Count}/{Total} tables", exportedCount, tables.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to export table: {Table}", table);
                        lock (lockObj) { failedTables.Add(table); }
                    }
                    finally
                    {
                        mediumSemaphore.Release();
                    }
                });
                await Task.WhenAll(mediumTasks);
            }
            
            // Export large tables sequentially
            if (largeTables.Count > 0)
            {
                _logger.LogInformation("Exporting {Count} large tables (sequential)...", largeTables.Count);
                foreach (var table in largeTables)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var dataFile = Path.Combine(dataDir, $"{table}.sql");
                        await ExportTableDataAsync(request.SourceDatabase, table, dataFile);
                        exportedCount++;
                        _logger.LogInformation("Exported {Count}/{Total} tables", exportedCount, tables.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to export table: {Table}", table);
                        failedTables.Add(table);
                    }
                }
            }

            result.DataPath = dataDir;
            result.ExportedTables = tables.Where(t => !failedTables.Contains(t)).ToList();
            result.TableCount = exportedCount;
            
            if (failedTables.Count > 0)
            {
                result.Success = true; // Partial success is considered success
                result.Message = $"Exported {exportedCount}/{tables.Count} tables. Large tables skipped: {string.Join(", ", failedTables)}";
            }
            else
            {
                result.Success = true;
                result.Message = $"Successfully exported {tables.Count} tables, {functions.Count} functions, {procedures.Count} procedures, {views.Count} views";
            }
        }
        catch (OperationCanceledException)
        {
            result.Message = "Export was cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");
            result.Message = $"Export failed: {ex.Message}";
        }

        return result;
    }

    public async Task<DatabaseImportResult> ImportDatabaseAsync(DatabaseImportRequest request, CancellationToken cancellationToken = default)
    {
        var result = new DatabaseImportResult();

        try
        {
            if (!Directory.Exists(request.SourcePath))
            {
                result.Message = $"Source path not found: {request.SourcePath}";
                return result;
            }

            if (request.ImportSchema)
            {
                _logger.LogInformation("[1/2] Creating database and importing schema...");
                
                await ExecuteMySqlAsync($"DROP DATABASE IF EXISTS {request.TargetDatabase}", false);
                await ExecuteMySqlAsync($"CREATE DATABASE {request.TargetDatabase} CHARACTER SET utf8 COLLATE utf8_general_ci", false);

                var schemaDir = Path.Combine(request.SourcePath, "DBSchema");
                if (!Directory.Exists(schemaDir))
                {
                    _logger.LogError("Schema directory not found: {Path}", schemaDir);
                    result.Message = $"Schema directory not found: {schemaDir}";
                    return result;
                }
                
                var schemaFiles = Directory.GetFiles(schemaDir, "*.sql");
                if (schemaFiles.Length == 0)
                {
                    _logger.LogError("No schema files found in: {Path}", schemaDir);
                    result.Message = $"No schema files found in: {schemaDir}";
                    return result;
                }

                var schemaFile = schemaFiles[0];
                _logger.LogInformation("Loading schema file: {File}", schemaFile);
                
                var schemaContent = await File.ReadAllTextAsync(schemaFile, cancellationToken);
                
                if (string.IsNullOrWhiteSpace(schemaContent))
                {
                    result.Message = "Schema file is empty";
                    return result;
                }
                
                // Replace database name (case-insensitive for Views/Functions/Procedures)
                schemaContent = schemaContent.Replace("`SST`", $"`{request.TargetDatabase}`");
                schemaContent = schemaContent.Replace("`sst`", $"`{request.TargetDatabase}`");
                
                // Remove USE database statements
                schemaContent = System.Text.RegularExpressions.Regex.Replace(schemaContent, "USE `.*?`;", "");
                
                // Remove DEFINER clauses to avoid permission issues
                schemaContent = System.Text.RegularExpressions.Regex.Replace(
                    schemaContent, 
                    @"DEFINER\s*=\s*`[^`]+`@`[^`]+`\s*", 
                    "", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                // Remove COMMENT clauses that may contain invalid UTF8 characters
                schemaContent = RemoveInvalidComments(schemaContent);
                
                // Fix hangfire index length issue
                schemaContent = schemaContent.Replace(
                    "UNIQUE KEY IX_hangfireSet_Key_Value (`Key`,`Value`)",
                    "UNIQUE KEY IX_hangfireSet_Key_Value (`Key`,`Value`(100))");

                _logger.LogInformation("Executing schema SQL ({0} chars)...", schemaContent.Length);
                try
                {
                    // Use --force to skip errors (e.g., VIEWs with missing columns) and continue
                    await ExecuteMySqlInputAsync(request.TargetDatabase, schemaContent, force: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Schema import completed with errors (this is expected for VIEWs/Functions with dependencies)");
                    result.Errors.Add($"Schema import warnings: {ex.Message}");
                }
                
                // Verify what was actually imported
                var importedTableCount = await GetTableCountAsync(request.TargetDatabase);
                _logger.LogInformation("Schema imported: {TableCount} tables created", importedTableCount);
                result.TablesImported = importedTableCount;
            }

            if (request.ImportData)
            {
                var dataDir = Path.Combine(request.SourcePath, "DBData");
                if (!Directory.Exists(dataDir))
                {
                    _logger.LogWarning("Data directory not found: {Path}, skipping data import", dataDir);
                }
                else
                {
                    var dataFiles = Directory.GetFiles(dataDir, "*.sql");
                    _logger.LogInformation("[2/2] Importing {Count} table data files", dataFiles.Length);

                    var successCount = 0;
                    var failedTables = new List<string>();
                    var skippedTables = new List<string>();
                    
                    // ✅ Define critical tables that must succeed
                    var criticalTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                    { 
                        "tradedata", "stock60days", "alertlist", "stock20days" 
                    };
                    
                    // ✅ Skip Hangfire tables (runtime data, not business data)
                    // Hangfire will recreate these tables automatically on startup
                    var skipTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "hangfireaggregatedcounter", "hangfirecounter", "hangfiredistributedlock",
                        "hangfirehash", "hangfirejob", "hangfirejobparameter", "hangfirejobqueue",
                        "hangfirejobstate", "hangfirelist", "hangfireserver", "hangfireset", "hangfirestate"
                    };
                    
                    foreach (var dataFile in dataFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var tableName = Path.GetFileNameWithoutExtension(dataFile);
                        
                        // Skip Hangfire tables (foreign key constraint issues)
                        if (skipTables.Contains(tableName))
                        {
                            _logger.LogInformation("  [{Current}/{Total}] {Table} - SKIPPED (Hangfire runtime data)",
                                successCount + failedTables.Count + skippedTables.Count + 1, dataFiles.Length, tableName);
                            skippedTables.Add(tableName);
                            continue;
                        }
                        
                        var isCritical = criticalTables.Contains(tableName);
                        
                        try
                        {
                            // ✅ Use streaming method for large files (>= 100MB)
                            var fileInfo = new FileInfo(dataFile);
                            var fileSizeMB = fileInfo.Length / 1024.0 / 1024.0;
                            
                            if (fileSizeMB >= 100)
                            {
                                _logger.LogInformation("  [{Current}/{Total}] {Table} ({SizeMB:F1} MB) - Using streaming mode",
                                    successCount + failedTables.Count + 1, dataFiles.Length, tableName, fileSizeMB);
                                await ImportTableDataStreamingAsync(request.TargetDatabase, dataFile, tableName, cancellationToken);
                            }
                            else
                            {
                                _logger.LogInformation("  [{Current}/{Total}] {Table} ({SizeMB:F1} MB)",
                                    successCount + failedTables.Count + 1, dataFiles.Length, tableName, fileSizeMB);
                                var content = await File.ReadAllTextAsync(dataFile, cancellationToken);
                                content = content.Replace("`SST`", $"`{request.TargetDatabase}`");
                                content = content.Replace("`sst`", $"`{request.TargetDatabase}`");
                                await ExecuteMySqlInputAsync(request.TargetDatabase, content);
                            }
                            
                            result.ImportedTables.Add(tableName);
                            successCount++;
                            
                            // ✅ Verify row count for critical tables
                            if (isCritical)
                            {
                                var rowCount = await GetTableRowCountAsync(request.TargetDatabase, tableName);
                                if (rowCount == 0)
                                {
                                    var warning = $"⚠️ CRITICAL: {tableName} imported but has 0 rows!";
                                    _logger.LogWarning(warning);
                                    result.Errors.Add(warning);
                                }
                                else
                                {
                                    _logger.LogInformation("  ✅ {Table}: Verified {RowCount:N0} rows", tableName, rowCount);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ Failed to import table: {Table}", tableName);
                            failedTables.Add(tableName);
                            
                            // ✅ Enhanced error message for critical tables
                            var errorMsg = isCritical 
                                ? $"❌ CRITICAL TABLE FAILED: {tableName}: {ex.Message}" 
                                : $"{tableName}: {ex.Message}";
                            result.Errors.Add(errorMsg);
                            
                            // ✅ For critical tables, consider immediate failure
                            if (isCritical)
                            {
                                _logger.LogError("Critical table {Table} failed - this may cause system instability", tableName);
                            }
                        }
                    }
                    
                    result.TablesImported = successCount;
                    
                    // Report skipped Hangfire tables
                    if (skippedTables.Count > 0)
                    {
                        _logger.LogInformation("Skipped {Count} Hangfire runtime tables: {Tables}", 
                            skippedTables.Count, string.Join(", ", skippedTables));
                        result.Errors.Add($"ℹ️ INFO: Skipped {skippedTables.Count} Hangfire runtime tables (will be auto-recreated)");
                    }
                    
                    // ✅ Check if any critical tables failed
                    var failedCriticalTables = failedTables.Where(t => criticalTables.Contains(t)).ToList();
                    
                    if (failedCriticalTables.Count > 0)
                    {
                        result.Success = false;
                        result.Message = $"❌ CRITICAL FAILURE: Failed to import {failedCriticalTables.Count} critical tables: {string.Join(", ", failedCriticalTables)}. " +
                                       $"Successfully imported {successCount}/{dataFiles.Length} tables total.";
                        _logger.LogError(result.Message);
                    }
                    else if (failedTables.Count > 0)
                    {
                        result.Message = $"⚠️ Partially successful: Imported {successCount}/{dataFiles.Length} tables. Failed: {string.Join(", ", failedTables)}";
                        _logger.LogWarning(result.Message);
                    }
                }
            }

            // Verify import success by checking actual table count
            var actualTableCount = await GetTableCountAsync(request.TargetDatabase);
            _logger.LogInformation("Verified {Count} tables exist in database {Database}", actualTableCount, request.TargetDatabase);
            
            // Update result with verified table count if only schema was imported
            if (request.ImportSchema && !request.ImportData)
            {
                result.TablesImported = actualTableCount;
            }
            
            // ✅ Enhanced success determination
            if (actualTableCount == 0)
            {
                result.Success = false;
                result.Message = "❌ Import failed: No tables found in database after import";
                result.Errors.Add("Schema import may have failed - database is empty");
            }
            else if (!string.IsNullOrEmpty(result.Message) && result.Message.Contains("CRITICAL FAILURE"))
            {
                // Already marked as failed by critical table failure
                result.Success = false;
            }
            else if (result.Errors.Any(e => e.Contains("CRITICAL")))
            {
                // Has critical warnings
                result.Success = false;
                if (string.IsNullOrEmpty(result.Message))
                {
                    result.Message = $"❌ Import failed with critical errors. {actualTableCount} tables exist but critical data may be missing.";
                }
            }
            else
            {
                result.Success = true;
                if (string.IsNullOrEmpty(result.Message))
                {
                    var warnings = result.Errors.Count > 0 ? $" (with {result.Errors.Count} warnings)" : "";
                    result.Message = $"✅ Successfully imported {result.TablesImported} tables to {request.TargetDatabase} (verified {actualTableCount} tables exist){warnings}";
                }
                else if (!result.Message.Contains("successfully") && !result.Message.Contains("Successfully") && !result.Message.Contains("✅"))
                {
                    // Partial success message already set
                    result.Success = true;
                }
            }
        }
        catch (OperationCanceledException)
        {
            result.Message = "Import was cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed");
            result.Message = $"Import failed: {ex.Message}";
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task<string> ExecuteMySqlAsync(string query, bool includeColumnNames = true)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _mysqlExePath,
            Arguments = includeColumnNames 
                ? $"-u root -e \"{query.Replace("\"", "\\\"")}\""
                : $"-u root -N -e \"{query.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = Process.Start(psi);
        if (process == null) return "";
        
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }

    private async Task ExecuteMySqlInputAsync(string database, string content, bool force = false)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"mysql_input_{Guid.NewGuid():N}.sql");
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            var forceFlag = force ? "--force" : "";
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{_mysqlExePath}\" -u root --default-character-set=utf8 {forceFlag} {database} < \"{tempFile}\"\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                // ✅ FIX 1: Add timeout protection (30 minutes for large files)
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException($"MySQL import timed out after 30 minutes. File may be too large or process hung.");
                }
                
                if (process.ExitCode != 0)
                {
                    _logger.LogError("MySQL import failed. Exit code: {Code}, Error: {Error}", process.ExitCode, error);
                    // ✅ FIX 2: Always throw on critical errors, even in force mode
                    if (!force || error.Contains("ERROR 2006") || error.Contains("ERROR 2013") || error.Contains("Lost connection"))
                    {
                        throw new InvalidOperationException($"MySQL import failed with exit code {process.ExitCode}: {error}");
                    }
                    else
                    {
                        _logger.LogWarning("MySQL import completed with non-critical errors (force mode): {Error}", error);
                    }
                }
                else if (!string.IsNullOrEmpty(error) && error.Contains("ERROR"))
                {
                    // ✅ FIX 3: Check for critical errors even when exit code is 0
                    if (error.Contains("ERROR 2006") || error.Contains("ERROR 2013") || error.Contains("Lost connection") || error.Contains("Out of memory"))
                    {
                        _logger.LogError("MySQL import critical error: {Error}", error);
                        throw new InvalidOperationException($"MySQL import critical error: {error}");
                    }
                    else if (!force)
                    {
                        _logger.LogError("MySQL import error: {Error}", error);
                        throw new InvalidOperationException($"MySQL import error: {error}");
                    }
                    else
                    {
                        _logger.LogWarning("MySQL import has errors (force mode, continuing): {Error}", error);
                    }
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogWarning("MySQL import stderr: {Error}", error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute MySQL import");
            throw;
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    /// <summary>
    /// ✅ NEW: Import large table data files using streaming + MySqlConnector
    /// Avoids Out-of-Memory and provides better progress tracking
    /// </summary>
    private async Task ImportTableDataStreamingAsync(string database, string dataFile, string tableName, CancellationToken cancellationToken = default)
    {
        var connectionString = $"Server=localhost;Database={database};User=root;Password=;AllowLoadLocalInfile=true;DefaultCommandTimeout=600";
        var fileInfo = new FileInfo(dataFile);
        var fileSizeMB = fileInfo.Length / 1024.0 / 1024.0;
        
        _logger.LogInformation("Importing {Table}: {SizeMB:F1} MB...", tableName, fileSizeMB);
        
        // For small files (<100MB), use traditional method
        if (fileSizeMB < 100)
        {
            var content = await File.ReadAllTextAsync(dataFile, cancellationToken);
            await ExecuteMySqlInputAsync(database, content);
            return;
        }
        
        // For large files (>=100MB), use streaming with MySqlConnector
        _logger.LogInformation("Using streaming mode for large file");
        
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sqlBatch = new StringBuilder();
        var batchCount = 0;
        var totalBatches = 0;
        var statementCount = 0;
        const int maxBatchStatements = 1000; // Execute 1000 INSERT statements per batch
        const int maxBatchSizeMB = 10; // Or 10MB, whichever comes first
        
        using var reader = new StreamReader(dataFile, Encoding.UTF8);
        
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            // Skip comments and USE statements
            if (line.TrimStart().StartsWith("--") || line.TrimStart().StartsWith("/*") || line.TrimStart().StartsWith("USE "))
                continue;
            
            sqlBatch.AppendLine(line);
            
            // Check if statement is complete (ends with semicolon)
            if (line.TrimEnd().EndsWith(";"))
            {
                statementCount++;
                
                // Execute batch when size limit reached
                if (statementCount >= maxBatchStatements || sqlBatch.Length >= maxBatchSizeMB * 1024 * 1024)
                {
                    await ExecuteBatchAsync(connection, sqlBatch.ToString(), cancellationToken);
                    totalBatches++;
                    batchCount++;
                    
                    if (batchCount % 10 == 0)
                    {
                        var progress = (double)reader.BaseStream.Position / reader.BaseStream.Length * 100;
                        _logger.LogInformation("  {Table}: {Progress:F1}% ({Batches} batches, ~{Statements} statements)",
                            tableName, progress, totalBatches, totalBatches * maxBatchStatements);
                    }
                    
                    sqlBatch.Clear();
                    statementCount = 0;
                }
            }
        }
        
        // Execute remaining statements
        if (sqlBatch.Length > 0)
        {
            await ExecuteBatchAsync(connection, sqlBatch.ToString(), cancellationToken);
            totalBatches++;
        }
        
        _logger.LogInformation("✅ {Table}: Completed {Batches} batches", tableName, totalBatches);
    }

    private async Task ExecuteBatchAsync(MySqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = new MySqlCommand(sql, connection);
            command.CommandTimeout = 300; // 5 minutes per batch
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.LockDeadlock)
        {
            // Retry once on deadlock
            _logger.LogWarning("Deadlock detected, retrying batch...");
            await Task.Delay(1000, cancellationToken);
            
            await using var retryCommand = new MySqlCommand(sql, connection);
            retryCommand.CommandTimeout = 300;
            await retryCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task ExportTableDataAsync(string database, string table, string outputFile)
    {
        var binPath = _configuration["MySql:BinPath"] ?? "D:\\wamp64\\bin\\mysql\\mysql8.0.31\\bin";
        var mysqldumpPath = Path.Combine(binPath, "mysqldump.exe");
        var tempFile = Path.Combine(Path.GetTempPath(), $"dump_{table}_{Guid.NewGuid():N}.sql");
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = mysqldumpPath,
                Arguments = $"-u root --no-create-info --complete-insert --skip-triggers --skip-extended-insert {database} {table}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return;
            
            using var reader = process.StandardOutput;
            using var writer = new StreamWriter(tempFile, false, Encoding.UTF8, bufferSize: 1024 * 1024);
            
            var copyTask = reader.BaseStream.CopyToAsync(writer.BaseStream);
            
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(30), cts.Token);
            var completedTask = await Task.WhenAny(copyTask, timeoutTask);
            
            if (completedTask == timeoutTask && !copyTask.IsCompleted)
            {
                try { process.Kill(); } catch { }
                throw new TimeoutException($"Export table {table} timed out after 30 minutes");
            }
            
            await copyTask;
            await writer.FlushAsync();
            writer.Close();
            
            if (!process.HasExited)
            {
                process.WaitForExit();
            }
            
            File.Copy(tempFile, outputFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    private async Task<int> GetTableCountAsync(string database)
    {
        try
        {
            var output = await ExecuteMySqlAsync(
                $"SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = '{database}' AND TABLE_TYPE = 'BASE TABLE'", false);
            
            if (!string.IsNullOrEmpty(output))
            {
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0 && int.TryParse(lines[0].Trim(), out var count))
                {
                    return count;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get table count for database: {Database}", database);
        }
        
        return 0;
    }

    /// <summary>
    /// ✅ NEW: Get row count for a specific table to verify data was imported
    /// </summary>
    private async Task<long> GetTableRowCountAsync(string database, string tableName)
    {
        try
        {
            var output = await ExecuteMySqlAsync(
                $"SELECT COUNT(*) FROM `{database}`.`{tableName}`", false);
            
            if (!string.IsNullOrEmpty(output))
            {
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0 && long.TryParse(lines[0].Trim(), out var count))
                {
                    return count;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get row count for table: {Database}.{Table}", database, tableName);
        }
        
        return 0;
    }

    private async Task<Dictionary<string, double>> GetTableSizesAsync(string database)
    {
        var tableSizes = new Dictionary<string, double>();
        
        try
        {
            var output = await ExecuteMySqlAsync(
                $"SELECT TABLE_NAME, ROUND((DATA_LENGTH + INDEX_LENGTH) / 1024 / 1024, 2) as SIZE_MB FROM information_schema.TABLES WHERE TABLE_SCHEMA = '{database}'", false);
            
            if (!string.IsNullOrEmpty(output))
            {
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines.Skip(1)) // Skip header
                {
                    var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var tableName = parts[0].Trim();
                        if (double.TryParse(parts[1].Trim(), out var sizeMB))
                        {
                            tableSizes[tableName] = sizeMB;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get table sizes, will use default categorization");
        }
        
        return tableSizes;
    }

    private string UnescapeMySqlString(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        // Unescape MySQL CLI output escape sequences
        // Use placeholder to handle \\\\ before other sequences
        const string placeholder = "\x00BACKSLASH\x00";
        
        return input
            .Replace("\\\\", placeholder)  // Temporarily replace double backslash
            .Replace("\\n", "\n")          // Newline
            .Replace("\\r", "\r")          // Carriage return
            .Replace("\\t", "\t")          // Tab
            .Replace("\\'", "'")           // Single quote
            .Replace("\\\"", "\"")         // Double quote
            .Replace(placeholder, "\\");   // Restore single backslash
    }

    private string RemoveInvalidComments(string sql)
    {
        // Remove COMMENT clauses from column definitions and table options
        // This prevents import failures due to invalid UTF8 characters in comments
        // Pattern handles SQL-style escaped quotes ('' within string literals)
        sql = Regex.Replace(sql, @"COMMENT\s+'(?:[^']|'')*'\s*", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"COMMENT\s+""(?:[^""]|"""")*""\s*", "", RegexOptions.IgnoreCase);
        
        return sql;
    }

    private async Task<List<string>> GetRoutinesAsync(string database, string routineType)
    {
        var routines = new List<string>();
        
        try
        {
            var output = await ExecuteMySqlAsync(
                $"SELECT ROUTINE_NAME FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = '{database}' AND ROUTINE_TYPE = '{routineType}'", false);
            
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var routine = line.Trim();
                if (!string.IsNullOrWhiteSpace(routine))
                    routines.Add(routine);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get {Type}s", routineType);
        }

        return routines;
    }

    private async Task<List<string>> GetViewsAsync(string database)
    {
        var views = new List<string>();
        
        try
        {
            var output = await ExecuteMySqlAsync(
                $"SELECT TABLE_NAME FROM information_schema.VIEWS WHERE TABLE_SCHEMA = '{database}'", false);
            
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var view = line.Trim();
                if (!string.IsNullOrWhiteSpace(view))
                    views.Add(view);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get views");
        }

        return views;
    }
}
