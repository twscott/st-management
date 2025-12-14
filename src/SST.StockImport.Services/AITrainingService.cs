using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Repositories;

namespace SST.StockImport.Services;

/// <summary>
/// AI Training 服务实现
/// </summary>
public class AITrainingService : IAITrainingService
{
    private readonly IScheduleRepository _repository;
    private readonly ILogger<AITrainingService> _logger;

    // 可配置的 Python 脚本路径
    private const string DefaultPythonScriptPath = "path/to/ai_training.py";

    public AITrainingService(
        IScheduleRepository repository,
        ILogger<AITrainingService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> TriggerAITrainingAsync(DateTime executionDate)
    {
        var log = new AITrainingLog
        {
            ExecutionDate = executionDate.Date,
            StartTime = DateTime.Now,
            Status = "Started",
            Message = "AI Training triggered"
        };

        try
        {
            // 启动 Python 进程（后台运行）
            var processInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = DefaultPythonScriptPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new InvalidOperationException("无法启动 Python 进程");
            }

            log.ProcessId = process.Id;
            await _repository.SaveAITrainingLogAsync(log);

            _logger.LogInformation($"AI Training 已启动 (ProcessId: {process.Id}, ExecutionDate: {executionDate:yyyy-MM-dd})");

            // 后台等待完成（不阻塞主线程）
            _ = Task.Run(async () =>
            {
                try
                {
                    process.WaitForExit();
                    log.EndTime = DateTime.Now;
                    log.Status = "Completed";
                    log.DurationMinutes = (int)(log.EndTime.Value - log.StartTime).TotalMinutes;
                    log.UpdatedAt = DateTime.Now;

                    await _repository.SaveAITrainingLogAsync(log);

                    _logger.LogInformation($"AI Training 已完成 (耗时: {log.DurationMinutes} 分钟)");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "等待 AI Training 完成时出错");
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发 AI Training 失败");
            log.Status = "Failed";
            log.Message = ex.Message;
            log.EndTime = DateTime.Now;
            log.UpdatedAt = DateTime.Now;

            await _repository.SaveAITrainingLogAsync(log);

            return false;
        }
    }

    public async Task<AITrainingLog?> GetTrainingLogAsync(DateTime executionDate)
    {
        return await _repository.GetAITrainingLogAsync(executionDate);
    }
}
