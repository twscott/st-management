namespace SST.StockImport.Core.Interfaces;

public interface IDatabaseService
{
    Task<List<string>> GetDatabasesAsync();
    
    Task<List<BackupFolderInfo>> GetBackupFoldersAsync();
    
    Task<DatabaseExportResult> ExportDatabaseAsync(DatabaseExportRequest request, CancellationToken cancellationToken = default);
    
    Task<DatabaseImportResult> ImportDatabaseAsync(DatabaseImportRequest request, CancellationToken cancellationToken = default);
    
    Task<List<string>> GetTablesAsync(string databaseName);
    
    Task<bool> TestConnectionAsync();
}

public class BackupFolderInfo
{
    public string FullPath { get; set; } = "";
    public string FolderName { get; set; } = "";
    public DateTime CreatedTime { get; set; }
    public string DatabaseName { get; set; } = "";
}

public class DatabaseExportRequest
{
    public string SourceDatabase { get; set; } = "sstv2";
    public string OutputPath { get; set; } = "";
}

public class DatabaseImportRequest
{
    public string SourcePath { get; set; } = "";
    public string TargetDatabase { get; set; } = "sstv2";
    public bool ImportSchema { get; set; } = true;
    public bool ImportData { get; set; } = true;
}

public class DatabaseExportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string SchemaPath { get; set; } = "";
    public string DataPath { get; set; } = "";
    public int TableCount { get; set; }
    public List<string> ExportedTables { get; set; } = new();
}

public class DatabaseImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int TablesImported { get; set; }
    public List<string> ImportedTables { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
