using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Infrastructure.Repositories;

/// <summary>
/// 日程数据访问实现
/// </summary>
public class ScheduleRepository : IScheduleRepository
{
    private readonly StockImportDbContext _context;

    public ScheduleRepository(StockImportDbContext context)
    {
        _context = context;
    }

    public async Task<ScheduleExecution?> GetExecutionAsync(DateTime executionDate, string scheduleSlot)
    {
        return await _context.ScheduleExecutions
            .Where(x => x.ExecutionDate == executionDate && x.ScheduleSlot == scheduleSlot)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ScheduleExecution>> GetExecutionsByDateAsync(DateTime executionDate)
    {
        return await _context.ScheduleExecutions
            .Where(x => x.ExecutionDate == executionDate)
            .OrderBy(x => x.ScheduleSlot)
            .ToListAsync();
    }

    public async Task SaveExecutionAsync(ScheduleExecution execution)
    {
        try
        {
            if (execution.Id == 0)
            {
                _context.ScheduleExecutions.Add(execution);
            }
            else
            {
                _context.ScheduleExecutions.Update(execution);
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // 如果表不存在或数据库不可用，则忽略保存错误
            // 日志会由调用方处理
            throw new InvalidOperationException("無法保存執行記錄到數據庫", ex);
        }
    }

    public async Task<GoodInfoFailedLinkTracking?> GetFailedLinksAsync(DateTime executionDate)
    {
        return await _context.GoodInfoFailedLinkTrackings
            .FindAsync(executionDate);
    }

    public async Task SaveFailedLinksAsync(GoodInfoFailedLinkTracking tracking)
    {
        try
        {
            if (await _context.GoodInfoFailedLinkTrackings.FindAsync(tracking.ExecutionDate) == null)
            {
                _context.GoodInfoFailedLinkTrackings.Add(tracking);
            }
            else
            {
                _context.GoodInfoFailedLinkTrackings.Update(tracking);
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("無法保存失敗鏈接追蹤到數據庫", ex);
        }
    }

    public async Task<AITrainingLog?> GetAITrainingLogAsync(DateTime executionDate)
    {
        return await _context.AITrainingLogs
            .Where(x => x.ExecutionDate == executionDate)
            .FirstOrDefaultAsync();
    }

    public async Task SaveAITrainingLogAsync(AITrainingLog log)
    {
        try
        {
            if (log.Id == 0)
            {
                _context.AITrainingLogs.Add(log);
            }
            else
            {
                _context.AITrainingLogs.Update(log);
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("無法保存AI訓練日誌到數據庫", ex);
        }
    }

    public async Task<List<ScheduleExecutionLog>> GetExecutionLogsAsync(DateTime fromDate, DateTime toDate)
    {
        return await _context.ScheduleExecutionLogs
            .Where(x => x.ExecutionDate >= fromDate && x.ExecutionDate <= toDate)
            .OrderByDescending(x => x.OperationTime)
            .ToListAsync();
    }

    public async Task SaveExecutionLogAsync(ScheduleExecutionLog log)
    {
        _context.ScheduleExecutionLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}
