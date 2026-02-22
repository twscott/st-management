# 推荐记录优化方案

## 问题分析
当前每次API请求都要实时计算推荐频率，查询过去20天数据，性能开销约0.3-0.5秒。

## 解决方案：创建推荐历史表

### 1. 数据库表设计

```sql
CREATE TABLE recommendation_history (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    stock_code VARCHAR(10) NOT NULL,
    recommendation_date DATE NOT NULL,
    hotspot_date DATE NOT NULL,
    cooling_days INT NOT NULL,
    maturity_score DECIMAL(5,2) NOT NULL,
    volume_ratio DECIMAL(10,2) NOT NULL,
    volume_score INT,
    positive_money_days INT,
    negative_money_days INT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_stock_date (stock_code, recommendation_date),
    INDEX idx_recommendation_date (recommendation_date),
    INDEX idx_stock_code (stock_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

### 2. 每日批次任务

```csharp
public class RecommendationHistoryProcessor : IDataProcessor
{
    public async Task ProcessAsync(DateTime targetDate)
    {
        // 1. 计算当天的推荐结果
        var recommendations = await _smartRecommendationService
            .GetTodayRecommendationsAsync(targetDate);
        
        // 2. 保存到历史表
        foreach (var rec in recommendations)
        {
            await SaveRecommendationHistory(rec, targetDate);
        }
        
        // 3. 清理60天以前的历史记录
        await CleanupOldHistory(targetDate.AddDays(-60));
    }
}
```

### 3. 快速查询推荐频率

```csharp
public async Task<int> GetRecommendationFrequencyFastAsync(
    string stockCode, 
    DateTime targetDate)
{
    var sql = @"
        SELECT COUNT(*) 
        FROM recommendation_history
        WHERE stock_code = @stockCode
          AND recommendation_date BETWEEN DATE_SUB(@targetDate, INTERVAL 20 DAY) 
                                      AND @targetDate";
    
    return await _context.Database
        .ExecuteSqlRawAsync(sql, stockCode, targetDate);
}
```

### 4. 性能对比

| 方案 | 查询时间 | 存储成本 | 数据准确性 |
|------|---------|---------|-----------|
| 实时计算 | 0.3-0.5秒 | 0 | 100% |
| 历史表 | 0.01-0.02秒 | ~10MB/年 | 99.9% |

### 5. 实施建议

**阶段1（当前）**：保持实时计算
- 优点：简单，无需schema变更
- 适用：日访问量 < 1000次/天

**阶段2（优化）**：添加历史表
- 触发条件：日访问量 > 1000次 或 响应时间 > 1秒
- 实施时间：1-2天
- 回滚风险：低（可随时切回实时计算）

### 6. 混合方案（推荐）

```csharp
public async Task<int> GetRecommendationFrequencyAsync(
    string stockCode, 
    DateTime targetDate)
{
    // 优先使用历史表（快速）
    var fromHistory = await GetFromHistoryTable(stockCode, targetDate);
    if (fromHistory > 0) return fromHistory;
    
    // 降级到实时计算（准确）
    return await CalculateRecommendationFrequencyAsync(stockCode, targetDate);
}
```

## 结论

**现阶段建议**：继续使用实时计算
- 系统刚上线，访问量不大
- 数据质量优先于性能
- 避免过早优化

**未来优化**：当出现以下情况时，考虑添加历史表
- 日请求量 > 1000次
- 用户反馈响应慢
- 需要更复杂的推荐频率分析（如：月度统计、趋势分析）
