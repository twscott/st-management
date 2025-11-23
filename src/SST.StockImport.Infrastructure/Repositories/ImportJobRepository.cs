using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Infrastructure.Repositories;

/// <summary>
/// ImportJob 資料存取實作
/// </summary>
public class ImportJobRepository : IImportJobRepository
{
    private readonly StockImportDbContext _context;

    public ImportJobRepository(StockImportDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 建立新的匯入作業
    /// </summary>
    public async Task<string> CreateAsync(ImportJob job, CancellationToken cancellationToken = default)
    {
        job.CreatedAt = DateTime.UtcNow;
        job.StartTime = job.StartTime == default ? DateTime.UtcNow : job.StartTime;
        
        await _context.ImportJobs.AddAsync(job, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return job.Id;
    }

    /// <summary>
    /// 更新匯入作業
    /// </summary>
    public async Task UpdateAsync(ImportJob job, CancellationToken cancellationToken = default)
    {
        _context.ImportJobs.Update(job);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 根據 ID 查詢作業
    /// </summary>
    public async Task<ImportJob?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.ImportJobs
            .Include(j => j.AlertLogs)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    /// <summary>
    /// 查詢最近的 N 筆作業
    /// </summary>
    public async Task<List<ImportJob>> GetRecentJobsAsync(
        string? market = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ImportJobs.AsQueryable();
        
        if (!string.IsNullOrEmpty(market))
            query = query.Where(j => j.Market == market);
        
        return await query
            .OrderByDescending(j => j.StartTime)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查詢失敗的股票代碼（從 AlertLog）
    /// </summary>
    public async Task<List<string>> GetFailedStockCodesAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AlertLogs
            .Where(a => a.JobId == jobId)
            .Select(a => a.StockCode)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查詢指定時間範圍的作業
    /// </summary>
    public async Task<IEnumerable<ImportJob>> GetByDateRangeAsync(
        DateTime startDate, 
        DateTime endDate)
    {
        return await _context.ImportJobs
            .Where(j => j.StartTime >= startDate && j.StartTime <= endDate)
            .OrderByDescending(j => j.StartTime)
            .ToListAsync();
    }

    /// <summary>
    /// 刪除作業（cascade 刪除相關 AlertLog）
    /// </summary>
    public async Task<bool> DeleteAsync(string id)
    {
        var job = await _context.ImportJobs
            .Include(j => j.AlertLogs)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
            return false;

        _context.ImportJobs.Remove(job);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 取得統計資訊
    /// </summary>
    public async Task<(int total, int running, int completed, int failed)> GetStatisticsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var query = _context.ImportJobs.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(j => j.StartTime >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(j => j.StartTime <= endDate.Value);

        var total = await query.CountAsync();
        var running = await query.CountAsync(j => j.Status == JobStatus.Running);
        var completed = await query.CountAsync(j => j.Status == JobStatus.Completed);
        var failed = await query.CountAsync(j => j.Status == JobStatus.Failed);

        return (total, running, completed, failed);
    }
}
