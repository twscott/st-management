using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.Entities;
using SST.StockImport.Infrastructure.Repositories;

namespace SST.StockImport.Services;

/// <summary>
/// GoodInfo 失败链服务实现
/// </summary>
public class GoodInfoFailedLinkService : IGoodInfoFailedLinkService
{
    private readonly IScheduleRepository _repository;
    private readonly ILogger<GoodInfoFailedLinkService> _logger;

    // GoodInfo links 的名称映射（link ID -> 名称）
    private static readonly Dictionary<int, string> LinkIdToNameMap = new()
    {
        { 1, "券資比" },
        { 2, "MACD負轉正" },
        { 3, "移動平均線" },
        { 4, "布林格通道" },
        { 5, "投信連續買賣" },
        { 6, "融資融券比" },
        { 7, "外資連續買賣" },
        { 8, "週轉率" },
        { 9, "成交量" },
        { 10, "股價變動" },
        { 11, "營利" },
        { 12, "財務報表" },
        { 13, "現金流" },
        { 14, "負債比" },
        { 15, "ROE" },
        { 16, "EPS趨勢" },
        { 17, "股利政策" },
        { 18, "技術面強度" },
        { 19, "綜合評分" }
    };

    public GoodInfoFailedLinkService(
        IScheduleRepository repository,
        ILogger<GoodInfoFailedLinkService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task TrackFailedLinksAsync(DateTime executionDate, List<string> failedLinkNames)
    {
        // 将名称转换为 ID
        var failedLinkIds = ConvertNamesToIds(failedLinkNames);

        var tracking = new GoodInfoFailedLinkTracking
        {
            ExecutionDate = executionDate,
            FailedLinkIds = failedLinkIds,
            TotalLinks = 19,
            SuccessCount = 19 - failedLinkIds.Count,
            FailCount = failedLinkIds.Count,
            TotalRetries = 0,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        await _repository.SaveFailedLinksAsync(tracking);

        _logger.LogInformation($"追踪失败链: {string.Join(",", failedLinkNames)} (执行日期: {executionDate:yyyy-MM-dd})");
    }

    public async Task<List<int>> GetFailedLinksAsync(DateTime executionDate)
    {
        var tracking = await _repository.GetFailedLinksAsync(executionDate);
        return tracking?.FailedLinkIds ?? new List<int>();
    }

    public async Task UpdateFailedLinksAsync(DateTime executionDate, List<string> newFailedLinkNames, string retrySlot)
    {
        var tracking = await _repository.GetFailedLinksAsync(executionDate);
        if (tracking == null)
        {
            _logger.LogWarning($"未找到执行日期 {executionDate:yyyy-MM-dd} 的失败链追踪记录");
            return;
        }

        var newFailedLinkIds = ConvertNamesToIds(newFailedLinkNames);

        tracking.FailedLinkIds = newFailedLinkIds;
        tracking.FailCount = newFailedLinkIds.Count;
        tracking.SuccessCount = 19 - newFailedLinkIds.Count;
        tracking.TotalRetries++;
        tracking.LastRetryTime = DateTime.Now;
        tracking.LastRetrySlot = retrySlot;
        tracking.UpdatedAt = DateTime.Now;

        await _repository.SaveFailedLinksAsync(tracking);

        _logger.LogInformation($"更新失败链: {string.Join(",", newFailedLinkNames)} (重试: {retrySlot})");
    }

    public async Task ClearFailedLinksAsync(DateTime executionDate)
    {
        var tracking = await _repository.GetFailedLinksAsync(executionDate);
        if (tracking != null)
        {
            tracking.FailedLinkIds.Clear();
            tracking.FailCount = 0;
            tracking.SuccessCount = 19;
            tracking.UpdatedAt = DateTime.Now;

            await _repository.SaveFailedLinksAsync(tracking);

            _logger.LogInformation($"清空失败链 (执行日期: {executionDate:yyyy-MM-dd})");
        }
    }

    /// <summary>
    /// 获取 link ID 的名称
    /// </summary>
    public static string GetLinkName(int linkId)
    {
        return LinkIdToNameMap.TryGetValue(linkId, out var name) ? name : $"Link {linkId}";
    }

    /// <summary>
    /// 将 link 名称转换为 ID
    /// </summary>
    private List<int> ConvertNamesToIds(List<string> failedLinkNames)
    {
        if (failedLinkNames == null || failedLinkNames.Count == 0)
            return new List<int>();

        var failedIds = new List<int>();

        foreach (var name in failedLinkNames)
        {
            var matchedId = LinkIdToNameMap
                .FirstOrDefault(x => x.Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                .Key;

            if (matchedId > 0)
            {
                failedIds.Add(matchedId);
            }
            else
            {
                // 如果无法找到名称，尝试直接解析为 ID
                if (int.TryParse(name, out var id) && id > 0 && id <= 19)
                {
                    failedIds.Add(id);
                }
            }
        }

        return failedIds;
    }
}
