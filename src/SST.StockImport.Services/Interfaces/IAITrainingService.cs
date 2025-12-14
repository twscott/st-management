using System;
using System.Threading.Tasks;
using SST.StockImport.Core.Entities;

namespace SST.StockImport.Services;

/// <summary>
/// AI 训练服务接口
/// </summary>
public interface IAITrainingService
{
    /// <summary>
    /// 触发 AI 训练
    /// </summary>
    Task<bool> TriggerAITrainingAsync(DateTime executionDate);

    /// <summary>
    /// 获取 AI 训练日志
    /// </summary>
    Task<AITrainingLog?> GetTrainingLogAsync(DateTime executionDate);
}
