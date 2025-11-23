using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Core.Interfaces;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Infrastructure.Repositories;

/// <summary>
/// AlertLog 資料存取實作
/// </summary>
public class AlertLogRepository : IAlertLogRepository
{
    private readonly StockImportDbContext _context;

    public AlertLogRepository(StockImportDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// 建立新的警報日誌
    /// </summary>
    public async Task CreateAsync(AlertLog alertLog, CancellationToken cancellationToken = default)
    {
        alertLog.CreatedAt = DateTime.UtcNow;
        
        await _context.AlertLogs.AddAsync(alertLog, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 批次建立警報日誌
    /// </summary>
    public async Task CreateBatchAsync(IEnumerable<AlertLog> alertLogs, CancellationToken cancellationToken = default)
    {
        var logs = alertLogs.ToList();
        if (!logs.Any())
            return;

        foreach (var log in logs)
        {
            log.CreatedAt = DateTime.UtcNow;
        }

        await _context.AlertLogs.AddRangeAsync(logs, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 根據作業 ID 查詢所有警報
    /// </summary>
    public async Task<List<AlertLog>> GetByJobIdAsync(
        string jobId,
        string? alertType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AlertLogs.Where(a => a.JobId == jobId);
        
        if (!string.IsNullOrEmpty(alertType))
            query = query.Where(a => a.AlertType == alertType);
        
        return await query
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 查詢最近的警報日誌
    /// </summary>
    public async Task<List<AlertLog>> GetRecentAlertsAsync(
        int limit = 100,
        string? alertType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AlertLogs.AsQueryable();
        
        if (!string.IsNullOrEmpty(alertType))
            query = query.Where(a => a.AlertType == alertType);
        
        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 根據警報類型查詢
    /// </summary>
    public async Task<IEnumerable<AlertLog>> GetByAlertTypeAsync(
        string alertType, 
        DateTime? startDate = null, 
        DateTime? endDate = null)
    {
        var query = _context.AlertLogs
            .Where(a => a.AlertType == alertType);

        if (startDate.HasValue)
            query = query.Where(a => a.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.CreatedAt <= endDate.Value);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }
}
