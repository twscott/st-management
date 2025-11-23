using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SST.StockImport.Infrastructure.Data;

/// <summary>
/// 設計時 DbContext 工廠，用於 EF Core 工具（migrations, scaffolding 等）
/// 這樣就不需要在設計時連接實際的資料庫
/// </summary>
public class StockImportDbContextFactory : IDesignTimeDbContextFactory<StockImportDbContext>
{
    public StockImportDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StockImportDbContext>();
        
        // 設計時使用的連接字串（不需要實際可連接的資料庫）
        var connectionString = "Server=localhost;Port=3306;Database=sst_stock_import;Uid=root;Pwd=;";
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 27));
        
        optionsBuilder.UseMySql(connectionString, serverVersion);

        return new StockImportDbContext(optionsBuilder.Options);
    }
}
