using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SST.StockImport.Services;

/// <summary>
/// GoodInfo 失败链追踪服务接口
/// </summary>
public interface IGoodInfoFailedLinkService
{
    /// <summary>
    /// 追踪失败的链
    /// </summary>
    Task TrackFailedLinksAsync(DateTime executionDate, List<string> failedLinkNames);

    /// <summary>
    /// 获取待重试的失败链 ID
    /// </summary>
    Task<List<int>> GetFailedLinksAsync(DateTime executionDate);

    /// <summary>
    /// 更新失败链（重试后）
    /// </summary>
    Task UpdateFailedLinksAsync(DateTime executionDate, List<string> newFailedLinkNames, string retrySlot);

    /// <summary>
    /// 清除失败链追踪
    /// </summary>
    Task ClearFailedLinksAsync(DateTime executionDate);
}
