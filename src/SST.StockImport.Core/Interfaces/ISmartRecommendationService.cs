using SST.StockImport.Core.DTOs.SmartRecommendation;

namespace SST.StockImport.Core.Interfaces;

/// <summary>
/// 智能推荐服务接口
/// </summary>
public interface ISmartRecommendationService
{
    /// <summary>
    /// 获取今日智能推荐（基于历史成功模式）
    /// </summary>
    /// <param name="request">推荐请求参数</param>
    /// <returns>推荐结果</returns>
    Task<SmartRecommendationResponse> GetTodayRecommendationsAsync(SmartRecommendationRequest request);

    /// <summary>
    /// 获取分类推荐（量能、大阳线、下影线各3档，应用最佳策略）
    /// </summary>
    /// <param name="request">分类推荐请求参数</param>
    /// <returns>分类推荐结果</returns>
    Task<CategoryRecommendationResponse> GetCategoryRecommendationsAsync(CategoryRecommendationRequest request);
}
